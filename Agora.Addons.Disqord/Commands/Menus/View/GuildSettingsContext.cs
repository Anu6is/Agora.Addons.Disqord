using Disqord;
using Disqord.Gateway;
using Emporia.Extensions.Discord;

namespace Agora.Addons.Disqord.Menus
{
    public class GuildSettingsContext
    {
        public CachedGuild Guild { get; }
        public Snowflake AuthorId { get; }
        public IServiceProvider Services { get; }
        public IDiscordGuildSettings Settings { get; }

        public GuildSettingsContext(Snowflake authorId, CachedGuild guild, IDiscordGuildSettings settings, IServiceProvider services)
        {
            Guild = guild;
            AuthorId = authorId;
            Services = services;
            Settings = settings;
        }
    }
}
