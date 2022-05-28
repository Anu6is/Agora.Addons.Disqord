using Disqord.Bot;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class RequireSetupAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext context)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>()
                                                 .GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Failure("Setup Required: Please execute the Server Setup command.");

            return Success();
        }
    }
}
