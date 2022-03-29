using Disqord;

namespace Agora.Addons.Disqord.Extensions
{
    public static class EmbedExtensions
    {
        public static LocalEmbed WithDefaultColor(this LocalEmbed e) => e.WithColor(new(0x2F3136));

        public static LocalEmbed AddInlineField(this LocalEmbed e, string name, string value) => e.AddField(name, value, true);

        public static LocalEmbed AddInlineField(this LocalEmbed e, string name, object value) => e.AddField(name, value, true);

        public static LocalEmbed AddInlineField(this LocalEmbed e, LocalEmbedField ef) => e.AddField(ef);

        public static LocalEmbed AddInlineBlankField(this LocalEmbed e) => e.AddBlankField(true);
    }
}
