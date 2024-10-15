using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;


namespace Agora.Addons.Disqord.Menus
{
    public sealed class WatchlistView : PagedViewBase
    {
        public WatchlistView(ReferenceNumber userReference, IEnumerable<Listing> listings)
            : base(new ListPageProvider(listings.Chunk(10).Select(x =>
                        new Page().WithEmbeds(new LocalEmbed()
                                  .WithDefaultColor()
                                  .WithTitle("Active Bids")
                                  .WithFields(x.Select(l =>
                                        new LocalEmbedField()
                                                .WithName(l.Product.Title.ToString())
                                                .WithValue($"{GetHighestBid(userReference, l)} | **{GetUserBid(userReference, l)}**{Environment.NewLine}{GetJumpLink(l)} Expires {GetExpiration(l)}")))))))
        {
            if (listings.Count() < 10)
                foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
                    RemoveComponent(button);
        }

        [Button(Label = "Continue")]
        public ValueTask Continue(ButtonEventArgs e)
        {
            if (CurrentPageIndex + 1 == PageProvider.PageCount)
                CurrentPageIndex = 0;
            else
                CurrentPageIndex++;

            return default;
        }

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        private static string GetJumpLink(Listing listing)
        {
            var channelReference = listing.ReferenceCode.Reference();
            var channelId = channelReference == 0 ? listing.ShowroomId.Value : channelReference;
            var link = Discord.MessageJumpLink(listing.Owner.EmporiumId.Value, channelId, listing.Product.ReferenceNumber.Value);

            return link;
        }

        private static string GetUserBid(ReferenceNumber userReference, Listing listing)
            => $"Your Bid: {(listing.Product as AuctionItem).Offers.OrderByDescending(o => o.SubmittedOn).First(b => b.UserReference.Equals(userReference)).Submission.Value}";

        private static string GetHighestBid(ReferenceNumber userReference, Listing listing)
            => listing is VickreyAuction
                    ? "Current Value: **Sealed**"
                    : $"{HasHighest(userReference, listing)} Current Value: {listing.CurrentOffer.Submission.Value}";

        private static LocalCustomEmoji HasHighest(ReferenceNumber userReference, Listing listing)
            => listing.CurrentOffer.UserReference.Equals(userReference) ? AgoraEmoji.GreenCheckMark : AgoraEmoji.RedCrossMark;

        private static string GetExpiration(Listing listing)
            => Markdown.Timestamp(listing.ExpiresAt(), Markdown.TimestampFormat.RelativeTime);
    }
}
