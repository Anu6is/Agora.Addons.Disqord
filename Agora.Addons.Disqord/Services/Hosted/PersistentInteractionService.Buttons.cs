using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using MediatR;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService
    {
        private readonly Dictionary<string, string> _confirmationRequired = new()
        {
            { "withdrawGiveaway", "Remove Giveaway Listing" },
            { "withdrawAuction", "Remove Auction Listing" },
            { "withdrawMarket", "Remove Market Listing" },
            { "withdrawTrade", "Remove Trade Listing" },
            { "acceptAuction", "Accept Current Bid" },
            { "acceptMarket", "Accept Current Offer"},
            { "instant", "Confirm This Purchase" },
            { "bundle", "Confirm This Purchase" },
            { "buy1", "Confirm This Purchase" },
            { "buy", "Confirm This Purchase" },
            { "sell", "Confirm This Sale" },
            { "trade", "Accept Trade Offer" },
        };

        private readonly Dictionary<string, Func<IComponentInteraction, LocalInteractionModalResponse>> _modalRedirect = new()
        {
            { "extendGiveaway", ExtendListingModal },
            { "extendAuction", ExtendListingModal },
            { "extendMarket", ExtendListingModal },
            { "extendTrade", ExtendListingModal },
            { "editGiveaway", EditGiveawayListingModal},
            { "editAuction", EditAuctionListingModal },
            { "editMarket", EditMarketListingModal },
            { "editTrade", EditTradeListingModal },
            { "bestOffer", SubmitMarketOfferModal },
            { "custombid", SubmitCustomBidModal },
            { "barter", SubmitTradeOfferModal },
            { "claim", PartialPurchaseModal }
        };

        private static IBaseRequest AuthorizeInteraction(IComponentInteraction interaction, ulong showroomId) => interaction.CustomId switch
        {
            "buy" => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            "buy1" => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true, ItemCount = 1},
            "sell" => new CreateDealCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            "join" => new CreateTicketCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            "trade" => new CreateDealCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            "barter" => new CreateDealCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            "bestOffer" => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true, Offer = 0 },
            "instant" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), 0) { AuthorizeOnly = true },
            "custombid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), 0) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("bundle") => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("extend") => new ExtendListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("extend", "")) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("accept") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("accept", "")) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("withdraw") => new WithdrawListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("withdraw", "")) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("editGiveaway") => new UpdateGiveawayItemCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("editAuction") => new UpdateAuctionItemCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("editMarket") => new UpdateMarketItemCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("editTrade") => new UpdateTradeItemCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("confirm") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("confirm", "")) { AuthorizeOnly = true },
            { } when interaction.CustomId.StartsWith("revert") => new RevertTransactionCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("revert", "")) { AuthorizeOnly = true },
            _ => null
        };

        private static IBaseRequest HandleInteraction(IComponentInteraction interaction, ulong showroomId) => interaction.CustomId switch
        {
            "buy" => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "buy1" => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { ItemCount = 1 },
            "sell" => new CreateDealCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "join" => new CreateTicketCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "trade" => new CreateDealCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "optout" => new CancelTicketCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "undobid" => new UndoBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)),
            "minbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMinimum = true },
            "maxbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMaximum = true },
            "instant" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMinimum = true, UseMaximum = true },
            { } when interaction.CustomId.StartsWith("bundle") => new CreatePaymentCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id)) { ItemCount = int.Parse(interaction.CustomId.Replace("bundle:", "")) },
            { } when interaction.CustomId.StartsWith("accept") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("accept", "")),
            { } when interaction.CustomId.StartsWith("confirm") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("confirm", "")),
            { } when interaction.CustomId.StartsWith("withdraw") => new WithdrawListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("withdraw", "")),
            { } when interaction.CustomId.StartsWith("revert") => new RevertTransactionCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(showroomId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("revert", "")),
            _ => null
        };

        private static Task HandleResponse(IComponentInteraction interaction) => interaction.CustomId switch
        {
            { } when interaction.CustomId.StartsWith("undobid")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Bid removed!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("optout")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Ticket returned!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("join")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Good Luck!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("revertMarket")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Offer removed!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("buy") || interaction.CustomId.StartsWith("bundle")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("minbid") || interaction.CustomId.StartsWith("maxbid")
                    => interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Bid successfully submitted!").WithIsEphemeral()),
            _ => Task.CompletedTask
        };

        private static LocalInteractionModalResponse ExtendListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle($"Extend Expiration")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.TextInput("extendTo", "Extend End To (date - yyyy-mm-dd 15:00)", TextInputComponentStyle.Short)
                            .WithMinimumInputLength(2)
                            .WithMaximumInputLength(16)
                            .WithIsRequired(false)),
                    LocalComponent.Row(
                        LocalComponent.TextInput("extendBy", "Extend End By (duration - 5d)", TextInputComponentStyle.Short)
                            .WithMinimumInputLength(2)
                            .WithMaximumInputLength(16)
                            .WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse EditAuctionListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Edit Auction Listing")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("image", "Update Image", TextInputComponentStyle.Short).WithPlaceholder("Insert image url").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("description", "Update Description", TextInputComponentStyle.Paragraph).WithPlaceholder("Item description").WithMaximumInputLength(500).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Update Buyer's Note", TextInputComponentStyle.Paragraph).WithPlaceholder("Hidden message").WithMaximumInputLength(250).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("minIncrease", "Update Minimum Increment", TextInputComponentStyle.Short).WithPlaceholder("0").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("maxIncrease", "Update Maximum Increment", TextInputComponentStyle.Short).WithPlaceholder("0").WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse EditMarketListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Edit Market Listing")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("title", "Update Title", TextInputComponentStyle.Short).WithPlaceholder("Item title").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("price", "Update Price", TextInputComponentStyle.Short).WithPlaceholder("Item price").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("image", "Update Image", TextInputComponentStyle.Short).WithPlaceholder("Insert image url").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("description", "Update Description", TextInputComponentStyle.Paragraph).WithPlaceholder("Item description").WithMaximumInputLength(500).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Update Buyer's Note", TextInputComponentStyle.Paragraph).WithPlaceholder("Hidden message").WithMaximumInputLength(250).WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse EditTradeListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Edit Trade Listing")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("image", "Update Image", TextInputComponentStyle.Short).WithPlaceholder("Insert image url").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("description", "Update Description", TextInputComponentStyle.Paragraph).WithPlaceholder("Item description").WithMaximumInputLength(500).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Update Buyer's Note", TextInputComponentStyle.Paragraph).WithPlaceholder("Hidden message").WithMaximumInputLength(250).WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse EditGiveawayListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Edit Giveaway Listing")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("title", "Update Title", TextInputComponentStyle.Short).WithPlaceholder("Item title").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("image", "Update Image", TextInputComponentStyle.Short).WithPlaceholder("Insert image url").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("description", "Update Description", TextInputComponentStyle.Paragraph).WithPlaceholder("Item description").WithMaximumInputLength(500).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Update Buyer's Note", TextInputComponentStyle.Paragraph).WithPlaceholder("Hidden message").WithMaximumInputLength(250).WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse SubmitMarketOfferModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Make an Offer")
                .WithComponents(LocalComponent.Row(LocalComponent.TextInput("amount", "Your best offer", TextInputComponentStyle.Short).WithPlaceholder("0").WithIsRequired()));
        }

        private static LocalInteractionModalResponse SubmitTradeOfferModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Submit Trade Offer")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("offer", "Trade Offer", TextInputComponentStyle.Short).WithPlaceholder("What are you offering?").WithMaximumInputLength(75).WithIsRequired(true)),
                    LocalComponent.Row(LocalComponent.TextInput("details", "Additional Details", TextInputComponentStyle.Paragraph).WithPlaceholder("Additional details (optional message for item owner)").WithMaximumInputLength(250).WithIsRequired(false)));
        }

        private static LocalInteractionModalResponse SubmitCustomBidModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Bid Amount")
                .WithComponents(LocalComponent.Row(LocalComponent.TextInput("amount", "Custom Bid Amount", TextInputComponentStyle.Short).WithPlaceholder("1").WithIsRequired()));
        }

        private static LocalInteractionModalResponse PartialPurchaseModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle("Purchase Items")
                .WithComponents(LocalComponent.Row(LocalComponent.TextInput("amount", "Amount to Claim", TextInputComponentStyle.Short).WithPlaceholder("0").WithIsRequired()));
        }
    }
}
