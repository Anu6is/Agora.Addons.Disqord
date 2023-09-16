using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireSetupAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            return Results.Success;
        }
    }
}
