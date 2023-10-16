using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Humanizer;
using HumanTimeParser.Core.Parsing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Qommon;

namespace Agora.Addons.Disqord.Commands
{
    public class AuctionTemplateView : ViewBase
    {
        private string _selectedCategory = string.Empty;
        private string _selectedSubcategory = string.Empty;

        private IServiceProvider Provider { get; set; }

        private CachedEmporium Emporium { get; set; }
        private AuctionTemplate AuctionTemplate { get; set; }
        private EmporiumTimeParser TimeParser { get; set; }
        private SelectionViewComponent CategorySelection { get; set; }
        private SelectionViewComponent SubcategorySelection { get; set; }

        public AuctionTemplateView(CachedEmporium emporium, AuctionTemplate template, IServiceProvider provider) : base(
            message => message.WithContent($"{(template.ReverseBidding ? "Reverse" : string.Empty)} {template.Type} Auction")
                              .AddEmbed(CreateEmbed(template).WithDefaultColor()))
        {
            Provider = provider;
            Emporium = emporium;
            AuctionTemplate = template;
            TimeParser = provider.GetRequiredService<EmporiumTimeParser>();

            CategorySelection = new SelectionViewComponent(SelectCategory) { MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Placeholder = "Select a default Category", Row = 1 };
            SubcategorySelection = new SelectionViewComponent(SelectSubcategory) { MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Placeholder = "Select a default Subcategory", Row = 2 };

            AddCategorySelection();
        }

        private static LocalEmbed CreateEmbed(AuctionTemplate template)
        {
            var field = template.Type switch
            {
                "Standard" => new LocalEmbedField().WithName("Buy Now Price").WithValue(Money.Create((decimal)template.BuyNowPrice, template.Currency)).WithIsInline(),
                "Sealed" => new LocalEmbedField().WithName("Max Participants").WithValue(template.MaxParticipants == 0 ? "Unlimited" : template.MaxParticipants).WithIsInline(),
                "Live" => new LocalEmbedField().WithName("Timeout").WithValue(template.Timeout.Humanize(precision:2, minUnit:Humanizer.Localisation.TimeUnit.Second)).WithIsInline(), 
                _ => null
            };

            return new LocalEmbed()
                .WithTitle($"Title: {template.Title ?? ""}")
                .WithDescription(template.Description ?? Markdown.CodeBlock(" "))
                .AddInlineField("Quantity", template.Quantity == 0 ? 1 : template.Quantity)
                .AddInlineField("Starting Price", Money.Create((decimal)template.StartingPrice, template.Currency))
                .AddInlineField("Reserved Price", Money.Create((decimal)template.ReservePrice, template.Currency))
                .AddInlineField("Duration", template.Duration.Humanize(precision:2, minUnit:Humanizer.Localisation.TimeUnit.Second))
                .AddInlineField("Reschedule", template.Reschedule.ToString())
                .AddInlineField(field)
                .AddInlineField("Min Bid Increase", template.MinBidIncrease)
                .AddInlineField("Max Bid Increase", template.MaxBidIncrease)
                .AddInlineBlankField()
                .AddInlineField("Category", template.Category ?? Markdown.CodeBlock(" "))
                .AddInlineField("Subcategory", template.Subcategory ?? Markdown.CodeBlock(" "))
                .AddInlineBlankField()
                .AddField($"Owner {(template.Anonymous ? "[hidden]" : "[visible]")}", template.Owner == 0 ? Markdown.CodeBlock(" ") : Mention.User(template.Owner))
                .WithImageUrl(template.Image)
                .WithFooter(template.Message);
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Row = 0, Placeholder = "Change the default owner", Type = SelectionComponentType.User)]
        public ValueTask SelectOwner(SelectionEventArgs e)
        {
            if (e.SelectedEntities.Count == 0) AuctionTemplate.Owner = 0;
            else AuctionTemplate.Owner = e.SelectedEntities[0].Id;

            UpdateMessage();

            return default;
        }

