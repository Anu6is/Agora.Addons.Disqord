using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class MessageProcessingService : AgoraService, IProductListingService, IAuditLogService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly IInteractionContextAccessor _interactionAccessor;

        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }

        public MessageProcessingService(DiscordBotBase bot, IInteractionContextAccessor interactionAccessor, ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _interactionAccessor = interactionAccessor;
        }

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            var categorization = await GetCategoryAsync(productListing);
            var message = new LocalMessage().AddEmbed(productListing.ToEmbed().WithCategory(categorization)).WithComponents(productListing.Buttons());
            var response = await _agora.SendMessageAsync(ShowroomId.Value, message);

            return ReferenceNumber.Create(response.Id);
        }

        public async ValueTask<ReferenceNumber> UpdateProductListingAsync(Listing productListing)
        {
            var categorization = await GetCategoryAsync(productListing);
            var productEmbeds = new List<LocalEmbed>() { productListing.ToEmbed().WithCategory(categorization) };

            if (_interactionAccessor.Context == null)
            {
                await _agora.ModifyMessageAsync(ShowroomId.Value,
                    productListing.Product.ReferenceNumber.Value,
                    x =>
                    {
                        x.Embeds = productEmbeds;
                        x.Components = productListing.Buttons();
                    });
            }
            else
            {
                var interaction = _interactionAccessor.Context.Interaction;

                if (interaction.Response().HasResponded)
                    await interaction.Followup().ModifyResponseAsync(x =>
                    {
                        x.Embeds = productEmbeds;
                        x.Components = productListing.Buttons();
                    });
                else
                    await interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
                    {
                        Embeds = productEmbeds,
                        Components = productListing.Buttons()
                    });
            }

            return productListing.Product.ReferenceNumber;
        }

        public async ValueTask<ReferenceNumber> OpenBarteringChannelAsync(Listing listing)
        {
            var duration = listing.ScheduledPeriod.Duration switch
            {
                var minutes when minutes < TimeSpan.FromMinutes(60) => TimeSpan.FromHours(1),
                var hours when hours < TimeSpan.FromHours(24) => TimeSpan.FromDays(1),
                var days when days < TimeSpan.FromDays(3) => TimeSpan.FromDays(3),
                _ => TimeSpan.FromDays(7),
            };

            try
            {
                var product = listing.Product;
                var thread = await _agora.CreatePublicThreadAsync(ShowroomId.Value, $"[{listing.ReferenceCode}] {product.Title}", product.ReferenceNumber.Value, x => x.AutomaticArchiveDuration = duration);
                
                await thread.SendMessageAsync(new LocalMessage().WithContent("Execute commands for this item HERE!"));
                
                return ReferenceNumber.Create(thread.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create bartering channel");
                return null;
            }
        }

        public async ValueTask CloseBarteringChannelAsync(ReferenceNumber referenceNumber)
        {
            try
            {
                await _agora.DeleteChannelAsync(referenceNumber.Value);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to close bartering channel.");
            }

            return;
        }

        public async ValueTask RemoveProductListingAsync(ReferenceNumber referenceNumber)
        {
            await _agora.DeleteMessageAsync(ShowroomId.Value, referenceNumber.Value);
            return;
        }

        public async ValueTask<ReferenceNumber> LogListingCreatedAsync(Listing productListing)
        {
            var intermediary = string.Empty;
            var value = productListing.ValueTag.ToString();
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var host = Mention.User(productListing.User.ReferenceNumber.Value);
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var original = productListing switch
            {
                StandardMarket { Discount: > 0, Product: MarketItem item } => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                FlashMarket { Discount: > 0, Product: MarketItem item } market => $"{Markdown.Strikethrough(item.Price.ToString())} ",
                MassMarket { Product: MarketItem item } market => market.CostPerItem.Value * item.Quantity.Amount > item.CurrentPrice 
                                                                ? $"{Markdown.Strikethrough(Money.Create(market.CostPerItem.Value * item.Quantity.Amount, item.Price.Currency))} " 
                                                                : string.Empty,
                _ => string.Empty
            };

            if (owner != productListing.User.ReferenceNumber.Value)
                intermediary = $" on behalf of {(productListing.Anonymous ? Markdown.Italics("Anonymous") : Mention.User(owner))}";
            else
                host = productListing.Anonymous ? Markdown.Italics("Anonymous") : host;

            var embed = new LocalEmbed().WithDescription($"{host} {Markdown.Underline("listed")} {Markdown.Bold($"{quantity}{title}")}{intermediary} for {original}{Markdown.Bold(value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.SteelBlue);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogListingUpdatedAsync(Listing productListing)
        {
            throw new NotImplementedException(); //TODO log updates
        }

        public async ValueTask<ReferenceNumber> LogListingWithdrawnAsync(Listing productListing)
        {
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";
            var user = string.Empty;

            if (owner != productListing.User.ReferenceNumber.Value) user = $"by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var embed = new LocalEmbed().WithDescription($"{Markdown.Bold($"{quantity}{title}")} hosted by {Mention.User(owner)} has been {Markdown.Underline("withrawn")} {user}")
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.OrangeRed);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogOfferSubmittedAsync(Listing productListing, Offer offer)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogOfferRevokedAsync(Listing productListing, Offer offer)
        {
            var user = string.Empty;
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var submitter = offer.UserReference.Value;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            if (submitter != productListing.User.ReferenceNumber.Value) user = $" by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var description = new StringBuilder()
                .Append("An offer of ").Append(Markdown.Bold(offer.Submission))
                .Append(" made by ").Append(Mention.User(submitter))
                .Append(" for ").Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" has been ").Append(Markdown.Underline("withdrawn")).Append(user);

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.Orange);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogOfferAcceptedAsync(Listing productListing, Offer offer)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogListingSoldAsync(Listing productListing)
        {
            var title = productListing.Product.Title.ToString();
            var value = productListing.CurrentOffer.Submission.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var duration = DateTimeOffset.UtcNow.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var stock = productListing is MassMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;
            var quantity = stock == 1 ? string.Empty : $"{stock} ";

            var description = new StringBuilder()
                .Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" was ").Append(Markdown.Underline("claimed"))
                .Append(" after ").Append(duration.Humanize())
                .Append(" for ").Append(Markdown.Bold(value))
                .Append(" by ").Append(Mention.User(buyer));

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.Teal);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> LogListingExpiredAsync(Listing productListing)
        {
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var quantity = productListing.Product.Quantity.Amount == 1 ? string.Empty : $"[{productListing.Product.Quantity}] ";

            var description = new StringBuilder()
                .Append(Markdown.Bold($"{quantity}{title}"))
                .Append(" hosted by ").Append(Mention.User(owner))
                .Append(" has ").Append(Markdown.Underline("expired"))
                .Append(" after ").Append(duration.Humanize());

            var embed = new LocalEmbed().WithDescription(description.ToString())
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingSoldAsync(Listing productListing)
        {
            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.UserReference.Value;
            var participants = $"{Mention.User(owner)} | {Mention.User(buyer)}";
            var stock = productListing is MassMarket
                ? (productListing.Product as MarketItem).Offers.OrderBy(x => x.SubmittedOn).Last().ItemCount
                : productListing.Product.Quantity.Amount;

            var quantity = stock == 1 ? string.Empty : $"{stock} ";
            var embed = new LocalEmbed().WithTitle($"{productListing} Claimed")
                                        .WithDescription($"{Markdown.Bold($"{quantity}{productListing.Product.Title}")} for {Markdown.Bold(productListing.CurrentOffer.Submission)}")
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Claimed By", Mention.User(buyer))
                                        .WithColor(Color.Teal);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(participants).AddEmbed(embed));
            var delivered = await SendHiddenMessage(productListing, message);

            if (!delivered)
                await message.ModifyAsync(x =>
                {
                    x.Content = participants;
                    x.Embeds = new[] { embed.WithFooter("The message attached to this listing failed to be delivered.") };
                });

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingExpiredAsync(Listing productListing)
        {
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
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
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

        private async Task<string> GetCategoryAsync(Listing productListing)
        {
            string categorization = string.Empty;
            var subcategoryId = productListing.Product.SubCategoryId;

            if (subcategoryId != null)
            {
                var emporium = await _agora.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(productListing.Owner.EmporiumId.Value);
                var category = emporium.Categories.FirstOrDefault(c => c.SubCategories.Any(s => s.Id.Equals(subcategoryId)));
                var subcategory = category.SubCategories.FirstOrDefault(s => s.Id.Equals(subcategoryId));

                categorization = $"{category.Title}{(subcategory.Title.Equals(category.Title.Value)  ? "" : $": {subcategory.Title}")}";
            }

            return categorization;
        }
    }
}