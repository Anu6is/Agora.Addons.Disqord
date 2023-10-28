using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
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
            if (context is IDiscordApplicationCommandContext cmdContext && cmdContext.Interaction.Type == InteractionType.ApplicationCommandAutoComplete) return Results.Success;

            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null)
                return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            if (settings.EconomyType.Equals(_economyType, StringComparison.OrdinalIgnoreCase))
                return Results.Success;

            return Results.Failure($"{_economyType} Economy must be enabled");
        }
    }
}
