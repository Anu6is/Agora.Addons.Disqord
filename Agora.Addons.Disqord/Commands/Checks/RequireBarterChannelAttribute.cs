using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class RequireBarterChannelAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            if (context.Bot.GetChannel(context.GuildId, context.ChannelId) is IThreadChannel) return Results.Success;

            return Results.Failure("This command can only be used in a barter channel.");
        }
    }
}
