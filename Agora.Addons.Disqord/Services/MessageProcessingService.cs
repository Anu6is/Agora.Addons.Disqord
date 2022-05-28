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
using Microsoft.Extensions.Logging;
using System.Text;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class MessageProcessingService : AgoraService, IProductListingService, IAuditLogService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        private readonly IInteractionContextAccessor _contextAccessor;
        
        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }        

        public MessageProcessingService(DiscordBotBase bot, IInteractionContextAccessor contextAccessor, ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
            _contextAccessor = contextAccessor;
        }

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            var productEmbed = productListing.ToEmbed();
            var message = new LocalMessage().AddEmbed(productEmbed).WithComponents(productListing.Buttons());
            var response = await _agora.SendMessageAsync(ShowroomId.Value, message);
            
            return ReferenceNumber.Create(response.Id);
        }

        public async ValueTask<ReferenceNumber> UpdateProductListingAsync(Listing productListing)
        {
            var productEmbeds = new List<LocalEmbed>() { productListing.ToEmbed() };

            if (_contextAccessor.Context == null)
                await _agora.ModifyMessageAsync(ShowroomId.Value, 
                    productListing.Product.ReferenceNumber.Value,
                    x =>
                    {
                        x.Embeds = productEmbeds;
                        x.Components = productListing.Buttons();
                    });
            else
                await _contextAccessor.Context.Interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse()
                {
                    Embeds = productEmbeds,
                    Components = productListing.Buttons()
                });

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
                var thread = await _agora.CreatePublicThreadAsync(ShowroomId.Value, $"[{listing.ReferenceCode}] {product.Title}", product.ReferenceNumber.Value, duration);
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
            var quantity = productListing.Product.Quantity;
            var value = productListing.ValueTag.ToString();
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var host = Mention.User(productListing.User.ReferenceNumber.Value);
            
            if (owner != productListing.User.ReferenceNumber.Value) 
                intermediary = $" on behalf of {(productListing.Anonymous ? Markdown.Italics("Anonymous") : Mention.User(owner))}";
            else
                host = productListing.Anonymous ? Markdown.Italics("Anonymous") : host;
            
            var embed = new LocalEmbed().WithDescription($"{host} listed {Markdown.Bold(title)} (x{quantity}){intermediary} for {Markdown.Bold(value)}")
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .WithFooter($"{productListing} | {productListing.ReferenceCode}")
                                        .WithColor(Color.SteelBlue);
            
            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));
            
            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogListingUpdatedAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogListingWithdrawnAsync(Listing productListing)
        {
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var quantity = productListing.Product.Quantity.ToString();
            var user = string.Empty;

            if (owner != productListing.User.ReferenceNumber.Value) user = $"by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var embed = new LocalEmbed().WithDescription($"{Markdown.Bold(title)} (x{quantity}) hosted by {Mention.User(owner)} has been {Markdown.Underline("withrawn")} {user}")
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
            var title = productListing.Product.Title.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var submitter = offer.User.ReferenceNumber.Value;
            var quantity = productListing.Product.Quantity.ToString();
            var user = string.Empty;

            if (submitter != productListing.User.ReferenceNumber.Value) user = $" by {Mention.User(productListing.User.ReferenceNumber.Value)}";

            var description = new StringBuilder()
                .Append("An offer of ").Append(Markdown.Bold(offer.Submission))
                .Append(" made by ").Append(Mention.User(submitter))
                .Append(" for ").Append(Markdown.Bold(title)).Append(" (x").Append(quantity).Append(") ")
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
            var quantity = productListing.Product.Quantity;
            var title = productListing.Product.Title.ToString();
            var value = productListing.CurrentOffer.Submission.ToString();
            var owner = productListing.Owner.ReferenceNumber.Value;
            var buyer = productListing.CurrentOffer.User.ReferenceNumber.Value;
            var duration = DateTimeOffset.UtcNow.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;

            var description = new StringBuilder()
                .Append(Markdown.Bold(title)).Append(" (x").Append(quantity).Append(") ")
                .Append("hosted by ").Append(Mention.User(owner))
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
            var quantity = productListing.Product.Quantity.ToString();
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;

            var description = new StringBuilder()
                .Append(Markdown.Bold(title)).Append(" (x").Append(quantity).Append(") ")
                .Append("hosted by ").Append(Mention.User(owner))
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
            var buyer = productListing.CurrentOffer.User.ReferenceNumber.Value;
            var embed = new LocalEmbed().WithTitle($"{productListing} Claimed")
                                        .AddField($"{productListing.Product.Title} (x{productListing.Product.Quantity})", productListing.CurrentOffer.Submission.ToString())
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Claimed By", Mention.User(buyer))
                                        .WithColor(Color.Teal);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent($"{Mention.User(owner)}\n{Mention.User(buyer)}").AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask<ReferenceNumber> PostListingExpiredAsync(Listing productListing)
        {
            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var embed = new LocalEmbed().WithTitle($"{productListing} Expired")
                                        .AddField(productListing.Product.Title.ToString(), productListing.ValueTag.ToString())
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Duration", duration.Humanize())
                                        .WithColor(Color.SlateGray);

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().WithContent(Mention.User(owner)).AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }
    }
}