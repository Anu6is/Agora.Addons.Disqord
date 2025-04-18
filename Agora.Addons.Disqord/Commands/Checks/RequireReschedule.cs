using Disqord;
using Disqord.Bot.Commands;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public sealed class RequireReschedule : DiscordParameterCheckAttribute
    {
        public override bool CanCheck(IParameter parameter, object value) => true;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            if (context is not IDiscordGuildCommandContext commandContext) return Results.Success;

            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            if (settings.Features.HasFlag(SettingsFlags.DisableRelisting))
                await commandContext.Bot.SendMessageAsync(commandContext.ChannelId,
                                                          new LocalMessage()
                                                                .AddEmbed(new LocalEmbed()
                                                                .WithDescription("Auto-Rescheduling is disabled")
                                                                .WithColor(Color.OrangeRed)));

            return Results.Success;
        }
    }
}
