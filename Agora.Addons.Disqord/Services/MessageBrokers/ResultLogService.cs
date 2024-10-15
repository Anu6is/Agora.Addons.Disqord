using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Services;
using Agora.Shared.Attributes;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class ResultLogService : AgoraService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly PluginManagerService _pluginService;

        private readonly ILogger _logger;
        private readonly IGuildSettingsService _settingsService;
        private readonly ICommandContextAccessor _commandAccessor;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public ResultLogService(DiscordBotBase bot,
                                PluginManagerService pluginService,
                                IGuildSettingsService settingsService,
                                ICommandContextAccessor commandAccessor,
                                IInteractionContextAccessor interactionAccessor,
                                ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _logger = logger;
            _pluginService = pluginService;
            _commandAccessor = commandAccessor;
            _settingsService = settingsService;
            _interactionAccessor = interactionAccessor;
        }

        public async ValueTask<ReferenceNumber> PostListingSoldAsync(Listing productListing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel)
                ShowroomId = new ShowroomId(productListing.ReferenceCode.Reference());

            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, productListing.ShowroomId.Value, result.FailureReason));

            var winners = productListing switch
            {
                StandardGiveaway giveaway => giveaway.Winners,
                RaffleGiveaway giveaway => giveaway.Winners,
                _ => Array.Empty<Ticket>()
            };

            winners ??= Array.Empty<Ticket>();

            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var claimant = winners.Length <= 1
                ? Mention.User(buyer)
                : string.Join(" | ", winners.Select(x => Mention.User(x.UserReference.Value)));
            var participants = $"{Mention.User(owner)} | {claimant}";
            var stock = productListing is MassMarket or MultiItemMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;
            var value = winners.Length <= 1
                ? Markdown.Bold(productListing.CurrentOffer.Submission)
                : string.Join(", ", winners.Select(x => Markdown.Bold(x.Submission)));
            var quantity = stock == 1 ? string.Empty : $"{stock} ";
            var listing = productListing is CommissionTrade ? "Trade Request" : productListing.ToString();
            var embed = new LocalEmbed().WithTitle($"{listing} Claimed")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} for {value}")
                                        .WithColor(Color.Teal);

            var carousel = productListing.Product.Carousel;

            if (owner != buyer && winners.Length <= 1)
                embed.WithFooter("review this transaction | right-click -> apps -> review");

            if (carousel != null && carousel.Images != null && carousel.Images.Count != 0)
                embed.WithThumbnailUrl(carousel.Images[0].Url);

            if (productListing.Product.Description != null)
                embed.AddField("Description", productListing.Product.Description.Value);

            embed.AddInlineField("Owner", Mention.User(owner)).AddInlineField("Claimed By", claimant);

            var isForumPost = _agora.GetChannel(EmporiumId.Value, productListing.ShowroomId.Value) is CachedForumChannel;
            var link = isForumPost ? $"\n{Discord.MessageJumpLink(EmporiumId.Value, productListing.Product.ReferenceNumber.Value, productListing.ReferenceCode.Reference())}" : string.Empty;
            var parameters = new PluginParameters() { { "Listing", productListing } };
            var pluginResult = await _pluginService.ExecutePlugin("CustomAnnouncement", parameters) as Result<string>;
            var content = pluginResult.IsSuccessful ? pluginResult.Data : $"{participants}{link}";
            var localMessage = new LocalMessage().WithContent(content).AddEmbed(embed);

            result = await AttachOfferLogsAsync(localMessage, productListing.Product, EmporiumId.Value, ShowroomId.Value);

            if (result is IExceptionResult)
                await TrySendFeedbackAsync(EmporiumId.Value, productListing.ShowroomId.Value, result.FailureReason);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, localMessage);
            var delivered = await SendHiddenMessage(productListing, message);

            if (message == null) return null;

            if (!delivered) await message.ModifyAsync(x =>
            {
                x.Content = participants;
                x.Embeds = new[] { embed.WithFooter("The message attached to this listing failed to be delivered.") };
            });

            if (channel is CachedForumChannel) await LockPostAsync();

            return ReferenceNumber.Create(message.Id);
        }

        private async Task<IResult> AttachOfferLogsAsync(LocalMessage localMessage, Product product, ulong emporiumId, ulong showroomId)
        {
            if (product is MarketItem || product is TradeItem) return Result.Failure("Listing type does not support attachments");

            var settings = await _settingsService.GetGuildSettingsAsync(emporiumId);

            if (product is AuctionItem && !settings.Features.AttachListingLogs) return Result.Failure("Attachment settings disabled");

            var result = await CheckPermissionsAsync(emporiumId, showroomId, Permissions.SendAttachments);

            if (!result.IsSuccessful)
                return Result.Exception("The bot lacks the necessary permissions to attach logs", null);

            IEnumerable<OfferLog> logs = product switch
            {
                GiveawayItem giveaway => giveaway.Offers.Select(ticket => new OfferLog(ticket.UserReference.Value, ticket.Submission.Value, ticket.SubmittedOn)),
                AuctionItem auction => auction.Offers.Select(bid => new OfferLog(bid.UserReference.Value, bid.Amount.Value.ToString(), bid.SubmittedOn)),
                _ => Array.Empty<OfferLog>(),
            };

            var stream = new MemoryStream();
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions() { WriteIndented = true });

            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(json);
            await writer.FlushAsync();

            stream.Seek(0, SeekOrigin.Begin);

            localMessage.AddAttachment(new LocalAttachment(stream, $"{logs.Count()} offers"));

            return Result.Success();
        }

        public async ValueTask<ReferenceNumber> PostListingExpiredAsync(Listing productListing)
        {
            var channel = _agora.GetChannel(EmporiumId.Value, ShowroomId.Value);

            if (channel is CachedCategoryChannel or CachedForumChannel)
                ShowroomId = new ShowroomId(productListing.ReferenceCode.Reference());

            var result = await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.ViewChannels | Permissions.SendMessages | Permissions.SendEmbeds);

            if (!result.IsSuccessful) return ReferenceNumber.Create(await TrySendFeedbackAsync(EmporiumId.Value, ShowroomId.Value, result.FailureReason));

            var owner = productListing.Owner.ReferenceNumber.Value;
            var expiration = productListing.ExpirationDate.AddSeconds(1);
            var clock = SystemClock.Now.ToOffset(expiration.Offset).AddSeconds(3);
            var duration = clock < expiration ? TimeSpan.Zero : expiration - productListing.ScheduledPeriod.ScheduledStart;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var embed = new LocalEmbed().WithTitle($"{productListing} Expired")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} @ {Markdown.Bold(productListing.ValueTag)}")
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Duration", duration == TimeSpan.Zero ? "Cancelled" : duration.Humanize(minUnit: Humanizer.Localisation.TimeUnit.Second))
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(Mention.User(owner)).AddEmbed(embed));

            if (channel is CachedForumChannel) await LockPostAsync();

            return ReferenceNumber.Create(message.Id);
        }

        private async ValueTask<bool> SendHiddenMessage(Listing productListing, IUserMessage message)
        {
            if (productListing.HiddenMessage == null) return true;

            var guildId = productListing.Owner.EmporiumId;
            var embed = new LocalEmbed().WithTitle("Acquisition includes a message!")
                                        .WithDescription(productListing.HiddenMessage.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode.Code()}")
                                        .WithColor(Color.Teal);

            try
            {
                var directChannel = await _agora.CreateDirectChannelAsync(productListing.CurrentOffer.UserReference.Value);

                await directChannel.SendMessageAsync(
                    new LocalMessage()
                        .AddEmbed(embed)
                        .AddComponent(
                            new LocalRowComponent()
                                .WithComponents(
                                    new LocalLinkButtonComponent()
                                        .WithLabel("View Results")
                                        .WithUrl($"https://discordapp.com/channels/{guildId}/{message.ChannelId}/{message.Id}"))));
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private async ValueTask<IResult> CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel is null)
            {
                var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

                settings.ResultLogChannelId = 0;

                await _settingsService.UpdateGuildSettingsAync(settings);

                return Result.Failure($"Unable to verify channel permissions for {Mention.Channel(channelId)}");
            }

            var channelPerms = currentMember.CalculateChannelPermissions(channel);

            if (channelPerms.HasFlag(permissions)) return Result.Success();

            var message = $"The bot lacks the necessary permissions ({permissions & ~channelPerms}) to post to {Mention.Channel(ShowroomId.Value)}";

            return Result.Failure(message);
        }

        private async Task<ulong> GetFeedbackChannelAsync(ulong guildId, ulong channelId)
        {
            var feedbackId = _interactionAccessor?.Context?.ChannelId ?? _commandAccessor?.Context?.ChannelId;

            if (feedbackId.HasValue && feedbackId != channelId) return feedbackId.Value;

            var settings = await _agora.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(guildId);

            if (settings.AuditLogChannelId != 0) return settings.AuditLogChannelId;
            else if (settings.ResultLogChannelId > 1) return settings.ResultLogChannelId;

            return 0;
        }

        private async Task<ulong> TrySendFeedbackAsync(ulong guildId, ulong channelId, string message)
        {
            var interaction = _interactionAccessor?.Context?.Interaction;
            var embed = new LocalEmbed().WithDescription(message).WithColor(Color.Red);

            _logger.LogDebug("Action failure feedback: {message}", message);

            try
            {
                if (interaction is null || interaction is IComponentInteraction)
                {
                    var feedbackId = await GetFeedbackChannelAsync(guildId, channelId);

                    if (feedbackId == 0) return feedbackId;

                    var response = await _agora.SendMessageAsync(feedbackId, new LocalMessage().AddEmbed(embed));
                    return response.Id;
                }
                else
                {
                    await interaction.SendMessageAsync(new LocalInteractionMessageResponse().AddEmbed(embed));
                    return interaction.Id;
                }
            }
            catch (Exception)
            {
                //unable to notify the user
            }

            return 0;
        }

        private async Task LockPostAsync()
        {
            if (_agora.GetChannel(EmporiumId.Value, ShowroomId.Value) is not CachedThreadChannel post) return;

            await Task.Delay(1000);

            await post.ModifyAsync(x =>
            {
                x.IsLocked = true;
                x.AutomaticArchiveDuration = TimeSpan.FromHours(24);
            });
        }
    }
}
