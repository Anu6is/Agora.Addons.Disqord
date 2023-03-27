using Agora.Shared.Models;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;

namespace Agora.Addons.Disqord.Commands
{
    public class QuickTipsView : PagedViewBase
    {
        public QuickTipsView(IEnumerable<Fact> tips)
            : base(new ListPageProvider(tips.Select(tip =>
            {
                var embed = new LocalEmbed().WithTitle("💡 Did You Know").WithDescription(tip.Summary);

                if (tip.Details != string.Empty) embed.AddField("Additional Info", tip.Details);

                return new Page().AddEmbed(embed);
            }).ToArray()))
        { }

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
    }
}
