using Disqord;

namespace Agora.Addons.Disqord
{
    public static class AgoraTag
    {
        public static readonly LocalForumTag Pending = new() { Emoji = new LocalEmoji("⌛"), Name = "Pending", IsModerated = true };
        public static readonly LocalForumTag Active = new() { Emoji = new LocalEmoji("✅"), Name = "Active", IsModerated = true };
        public static readonly LocalForumTag Expired = new() { Emoji = new LocalEmoji("⛔"), Name = "Expired", IsModerated = true };
        public static readonly LocalForumTag Locked = new() { Emoji = new LocalEmoji("🔐"), Name = "Locked", IsModerated = true };
        public static readonly LocalForumTag Sold = new() { Emoji = new LocalEmoji("💰"), Name = "Sold", IsModerated = true };
    }
}
