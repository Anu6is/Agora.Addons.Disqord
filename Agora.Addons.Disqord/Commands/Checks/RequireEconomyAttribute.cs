using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireEconomyAttribute : DiscordGuildCheckAttribute
    {
        private readonly string _economyType;

        public RequireEconomyAttribute(string economyType)
        {
            _economyType = economyType;
        }

        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings.EconomyType.Equals(_economyType, StringComparison.OrdinalIgnoreCase))
                return Results.Success;

            return Results.Failure($"{_economyType} Economy must be enabled");
        }
    }
}
