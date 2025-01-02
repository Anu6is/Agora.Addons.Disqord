using Agora.Shared.Models;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Agora.Addons.Disqord.Commands
{
    public class QuickTipsView : PagedViewBase
    {
        private readonly CultureInfo _locale;
        private readonly IServiceScopeFactory _scopeFactory;

        public QuickTipsView(IEnumerable<Fact> tips, CultureInfo locale, IServiceScopeFactory scopeFactory)
            : base(new ListPageProvider(tips.Select(tip =>
            {
                var embed = new LocalEmbed().WithTitle("💡 Did You Know").WithDescription(tip.Summary);

                if (tip.Details != string.Empty) embed.AddField("Additional Info", tip.Details);

                return new Page().AddEmbed(embed);
            }).ToArray()))
        {
            _locale = locale;
            _scopeFactory = scopeFactory;

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                button.Label = TranslateButton(button.Label);
            }
        }

        [Button(Label = "Back", Style = LocalButtonComponentStyle.Primary)]
        public ValueTask Previous(ButtonEventArgs e)
        {
            if (CurrentPageIndex == 0)
                CurrentPageIndex = PageProvider.PageCount - 1;
            else
                CurrentPageIndex--;

            return default;
        }

        [Button(Label = "Next", Style = LocalButtonComponentStyle.Primary)]
        public ValueTask NextTip(ButtonEventArgs e)
        {
            if (CurrentPageIndex + 1 == PageProvider.PageCount)
                CurrentPageIndex = 0;
            else
                CurrentPageIndex++;

            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        private string TranslateButton(string key)
        {
            using var scope = _scopeFactory.CreateScope();
            var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();

            localization.SetCulture(_locale);

            return localization.Translate(key, "ButtonStrings");
        }
    }
}
