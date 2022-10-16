using Disqord;
using Disqord.Bot.Commands;
using Disqord.Rest;
using Qmmands;
using Qommon;
using System.Runtime.CompilerServices;

namespace Agora.Addons.Disqord.Commands
{
    public partial class AgoraCommandRateLimiter : ICommandRateLimiter
    {
        public Func<ICommandContext, object, object> BucketKeyGenerator { get; set; }

        protected readonly ConditionalWeakTable<ICommand, Node> Nodes;

        public AgoraCommandRateLimiter()
        {
            Nodes = new ConditionalWeakTable<ICommand, Node>();
        }

        protected virtual async ValueTask<Bucket> CreateBucketAsync(ICommandContext context, RateLimitAttribute rateLimit)
        {
            var bucket = new Bucket(rateLimit);

            if (rateLimit.BucketType.Equals(ChannelType.News) && context is IDiscordGuildCommandContext commandContext)
            {
                var startFrom = new Snowflake(DateTimeOffset.UtcNow - rateLimit.Window);
                var messages = await commandContext.Bot.FetchMessagesAsync(commandContext.ChannelId, rateLimit.Uses, FetchDirection.After, startFrom);

                if (messages.Count == 0) return bucket;

                var lastMessage = messages.OrderBy(x => x.CreatedAt()).Last();

                bucket.ReloadState(rateLimit.Uses - messages.Count, lastMessage.CreatedAt());
            }

            return bucket;
        }

        protected virtual Node CreateNode(IEnumerable<RateLimitAttribute> rateLimits)
            => new(this, rateLimits);

        public async ValueTask<IResult> RateLimitAsync(ICommandContext context)
        {
            if (BucketKeyGenerator == null)
                return Results.Success;

            Guard.IsNotNull(context.Command);

            var command = context.Command;
            var node = Nodes.GetValue(command, command =>
            {
                var rateLimitAttributes = command.CustomAttributes.OfType<RateLimitAttribute>().ToArray();
                return rateLimitAttributes.Length != 0 ? CreateNode(rateLimitAttributes) : null;
            });

            if (node != null)
                return await node.RateLimit(context);

            return Results.Success;
        }
    }
}
