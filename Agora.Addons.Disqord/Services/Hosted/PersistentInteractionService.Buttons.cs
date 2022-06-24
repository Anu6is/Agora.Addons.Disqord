using Disqord;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using MediatR;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService
    {
        private readonly Dictionary<string, Func<IComponentInteraction, LocalInteractionModalResponse>> _modalRedirect = new()
        {
            { "extendAuction", ExtendListingModal },
            { "extendMarket", ExtendListingModal },
            { "editAuction", EditAuctionListingModal },
            { "editMarket", EditMarketListingModal }
        };

        private static IBaseRequest HandleInteraction(IComponentInteraction interaction) => interaction.CustomId switch
        {
            "buy" => new ClaimListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), "Market"),
            "undobid" => new UndoBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id)),
            "minbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMinimum = true },
            "maxbid" => new CreateBidCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), 0) { UseMaximum = true },
            { } when interaction.CustomId.StartsWith("accept") => new AcceptListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("accept", "")),
            { } when interaction.CustomId.StartsWith("withdraw") => new WithdrawListingCommand(new EmporiumId(interaction.GuildId.Value), new ShowroomId(interaction.ChannelId), ReferenceNumber.Create(interaction.Message.Id), interaction.CustomId.Replace("withdraw", "")),
            _ => null
        };

        private static Task HandleResponse(IComponentInteraction interaction) => interaction.CustomId switch
        {
            { } when interaction.CustomId.StartsWith("withdraw") => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Listing successfully withdrawn!").WithIsEphemeral(true)),
            "buy" => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral(true)),
            _ => Task.CompletedTask
        };

        private static LocalInteractionModalResponse ExtendListingModal(IComponentInteraction interaction)
        {
            return new LocalInteractionModalResponse().WithCustomId($"{interaction.CustomId}:{interaction.Message.Id}")
                .WithTitle($"Extend Expiration")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.Selection("option")
                            .WithMinimumSelectedOptions(1)
                            .WithMaximumSelectedOptions(1)
                            .WithPlaceholder("Select an extension option")
                            .WithOptions(
                                new LocalSelectionComponentOption("Extend By", "duration").WithDescription("Extend the product listing by a specified duration."),
                                new LocalSelectionComponentOption("Extend To", "datetime").WithDescription("Extend the product listing to a specified date and time."))
                            ),
                    LocalComponent.Row(
                        LocalComponent.TextInput("extension", "Input Extension", TextInputComponentStyle.Short)
                            .WithMinimumInputLength(2)
                            .WithMaximumInputLength(16)
                            .WithIsRequired()));
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
                    LocalComponent.Row(LocalComponent.TextInput("image", "Update Image", TextInputComponentStyle.Short).WithPlaceholder("Insert image url").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("description", "Update Description", TextInputComponentStyle.Paragraph).WithPlaceholder("Item description").WithMaximumInputLength(500).WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Update Buyer's Note", TextInputComponentStyle.Paragraph).WithPlaceholder("Hidden message").WithMaximumInputLength(250).WithIsRequired(false)));
                    //LocalComponent.Row(LocalComponent.Selection("discount", 
                    //    new LocalSelectionComponentOption("Percentage", "percent").WithDescription("Apply a percentage discount from 1 to 100"), 
                    //    new LocalSelectionComponentOption("Fixed Amount", "fixed").WithDescription("Apply a fixed amount discount from 1 to item value"))
                    //.WithMinimumSelectedOptions(0)
                    //.WithMaximumSelectedOptions(1)
                    //.WithPlaceholder("Select the type of discount to apply")),
                    //LocalComponent.Row(LocalComponent.TextInput("discountValue", "Discount Value", TextInputComponentStyle.Short).WithPlaceholder("0").WithIsRequired(false)));
        }
    }
}