        private void AddCategorySelection()
        {

            if (Emporium.Categories.Count == 0)
                CategorySelection.Options.Add(new LocalSelectionComponentOption("No Categories Exist", "0"));

            var count = 0;
            foreach (var category in Emporium.Categories)
            {
                count++;
                var option = new LocalSelectionComponentOption(category.Title.ToString(), count.ToString());
                CategorySelection.Options.Add(option);
            }

            CategorySelection.IsDisabled = Emporium.Categories.Count == 0;

            AddComponent(CategorySelection);
        }
        
        public ValueTask SelectCategory(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count == 1)
            {
                if (e.SelectedOptions[0].Value == "0") return default;
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                _selectedCategory = e.SelectedOptions[0].Label.Value;

                var subcategories = Emporium.Categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories.Where(x => !x.Title.Equals(_selectedCategory)).Count();

                if (subcategories > 0) AddSubcategorySelection();

                AuctionTemplate.Category = _selectedCategory;

                UpdateMessage();
            }
            else
            {
                _selectedCategory = string.Empty;
                _selectedSubcategory = string.Empty;

                AuctionTemplate.Category = _selectedCategory;
                AuctionTemplate.Subcategory = _selectedSubcategory;

                e.Selection.Options.First(x => x.IsDefault.GetValueOrDefault()).IsDefault = false;

                RemoveComponent(SubcategorySelection);

                UpdateMessage();
            }

