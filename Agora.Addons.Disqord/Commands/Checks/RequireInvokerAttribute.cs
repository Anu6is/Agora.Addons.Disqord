using Disqord;
using Disqord.Bot.Commands;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireInvokerAttribute : DiscordParameterCheckAttribute
    {
        public override bool CanCheck(IParameter parameter, object value) => value is Snowflake;

        public override ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            var user = (Snowflake)argument;

            if (context.AuthorId.Equals(user)) return Results.Success;

            return Results.Failure($"Only {Mention.User(user)} can execute this action!");
        }
    }
}
