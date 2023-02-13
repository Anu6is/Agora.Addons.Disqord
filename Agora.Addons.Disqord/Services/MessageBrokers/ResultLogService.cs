using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Cache;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public sealed class ResultLogService : AgoraService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly IGuildSettingsService _settingsService;
        private readonly ICommandContextAccessor _commandAccessor;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public ResultLogService(DiscordBotBase bot,
                                IGuildSettingsService settingsService,
                                ICommandContextAccessor commandAccessor,
                                IInteractionContextAccessor interactionAccessor,
                                ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _commandAccessor = commandAccessor;
            _settingsService = settingsService;
            _interactionAccessor = interactionAccessor;
        }

        public async ValueTask<ReferenceNumber> PostListingSoldAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var participants = $"{Mention.User(owner)} | {Mention.User(buyer)}";
            var stock = productListing is MassMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;

            var quantity = stock == 1 ? string.Empty : $"{stock} ";
            var embed = new LocalEmbed().WithTitle($"{productListing} Claimed")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} for {Markdown.Bold(productListing.CurrentOffer.Submission)}")
                                        .WithColor(Color.Teal);

            var carousel = productListing.Product.Carousel;

            if (owner != buyer)
                embed.WithFooter("review this transaction | right-click -> apps -> review");

            if (carousel != null && carousel.Images != null && carousel.Images.Any())
                embed.WithThumbnailUrl(carousel.Images[0].Url);

            if (productListing.Product.Description != null)
                embed.AddField("Description", productListing.Product.Description.Value);

            embed.AddInlineField("Owner", Mention.User(owner)).AddInlineField("Claimed By", Mention.User(buyer));

            var localMessage = new LocalMessage().WithContent(participants).AddEmbed(embed);
            var message = await _agora.SendMessageAsync(ShowroomId.Value, localMessage);
            var delivered = await SendHiddenMessage(productListing, message);

            if (message == null) return null;

            if (!delivered) await message.ModifyAsync(x =>
            {
                x.Content = participants;
                x.Embeds = new[] { embed.WithFooter("The message attached to this listing failed to be delivered.") };
            });

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingExpiredAsync(Listing productListing)
        {
            await CheckPermissionsAsync(EmporiumId.Value, ShowroomId.Value, Permissions.SendMessages | Permissions.SendEmbeds);

            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var embed = new LocalEmbed().WithTitle($"{productListing} Expired")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} @ {Markdown.Bold(productListing.ValueTag)}")
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Duration", duration.Humanize())
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(Mention.User(owner)).AddEmbed(embed));

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

        private async ValueTask CheckPermissionsAsync(ulong guildId, ulong channelId, Permissions permissions)
        {
            var currentMember = _agora.GetCurrentMember(guildId);
            var channel = _agora.GetChannel(guildId, channelId);

            if (channel == null)
            {
                var settings = await _settingsService.GetGuildSettingsAsync(EmporiumId.Value);

                settings.ResultLogChannelId = 0;

                await _settingsService.UpdateGuildSettingsAync(settings);

                throw new NoMatchFoundException($"Unable to verify channel permissions for {Mention.Channel(channelId)}");
            }

            var channelPerms = currentMember.CalculateChannelPermissions(channel);

            if (!channelPerms.HasFlag(permissions))
            {
                var message = $"The bot lacks the necessary permissions ({permissions & ~channelPerms}) to post to {Mention.Channel(ShowroomId.Value)}";
                var feedbackId = _interactionAccessor?.Context?.ChannelId ?? _commandAccessor?.Context?.ChannelId;

                if (feedbackId.HasValue && feedbackId != channelId)
                {
                    await _agora.SendMessageAsync(feedbackId.Value, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                }
                else
                {
                    var settings = await _agora.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(guildId);

                    if (settings.AuditLogChannelId != 0)
                        await _agora.SendMessageAsync(settings.AuditLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                    else if (settings.ResultLogChannelId != 0)
                        await _agora.SendMessageAsync(settings.ResultLogChannelId, new LocalMessage().AddEmbed(new LocalEmbed().WithDescription(message).WithColor(Color.Red)));
                }

                throw new InvalidOperationException(message);
            }

            return;
        }
    }
}
