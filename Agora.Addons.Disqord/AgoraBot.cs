using Disqord.Bot;
using Disqord.Bot.Sharding;
using Disqord.Sharding;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;

namespace Agora.Addons.Disqord
{
    internal class AgoraBot : DiscordBotSharder
    {
        public AgoraBot(IOptions<DiscordBotSharderConfiguration> options, ILogger<DiscordBotSharder> logger, IServiceProvider services, DiscordClientSharder client) 
            : base(options, logger, services, client) { }

        protected override string FormatFailureReason(DiscordCommandContext context, FailedResult result)
        {
            if (result is CommandExecutionFailedResult executionFailedResult)
                return executionFailedResult.Exception switch
                {
                    ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                    UnauthorizedAccessException unauthorizedAccessException => unauthorizedAccessException.Message,
                    _ => null
                };
            
            return base.FormatFailureReason(context, result);
        }
    }
}