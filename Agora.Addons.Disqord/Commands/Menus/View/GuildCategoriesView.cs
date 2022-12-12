using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class GuildCategoriesView : ViewBase
    {
        private readonly List<Category> _categories = new();
        private readonly GuildSettingsContext _context;
        private string _selectedCategory = string.Empty;
        private string _selectedSubcategory = string.Empty;

        public GuildCategoriesView(List<Category> categories, GuildSettingsContext context)
            : base(message => message.AddEmbed(new LocalEmbed().WithDescription($"Number of registered categories: {Markdown.Bold(categories.Count)}").WithDefaultColor()))
        {
            _context = context;
            _categories = categories;

            AddCategoryComponents();
        }

        public async ValueTask AddCategory(ButtonEventArgs e)
        {
            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Create a Category")
                .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = e.Interaction.CustomId,
                    Label = "Enter Category Title",
                    Placeholder = "category title",
                    MaximumInputLength = 25,
                    IsRequired = true
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var modalInput = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                _categories.Add(await mediator.Send(new CreateCategoryCommand(new EmporiumId(_context.Guild.Id), CategoryTitle.Create(modalInput))));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"Category {Markdown.Bold(modalInput)} Successfully Added!"));

            MessageTemplate = message => message.AddEmbed(new LocalEmbed().WithDescription($"Number of registered categories: {Markdown.Bold(_categories.Count)}").WithDefaultColor());

            AddCategoryComponents();
            ReportChanges();

            return;
        }

        public async ValueTask AddSubcategory(ButtonEventArgs e)
        {
            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle($"{_selectedCategory} Subcategory")
                .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = e.Interaction.CustomId,
                    Label = "Enter Subcategory Title",
                    Placeholder = $"{_selectedCategory.ToLower()} subcategory",
                    MaximumInputLength = 25,
                    IsRequired = true
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var modalInput = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var subcategories = _categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories;

            try
            {
                subcategories.Add(await mediator.Send(new CreateSubcategoryCommand(new EmporiumId(_context.Guild.Id), CategoryTitle.Create(_selectedCategory), SubcategoryTitle.Create(modalInput))));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"{Markdown.Bold(modalInput)} Successfully Added to {_selectedCategory}"));

            MessageTemplate = message =>
            {
                message.AddEmbed(new LocalEmbed().WithDescription($"Subcategories registerd under {_selectedCategory}: {Markdown.Bold(subcategories.Skip(1).Count())}").WithDefaultColor());
            };

            AddSubcategoryComponents();
            ReportChanges();

            return;
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

                AddSubcategoryComponents();

                var subcategories = _categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories.Skip(1).Count();

                MessageTemplate = message =>
                {
                    message.AddEmbed(new LocalEmbed().WithDescription($"Subcategories registerd under {_selectedCategory}: {Markdown.Bold(subcategories)}").WithDefaultColor());
                };

                ReportChanges();
            }
            else
            {
                _selectedCategory = string.Empty;
            }

            return default;
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

                EnumerateComponents().OfType<ButtonViewComponent>().First(x => x.Label == "Remove Subcategory").IsDisabled = false;

                ReportChanges();
            }
            else
            {
                _selectedSubcategory = string.Empty;
            }

            return default;
        }

        public async ValueTask RemoveCategory(ButtonEventArgs e)
        {
            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var title = CategoryTitle.Create(_selectedCategory);
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new DeleteCategoryCommand(new EmporiumId(_context.Guild.Id), title));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"Category {Markdown.Bold(_selectedCategory)} Successfully Removed!"));

            _selectedCategory = string.Empty;
            _categories.Remove(Category.Create(title));

            MessageTemplate = message => message.AddEmbed(new LocalEmbed().WithDescription($"Number of registered categories: {Markdown.Bold(_categories.Count)}").WithDefaultColor());

            AddCategoryComponents();
            ReportChanges();

            return;
        }

        public async ValueTask RemoveSubCategory(ButtonEventArgs e)
        {
            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var categoryTitle = CategoryTitle.Create(_selectedCategory);
            var category = _categories.First(x => x.Title.Equals(categoryTitle));
            var subcategory = category.SubCategories.First(x => x.Title.Equals(_selectedSubcategory));
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new DeleteSubcategoryCommand(new EmporiumId(_context.Guild.Id), categoryTitle, subcategory.Title));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"Subcategory {Markdown.Bold(_selectedSubcategory)} Successfully Removed!"));

            _selectedSubcategory = string.Empty;
            category.SubCategories.Remove(subcategory);

            MessageTemplate = message =>
            {
                message.AddEmbed(new LocalEmbed().WithDescription($"Subcategories registerd under {_selectedCategory}: {Markdown.Bold(category.SubCategories.Skip(1).Count())}").WithDefaultColor());
            };

            AddSubcategoryComponents();
            ReportChanges();

            return;
        }

        private void AddCategoryComponents()
        {
            foreach (var componenet in EnumerateComponents().Where(x => x.Row < 4))
                RemoveComponent(componenet);

            var removeCategory = new ButtonViewComponent(RemoveCategory) { Label = "Remove Category", Style = LocalButtonComponentStyle.Danger, Row = 0, Position = 0 };
            var selection = new SelectionViewComponent(SelectCategory) { MaximumSelectedOptions = 1, Placeholder = "Select a Category", Row = 1 };

            if (_categories.Count == 0)
                selection.Options.Add(new LocalSelectionComponentOption("No Categories Exist", "0"));

            var count = 0;
            foreach (var category in _categories)
            {
                count++;
                var option = new LocalSelectionComponentOption(category.Title.ToString(), count.ToString());
                selection.Options.Add(option);
            }

            selection.IsDisabled = _categories.Count == 0;
            removeCategory.IsDisabled = _selectedCategory == string.Empty;

            AddComponent(new ButtonViewComponent(AddCategory) { Label = "Add Category", Style = LocalButtonComponentStyle.Success, Row = 0, Position = 1 });
            AddComponent(removeCategory);
            AddComponent(selection);
        }

        private void AddSubcategoryComponents()
        {
            foreach (var componenet in EnumerateComponents().Where(x => x.Row < 4))
                RemoveComponent(componenet);

            var removeSubcategory = new ButtonViewComponent(RemoveSubCategory) { Label = "Remove Subcategory", Style = LocalButtonComponentStyle.Danger, Row = 1, Position = 0 };
            var selection = new SelectionViewComponent(SelectSubcategory) { MaximumSelectedOptions = 1, Placeholder = "Select a Subcategory", Row = 2 };
            var subcategories = _categories.First(x => x.Title.Equals(_selectedCategory)).SubCategories.Skip(1).ToArray();

            if (subcategories.Length == 0)
                selection.Options.Add(new LocalSelectionComponentOption("No subcategories Exist", "0"));

            var count = 0;
            foreach (var subcategory in subcategories)
            {
                count++;
                var option = new LocalSelectionComponentOption(subcategory.Title.ToString(), count.ToString());
                selection.Options.Add(option);
            }

            selection.IsDisabled = subcategories.Length == 0;
            removeSubcategory.IsDisabled = _selectedSubcategory == string.Empty;

            AddComponent(new ButtonViewComponent(RemoveCategory) { Label = $"Remove Category", Style = LocalButtonComponentStyle.Danger, Row = 0, Position = 0 });
            AddComponent(new ButtonViewComponent(CategoryView) { Label = $"Back to Categories", Style = LocalButtonComponentStyle.Primary, Row = 0, Position = 1 });
            AddComponent(new ButtonViewComponent(AddSubcategory) { Label = "Add Subcategory", Style = LocalButtonComponentStyle.Success, Row = 1, Position = 1 });
            AddComponent(removeSubcategory);
            AddComponent(selection);
        }

        public ValueTask CategoryView(ButtonEventArgs e)
        {
            AddCategoryComponents();

            MessageTemplate = message => message.AddEmbed(new LocalEmbed().WithDescription($"Number of registered categories: {Markdown.Bold(_categories.Count)}").WithDefaultColor());

            ReportChanges();

            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public async ValueTask CloseView(ButtonEventArgs e) => await Task.Delay(TimeSpan.FromMilliseconds(500));

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
