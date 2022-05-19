using Disqord;
using Disqord.Bot;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class RequireBarterChannelAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext context)
        {
            if (context.Channel is IThreadChannel) return Success();
            
            return Failure("This command can only be used in a barter channel.");
        }
    }
}
