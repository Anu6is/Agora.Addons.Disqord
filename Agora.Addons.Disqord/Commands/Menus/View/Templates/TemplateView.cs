using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest;
using Emporia.Domain.Extension;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Humanizer;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Qommon;

namespace Agora.Addons.Disqord.Commands
{
    public abstract class TemplateView<T> : PagedViewBase where T : ITemplate
    {
        private string _selectedCategory = string.Empty;
        private string _selectedSubcategory = string.Empty;

        protected T CurrentTemplate { get; set; }
        protected IServiceProvider Provider { get; set; }

        protected CachedEmporium Emporium { get; set; }
        protected EmporiumTimeParser TimeParser { get; set; }
        private SelectionViewComponent CategorySelection { get; set; }
        private SelectionViewComponent SubcategorySelection { get; set; }

        public TemplateView(IEnumerable<T> templates, CachedEmporium emporium, IServiceProvider provider)
            : base(new ListPageProvider(templates.Select(template => new Page().AddEmbed(template.CreateEmbed()))))
        {
            Emporium = emporium;
            Provider = provider;
            CurrentTemplate = templates.First();

            TimeParser = provider.GetRequiredService<EmporiumTimeParser>();

            CategorySelection = new SelectionViewComponent(SelectCategory) { MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Placeholder = "Select a default Category", Row = 1 };
            SubcategorySelection = new SelectionViewComponent(SelectSubcategory) { MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Placeholder = "Select a default Subcategory", Row = 2 };

            AddCategorySelection();
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 0, Row = 0, Placeholder = "Change the default owner", Type = SelectionComponentType.User)]
        public async ValueTask SelectOwner(SelectionEventArgs e)
        {
            if (e.SelectedEntities.Count == 0)
            {
                CurrentTemplate.Owner = 0;
            }
            else
            {
                CurrentTemplate.Owner = e.SelectedEntities[0].Id;
            }

            await UpdateMessageAsync();
        }

        protected virtual void AddCategorySelection()
        {

            if (Emporium.Categories.Count == 0)
                CategorySelection.Options.Add(new LocalSelectionComponentOption("No Categories Exist", "0"));

            for (int i = 0; i < Emporium.Categories.Count; i++)
            {
                var category = Emporium.Categories[i];
                var option = new LocalSelectionComponentOption(category.Title.ToString(), $"{i + 1}");

                CategorySelection.Options.Add(option);
            }

            CategorySelection.IsDisabled = Emporium.Categories.Count == 0;

            AddComponent(CategorySelection);
        }

