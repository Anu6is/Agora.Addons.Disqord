using Disqord;
using Disqord.Bot.Commands;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireContentAttribute : DiscordParameterCheckAttribute
    {
        private readonly string _contentType;

        public RequireContentAttribute(string contentType)
        {
            _contentType = contentType;
        }

        public override bool CanCheck(IParameter parameter, object value) => value is IAttachment;

        public override ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            if (argument is IAttachment attachment && attachment.ContentType.StartsWith(_contentType)) return Results.Success;

            return Results.Failure($"Invalid Upload: Attachment must be a {_contentType}");
        }
    }
}
