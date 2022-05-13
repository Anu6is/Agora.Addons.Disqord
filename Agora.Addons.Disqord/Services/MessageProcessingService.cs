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

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class MessageProcessingService : AgoraService, IProductListingService, IAuditLogService, IResultLogService
    {
        private readonly DiscordBotBase _agora;
        
        public EmporiumId EmporiumId { get; set; }
        public ShowroomId ShowroomId { get; set; }        

        public MessageProcessingService(DiscordBotBase bot, ILogger<MessageProcessingService> logger) : base(logger)
        {
            _agora = bot;
        }

        public async ValueTask<ReferenceNumber> PostProductListingAsync(Listing productListing)
        {
            var productEmbed = productListing.ToEmbed();
            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(productEmbed));
            
            return ReferenceNumber.Create(message.Id);
        }

        public async ValueTask RemoveProductListingAsync(ReferenceNumber referenceNumber)
        {
            await _agora.DeleteMessageAsync(ShowroomId.Value, referenceNumber.Value);
            
            return;
        }
        
        public async ValueTask<ReferenceNumber> LogListingCreatedAsync(Listing productListing)
        {            
            var owner = productListing.Owner.ReferenceNumber.Value;
            var createdBy =  await _agora.GetOrFetchMemberAsync(EmporiumId.Value, productListing.User.ReferenceNumber.Value);
            
            var embed = new LocalEmbed().WithTitle($"{productListing} Created")
                                        .AddField(productListing.Product.Title.ToString(), productListing.ValueTag.ToString())                
                                        .AddInlineField("Scheduled Start", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledStart))
                                        .AddInlineField("Scheduled End", Markdown.Timestamp(productListing.ScheduledPeriod.ScheduledEnd))
                                        .AddField("Owner", productListing.Anonymous ? Markdown.Italics("Anonymous") : Mention.User(owner))
                                        .WithFooter($"Created by {createdBy}");
            
            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));
            
            return ReferenceNumber.Create(message.Id);
        }

        public ValueTask<ReferenceNumber> LogListingUpdatedAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ReferenceNumber> LogListingWithdrawnAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ReferenceNumber> LogOfferSubmittedAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ReferenceNumber> LogOfferRevokedAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }
        
        public ValueTask<ReferenceNumber> LogOfferAcceptedAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ReferenceNumber> LogListingSoldAsync(Listing productListing)
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ReferenceNumber> LogListingExpiredAsync(Listing productListing)
        {
            var owner = productListing.Owner.ReferenceNumber.Value;
            var duration = productListing.ExpirationDate.AddSeconds(1) - productListing.ScheduledPeriod.ScheduledStart;
            var embed = new LocalEmbed().WithTitle($"{productListing} Expired")
                                        .AddField(productListing.Product.Title.ToString(), productListing.ValueTag.ToString())
                                        .AddInlineField("Owner", Mention.User(owner)).AddInlineField("Duration",duration.Humanize())
                                        .WithFooter($"Closed by {_agora.CurrentUser}");

            var message = await _agora.SendMessageAsync(ShowroomId.Value, new LocalMessage().AddEmbed(embed));

            return ReferenceNumber.Create(message.Id);
        }
    }
}