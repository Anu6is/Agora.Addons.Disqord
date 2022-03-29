using Disqord;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus
{
    public class GuildSettingsContext
    {
        public Snowflake GuildId { get; }
        public IServiceProvider Services { get; }
        public IDiscordGuildSettings Settings { get; }
        public IGuildSettingsService SettingsService { get; }

        public GuildSettingsContext(IDiscordGuildSettings settings, IServiceProvider services)
        {
            Services = services;
            Settings = settings;
            GuildId = settings.GuildId;
            SettingsService = services.GetRequiredService<IGuildSettingsService>();
        }
    }
}
