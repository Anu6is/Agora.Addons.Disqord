using Disqord;

namespace Agora.Addons.Disqord.Menus
{
    public delegate ValueTask ActionButtonCallback();
    public class ActionButton
    {
        public ActionButtonCallback Callback { get; init; }
        
        public string Label { get; set; }
        public LocalButtonComponentStyle Style { get; set; }
        public LocalEmoji Emoji { get; set; }
        public bool IsDisabled { get; set; }

        public ActionButton(ActionButtonCallback callback)
        {
            Callback = callback;
        }

        public ActionButton WithLabel(string label)
        {
            Label = label;
            return this;
        }

        public ActionButton WithEmoji(LocalEmoji emoji)
        {
            Emoji = emoji;
            return this;
        }

        public ActionButton IsRed()
        {
            Style = LocalButtonComponentStyle.Danger;
            return this;
        }

        public ActionButton IsGreen()
        {
            Style = LocalButtonComponentStyle.Success;
            return this;
        }

        public ActionButton IsBlue()
        {
            Style = LocalButtonComponentStyle.Primary;
            return this;
        }

        public ActionButton IsGrey()
        {
            Style = LocalButtonComponentStyle.Secondary;
            return this;
        }

        public ActionButton Disable()
        {
            IsDisabled = true;
            return this;
        }

        public ActionButton Enable()
        {
            IsDisabled = false;
            return this;
        }
    }
}
