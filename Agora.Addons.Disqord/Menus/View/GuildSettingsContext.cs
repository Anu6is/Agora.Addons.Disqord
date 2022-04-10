using Disqord.Gateway;
using Emporia.Extensions.Discord;

namespace Agora.Addons.Disqord.Menus
{
    public class GuildSettingsContext
    {
        public CachedGuild Guild { get; }
        public IServiceProvider Services { get; }
        public IDiscordGuildSettings Settings { get; }

        public GuildSettingsContext(CachedGuild guild, IDiscordGuildSettings settings, IServiceProvider services)
        {
            Guild = guild;
            Services = services;
            Settings = settings;
        }
    }
}
