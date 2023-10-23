using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using HumanTimeParser.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Commands
{
    public class AuctionTemplateListView : PagedViewBase
    {
        private IServiceProvider Provider { get; set; }
        private EmporiumTimeParser TimeParser { get; set; }
        private AuctionTemplate CurrentTemplate { get; set; }

        private List<AuctionTemplate> AuctionTemplates { get; set; }

        public AuctionTemplateListView(IEnumerable<AuctionTemplate> templates, IServiceProvider provider)
            : base(new ListPageProvider(templates.Select(template => new Page().AddEmbed(template.CreateEmbed()))))
        {
            Provider = provider;
            AuctionTemplates = templates.ToList();
            CurrentTemplate = AuctionTemplates[CurrentPageIndex];
            TimeParser = provider.GetRequiredService<EmporiumTimeParser>();
        }

        [Button(Label = "Delete", Style = LocalButtonComponentStyle.Danger, Row = 0)]
        public ValueTask Delete(ButtonEventArgs e)
        {
            return default;
        }

        [Button(Label = "Edit Item", Style = LocalButtonComponentStyle.Primary, Row = 0)]
        public async ValueTask EditItem(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Edit Item Details")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("title", "Title", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(CurrentTemplate.Title ?? "Enter Title")
                                                     .WithMaximumInputLength(75)
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("desc", "Description", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(CurrentTemplate.Description ?? "Enter Description")
                                                     .WithMaximumInputLength(500)
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("price", "Starting Price", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(CurrentTemplate.StartingPrice.ToString())
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("image", "Image URL", TextInputComponentStyle.Short)
                                                     .WithPlaceholder("Enter Image URL")
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("quantity", "Quantity", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(CurrentTemplate.Quantity.ToString())
                                                     .WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>().ToArray();
            var title = rows[0].Components.OfType<ITextInputComponent>().First().Value;
            var desc = rows[1].Components.OfType<ITextInputComponent>().First().Value;
            var price = rows[2].Components.OfType<ITextInputComponent>().First().Value;
            var image = rows[3].Components.OfType<ITextInputComponent>().First().Value;
            var quantity = rows[4].Components.OfType<ITextInputComponent>().First().Value;

            CurrentTemplate.Title = title ?? CurrentTemplate.Title;
            CurrentTemplate.Description = desc ?? CurrentTemplate.Description;
            CurrentTemplate.Image = image ?? CurrentTemplate.Image;

            if (price.IsNotNull() && double.TryParse(price, out var startingPrice)) CurrentTemplate.StartingPrice = startingPrice;
            if (quantity.IsNotNull() && int.TryParse(quantity, out var stock)) CurrentTemplate.Quantity = stock;

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Edit Listing", Style = LocalButtonComponentStyle.Primary, Row = 0)]
        public async ValueTask EditListing(ButtonEventArgs e)
        {
            var value = CurrentTemplate.Type switch
            {
                "Standard" => "Buy Now Price",
                "Sealed" => "Max Participants",
                "Live" => "Timeout",
                _ => null
            };

            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Edit Listing Details")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("duration", "Duration", TextInputComponentStyle.Short).WithPlaceholder("example: 3h").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("min", "Min Bid Increase", TextInputComponentStyle.Short).WithPlaceholder("example: 100").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("max", "Max Bid Increase", TextInputComponentStyle.Short).WithPlaceholder("example: 500").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("reserve", "Reserve Price", TextInputComponentStyle.Short).WithPlaceholder("example: 250").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("unique", value, TextInputComponentStyle.Short).WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>().ToArray();
            var duration = rows[0].Components.OfType<ITextInputComponent>().First().Value;
            var min = rows[1].Components.OfType<ITextInputComponent>().First().Value;
            var max = rows[2].Components.OfType<ITextInputComponent>().First().Value;
            var reserve = rows[3].Components.OfType<ITextInputComponent>().First().Value;
            var unique = rows[4].Components.OfType<ITextInputComponent>().First().Value;

            if (duration.IsNotNull() && (TimeParser.Parse(duration) is ISuccessfulTimeParsingResult<DateTime> successfulResult))
                CurrentTemplate.Duration = (successfulResult.Value - DateTime.UtcNow).Add(TimeSpan.FromSeconds(1));

            if (min.IsNotNull() && double.TryParse(min, out var minIncrease)) CurrentTemplate.MinBidIncrease = minIncrease;
            if (max.IsNotNull() && double.TryParse(max, out var maxIncrease)) CurrentTemplate.MaxBidIncrease = maxIncrease;
            if (reserve.IsNotNull() && double.TryParse(reserve, out var reservePrice)) CurrentTemplate.ReservePrice = reservePrice;

            switch (CurrentTemplate.Type)
            {
                case "Standard":
                    if (unique.IsNotNull() && double.TryParse(unique, out var buyNowPrice)) CurrentTemplate.BuyNowPrice = buyNowPrice;
                    break;
                case "Sealed":
                    if (unique.IsNotNull() && int.TryParse(unique, out var maxParticipants)) CurrentTemplate.MaxParticipants = maxParticipants;
                    break;
                case "Live":
                    if (unique.IsNotNull() && (TimeParser.Parse(unique) is ISuccessfulTimeParsingResult<DateTime> timeout))
                        CurrentTemplate.Timeout = (timeout.Value - DateTime.UtcNow).Add(TimeSpan.FromSeconds(1));
                    break;
                default:
                    break;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Edit Details", Style = LocalButtonComponentStyle.Primary, Row = 0)]
        public async ValueTask EditDetails(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Edit Additional Details")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("anonymous", "Hide Owner", TextInputComponentStyle.Short).WithPlaceholder("True | False").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("reschedule", "Reschedule", TextInputComponentStyle.Short).WithPlaceholder("Never | Sold | Expired | Always").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("message", "Hidden Message", TextInputComponentStyle.Paragraph).WithPlaceholder("Add note").WithMaximumInputLength(250).WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>().ToArray();
            var anonymous = rows[0].Components.OfType<ITextInputComponent>().First().Value;
            var reschedule = rows[1].Components.OfType<ITextInputComponent>().First().Value;
            var note = rows[2].Components.OfType<ITextInputComponent>().First().Value;

            if (reschedule.IsNotNull() && Enum.TryParse<RescheduleOption>(reschedule, true, out var rescheduleResult)) CurrentTemplate.Reschedule = rescheduleResult;
            if (anonymous.IsNotNull() && bool.TryParse(anonymous, out var anonymousResult)) CurrentTemplate.Anonymous = anonymousResult;

            CurrentTemplate.Message = note ?? CurrentTemplate.Message;

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Back", Style = LocalButtonComponentStyle.Primary, Row = 1)]
        public ValueTask Previous(ButtonEventArgs e)
        {
            if (CurrentPageIndex == 0)
                CurrentPageIndex = PageProvider.PageCount - 1;
            else
                CurrentPageIndex--;

            CurrentTemplate = AuctionTemplates[CurrentPageIndex];
            
            return default;
        }

        [Button(Label = "Preview", Style = LocalButtonComponentStyle.Success, Row = 1)]
        public ValueTask Preview(ButtonEventArgs e)
        {
            return default;
        }

        [Button(Label = "Next", Style = LocalButtonComponentStyle.Primary, Row = 1)]
        public ValueTask NextTip(ButtonEventArgs e)
        {
            if (CurrentPageIndex + 1 == PageProvider.PageCount)
                CurrentPageIndex = 0;
            else
                CurrentPageIndex++;

            CurrentTemplate = AuctionTemplates[CurrentPageIndex];
            
            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Row = 1)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        private void UpdateMessage()
        {
            MessageTemplate = message => message.WithContent($"{(CurrentTemplate.ReverseBidding ? "Reverse" : string.Empty)} {CurrentTemplate.Type} Auction")
                                                .AddEmbed(CurrentTemplate.CreateEmbed().WithAuthor(CurrentTemplate.Name).WithDefaultColor());

            ReportChanges();
        }
    }
}
