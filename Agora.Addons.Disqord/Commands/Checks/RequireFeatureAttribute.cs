using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public class RequireFeatureAttribute : DiscordGuildCheckAttribute
    {
        private readonly SettingsFlags _flag;
        private readonly bool _invert;

        public RequireFeatureAttribute(SettingsFlags settingsFeature, bool invert = false)
        {
            _flag = settingsFeature;
            _invert = invert;
        }

        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            if (context is IDiscordApplicationCommandContext cmdContext && cmdContext.Interaction.Type == InteractionType.ApplicationCommandAutoComplete) return Results.Success;

            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null)
                return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            if (settings.Features.HasFlag(_flag) != _invert)
                return Results.Success;

            return Results.Failure($"{_flag} must be enabled");
        }
    }
}