            return default;
        }

        private void AddSubcategorySelection()
        {
            _selectedSubcategory = string.Empty;
            SubcategorySelection.Options.Clear();
            RemoveComponent(SubcategorySelection);
            AuctionTemplate.Subcategory = _selectedSubcategory;

            var subcategories = Emporium.Categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories.Where(x => !x.Title.Equals(_selectedCategory)).ToArray();

            if (subcategories.Length == 0)
                SubcategorySelection.Options.Add(new LocalSelectionComponentOption("No subcategories Exist", "0"));

            var count = 0;
            foreach (var subcategory in subcategories)
            {
                count++;
                var option = new LocalSelectionComponentOption(subcategory.Title.ToString(), count.ToString());
                SubcategorySelection.Options.Add(option);
            }

            SubcategorySelection.IsDisabled = subcategories.Length == 0;

            AddComponent(SubcategorySelection);
        }

        public ValueTask SelectSubcategory(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count == 1)
            {
                if (e.SelectedOptions[0].Value == "0") return default;
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                _selectedSubcategory = e.SelectedOptions[0].Label.Value;

                AuctionTemplate.Subcategory = _selectedSubcategory;

                UpdateMessage();
            }
            else
            {
                _selectedSubcategory = string.Empty;

                AuctionTemplate.Subcategory = _selectedSubcategory;

                e.Selection.Options.First(x => x.IsDefault.GetValueOrDefault()).IsDefault = false;

                UpdateMessage();
            }

            return default;
        }

        [Button(Label = "Edit Item", Style = LocalButtonComponentStyle.Primary, Row = 3)]
        public async ValueTask EditItem(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Edit Item Details")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("title", "Title", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(AuctionTemplate.Title ?? "Enter Title")
                                                     .WithMaximumInputLength(75)
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("desc", "Description", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(AuctionTemplate.Description ?? "Enter Description")
                                                     .WithMaximumInputLength(500)
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("price", "Starting Price", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(AuctionTemplate.StartingPrice.ToString())
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("image", "Image URL", TextInputComponentStyle.Short)
                                                     .WithPlaceholder("Enter Image URL")
                                                     .WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("quantity", "Quantity", TextInputComponentStyle.Short)
                                                     .WithPlaceholder(AuctionTemplate.Quantity.ToString())
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

            AuctionTemplate.Title = title ?? AuctionTemplate.Title;
            AuctionTemplate.Description = desc ?? AuctionTemplate.Description;
            AuctionTemplate.Image = image ?? AuctionTemplate.Image;

            if (price.IsNotNull() && double.TryParse(price, out var startingPrice)) AuctionTemplate.StartingPrice = startingPrice;
            if (quantity.IsNotNull() && int.TryParse(quantity, out var stock)) AuctionTemplate.Quantity = stock;

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Edit Listing", Style = LocalButtonComponentStyle.Primary, Row = 3)]
        public async ValueTask EditListing(ButtonEventArgs e)
        {
            var value = AuctionTemplate.Type switch
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
                    AuctionTemplate.Duration = (successfulResult.Value - DateTime.UtcNow).Add(TimeSpan.FromSeconds(1));

            if (min.IsNotNull() && double.TryParse(min, out var minIncrease)) AuctionTemplate.MinBidIncrease = minIncrease;
            if (max.IsNotNull() && double.TryParse(max, out var maxIncrease)) AuctionTemplate.MaxBidIncrease = maxIncrease;
            if (reserve.IsNotNull() && double.TryParse(reserve, out var reservePrice)) AuctionTemplate.ReservePrice = reservePrice;

            switch (AuctionTemplate.Type)
            {
                case "Standard":
                    if (unique.IsNotNull() && double.TryParse(unique, out var buyNowPrice)) AuctionTemplate.BuyNowPrice = buyNowPrice;
                    break;
                case "Sealed":
                    if (unique.IsNotNull() && int.TryParse(unique, out var maxParticipants)) AuctionTemplate.MaxParticipants = maxParticipants;
                    break;
                case "Live":
                    if (unique.IsNotNull() && (TimeParser.Parse(unique) is ISuccessfulTimeParsingResult<DateTime> timeout))
                        AuctionTemplate.Timeout = (timeout.Value - DateTime.UtcNow).Add(TimeSpan.FromSeconds(1));
                    break;
                default:
                    break;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Edit Details", Style = LocalButtonComponentStyle.Primary, Row = 3)]
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

            if (reschedule.IsNotNull() && Enum.TryParse<RescheduleOption>(reschedule, true, out var rescheduleResult)) AuctionTemplate.Reschedule = rescheduleResult;
            if (anonymous.IsNotNull() && bool.TryParse(anonymous, out var anonymousResult)) AuctionTemplate.Anonymous = anonymousResult;

            AuctionTemplate.Message = note ?? AuctionTemplate.Message;

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Edits applied successfully")));

            UpdateMessage();
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 4)]
        public async ValueTask SaveChanges(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Save Template")
                .WithComponents(LocalComponent.Row(LocalComponent.TextInput("name", "Template Name", TextInputComponentStyle.Short)
                                         .WithPlaceholder(AuctionTemplate.Quantity.ToString())
                                         .WithMaximumInputLength(100)
                                         .WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var name = modal.Components.OfType<IRowComponent>().ToArray()[0]
                            .Components.OfType<ITextInputComponent>().First().Value;

            AuctionTemplate.Name = name;

            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new CreateAuctionTemplateCommand(AuctionTemplate));

            if (result.IsSuccessful)
            {
                var embed = new LocalEmbed().WithColor(Color.Teal).WithDescription($"Template saved as {Markdown.Bold(name)}");

                await modal.Response().ModifyMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().AddEmbed(embed).WithComponents());
            }
            else
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(result.FailureReason).WithIsEphemeral());

                if (result is IExceptionResult exResult)
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, exResult.RaisedException);
            }
        }


        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        private void UpdateMessage()
        {
            MessageTemplate = message => message.WithContent($"{(AuctionTemplate.ReverseBidding ? "Reverse" : string.Empty)} {AuctionTemplate.Type} Auction")
                                                .AddEmbed(CreateEmbed(AuctionTemplate).WithAuthor(AuctionTemplate.Name).WithDefaultColor());

            ReportChanges();
        }
    }
}