        public async ValueTask SelectCategory(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count == 1)
            {
                if (e.SelectedOptions[0].Value == "0") return;

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                _selectedCategory = e.SelectedOptions[0].Label.Value;

                var subcategories = Emporium.Categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories.Where(x => !x.Title.Equals(_selectedCategory)).Count();

                if (subcategories > 0) AddSubcategorySelection();

                CurrentTemplate.Category = _selectedCategory;

                await UpdateMessageAsync();
            }
            else
            {
                _selectedCategory = string.Empty;
                _selectedSubcategory = string.Empty;

                CurrentTemplate.Category = _selectedCategory;
                CurrentTemplate.Subcategory = _selectedSubcategory;

                e.Selection.Options.First(x => x.IsDefault.GetValueOrDefault()).IsDefault = false;

                RemoveComponent(SubcategorySelection);

                await UpdateMessageAsync();
            }
        }

        protected virtual void AddSubcategorySelection()
        {
            _selectedSubcategory = string.Empty;
            CurrentTemplate.Subcategory = string.Empty;

            SubcategorySelection.Options.Clear();

            RemoveComponent(SubcategorySelection);

            var subcategories = Emporium.Categories.First(x => x.Title.Equals(_selectedCategory))
                                        .SubCategories.Where(x => !x.Title.Equals(_selectedCategory))
                                        .ToArray();

            if (subcategories.Length == 0)
                SubcategorySelection.Options.Add(new LocalSelectionComponentOption("No subcategories Exist", "0"));

            for (int i = 0; i < subcategories.Length; i++)
            {
                Emporia.Domain.Entities.Subcategory subcategory = subcategories[i];

                var option = new LocalSelectionComponentOption(subcategory.Title.ToString(), $"{i + 1}");

                SubcategorySelection.Options.Add(option);
            }

            SubcategorySelection.IsDisabled = subcategories.Length == 0;

            AddComponent(SubcategorySelection);
        }

        public async ValueTask SelectSubcategory(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count == 1)
            {
                if (e.SelectedOptions[0].Value == "0") return;

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                _selectedSubcategory = e.SelectedOptions[0].Label.Value;

                CurrentTemplate.Subcategory = _selectedSubcategory;

                await UpdateMessageAsync();
            }
            else
            {
                _selectedSubcategory = string.Empty;

                CurrentTemplate.Subcategory = _selectedSubcategory;

                e.Selection.Options.First(x => x.IsDefault.GetValueOrDefault()).IsDefault = false;

                await UpdateMessageAsync();
            }
        }

        [Button(Label = "Edit Item", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 3)]
        public abstract ValueTask EditItem(ButtonEventArgs e);

        [Button(Label = "Edit Listing", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 3)]
        public abstract ValueTask EditListing(ButtonEventArgs e);

        [Button(Label = "Edit Details", Style = LocalButtonComponentStyle.Primary, Position = 3, Row = 3)]
        public abstract ValueTask EditDetails(ButtonEventArgs e);

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Position = 4, Row = 3)]
        public virtual async ValueTask SaveChanges(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse()
                                        .WithCustomId(e.Interaction.Message.Id.ToString())
                                        .WithTitle("Save Template")
                                        .WithComponents(LocalComponent.Row(LocalComponent.TextInput("name", "Template Name", TextInputComponentStyle.Short)
                                                                                         .WithPlaceholder(CurrentTemplate.Quantity.ToString())
                                                                                         .WithPrefilledValue(CurrentTemplate.Name.IsNull() ? string.Empty : CurrentTemplate.Name)
                                                                                         .WithMaximumInputLength(100)
                                                                                         .WithIsRequired(true)));

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

            CurrentTemplate.Name = name.IsNotNull() ? name : CurrentTemplate.Name;

            if (CurrentTemplate.Name.IsNull())
            {
                var embed = new LocalEmbed().WithDescription("Template Name Required").WithDefaultColor();
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().AddEmbed(embed));

                return;
            }

            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(SaveCommand());

            if (result.IsSuccessful)
            {
                var embed = new LocalEmbed().WithColor(Color.Teal).WithDescription($"Template saved as {Markdown.Bold(name)}");

                await modal.Response().ModifyMessageAsync(new LocalInteractionMessageResponse().WithContent(string.Empty).AddEmbed(embed).WithComponents());
            }
            else
            {
                var embed = new LocalEmbed().WithColor(Color.Red).WithDescription(result.FailureReason);

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().AddEmbed(embed));

                if (result is IExceptionResult exResult)
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, exResult.RaisedException);
            }
        }


        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 0, Row = 4)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected virtual async ValueTask UpdateMessageAsync()
        {
            var page = await PageProvider.GetPageAsync(this);

            page.WithEmbeds(CurrentTemplate.CreateEmbed());

            ReportPageChanges();

            await UpdateAsync();
        }

        public override async ValueTask UpdateAsync()
        {
            var count = PageProvider.PageCount;

            if (count > 1)
            {
                var page = await PageProvider.GetPageAsync(this);

                page.WithContent($"### [{CurrentPageIndex + 1}/{PageProvider.PageCount}]");
            }

            await base.UpdateAsync();
        }

        protected abstract IRequest<IResult<T>> SaveCommand();

        protected virtual LocalEmbed PreviewEmbed(string listing)
        {

            var prefix = CurrentTemplate is AuctionTemplate auction && auction.ReverseBidding ? "Reverse " : string.Empty;

            return ProductDetails(new LocalEmbed
            {
                Title = $"{prefix}{listing}: {CurrentTemplate.Title}",
                Author = UniqueTrait(),
                Description = CurrentTemplate.Description,
                Url = CurrentTemplate.Image,
                ImageUrl = CurrentTemplate.Image,
                Footer = new LocalEmbedFooter().WithText($"Reference Code: 000")
                                               .WithIconUrl(ProductExtensions.GetScheduleEmojiUrl(CurrentTemplate.Reschedule))
            }.WithDefaultColor());
        }

        private LocalEmbedAuthor UniqueTrait() => CurrentTemplate switch
        {
            AuctionTemplate { Type: "Standard" } auction => auction.BuyNowPrice == 0 ? null : new LocalEmbedAuthor().WithName($"Instant Purchase Price: {auction.BuyNowPrice.ToString($"N{auction.Currency.DecimalDigits}")}"),
            AuctionTemplate { Type: "Sealed" } auction => auction.MaxParticipants == 0 ? null : new LocalEmbedAuthor().WithName($"Max Participants: {auction.MaxParticipants}"),
            AuctionTemplate { Type: "Live" } auction => auction.Timeout == TimeSpan.Zero ? null : new LocalEmbedAuthor().WithName($"Bidding Timeout: {auction.Timeout.Add(TimeSpan.FromSeconds(1)).Humanize()}"),
            _ => null
        };

        private LocalEmbed ProductDetails(LocalEmbed embed) => CurrentTemplate switch
        {
            AuctionTemplate auction => embed.AddInlineField("Quantity", Math.Max(1, auction.Quantity).ToString())
                                            .AddInlineField("Starting Price", auction.StartingPrice == 0 ? Markdown.Italics("Undefined") : auction.StartingPrice.ToString($"N{auction.Currency.DecimalDigits}"))
                                            .AddInlineField("Current Bid", "No Bids")
                                            .AddInlineField("Scheduled Start", Markdown.Timestamp(Emporium.LocalTime.DateTime))
                                            .AddInlineField("Scheduled End", Markdown.Timestamp(Emporium.LocalTime.DateTime.Add(auction.Duration)))
                                            .AddInlineField("Expiration", Markdown.Timestamp(Emporium.LocalTime.DateTime.Add(auction.Duration), Markdown.TimestampFormat.RelativeTime))
                                            .AddInlineField("Item Owner", auction.Anonymous ? Markdown.BoldItalics("Anonymous") : auction.Owner == 0 ? Markdown.Italics("Undefined") : Mention.User(auction.Owner)),
            _ => null
        };

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
