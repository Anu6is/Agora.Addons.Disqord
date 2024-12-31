using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Menus;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Commands.Menus.View
{
    public class ListingRequirementsView : ViewBase
    {
        public DefaultListingRequirements Requirements { get; }
        public SelectionViewComponent Selection { get; }
        public GuildSettingsContext Context { get; }

        public ListingRequirementsView(DefaultListingRequirements requirements, GuildSettingsContext context)
            : base(message => message.AddEmbed(
                new LocalEmbed()
                    .WithTitle($"Additional {requirements.ListingType} Requirements")
                    .WithDescription($"Required optional values:{Environment.NewLine}{Markdown.CodeBlock(requirements)}")
                    .WithDefaultColor()))
        {
            var isAuction = requirements.ListingType == ListingType.Auction;
            var choices = requirements.AsEnumerable();
            var enabled = requirements.Configured();

            Context = context;
            Requirements = requirements;
            Selection = new SelectionViewComponent(RequirementSelection)
            {
                MinimumSelectedOptions = 0,
                MaximumSelectedOptions = isAuction ? choices.Count() : choices.Count() - 1,
                Placeholder = "Select the options that should be required"
            };

            foreach (var item in choices)
            {
                var label = item;

                if (item.Equals(nameof(DefaultListingRequirements.MaxBidIncrease)))
                {
                    if (isAuction)
                        label = "Maximum Bid Increase";
                    else
                        continue;
                }

                Selection.Options.Add(new LocalSelectionComponentOption(label, item).WithIsDefault(enabled.Any(x => x.Equals(item))));
            }

            AddComponent(Selection);

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                button.Label = TranslateButton(button.Label);
            }
        }

        public async ValueTask RequirementSelection(SelectionEventArgs e)
        {
            Requirements.Clear();

            foreach (var option in e.SelectedOptions)
                Requirements.Enable(option.Value.Value);

            foreach (var option in e.Selection.Options)
                option.IsDefault = Requirements.Configured().Any(x => x.Equals(option.Value.Value));

            MessageTemplate = message => message.WithEmbeds(new LocalEmbed()
                .WithTitle($"Additional {Requirements.ListingType} Requirements")
                .WithDescription($"Required optional values:{Environment.NewLine}{Markdown.CodeBlock(Requirements)}")
                .WithDefaultColor());

            using var scope = Context.Services.CreateScope();
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateListingRequirementsCommand(Requirements));
            }

            ReportChanges();
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        private string TranslateButton(string key)
        {
            using var scope = Context.Services.CreateScope();
            var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();

            localization.SetCulture(Context.Guild.PreferredLocale);

            return localization.Translate(key, "ButtonStrings");
        }
    }
}
