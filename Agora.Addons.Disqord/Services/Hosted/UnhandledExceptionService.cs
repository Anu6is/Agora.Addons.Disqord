using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Qmmands;
using Qommon;
using Sentry;

namespace Agora.Addons.Disqord
{
    public class UnhandledExceptionService : DiscordBotService
    {
        private readonly IHub _hub;

        public UnhandledExceptionService(DiscordBotBase bot, IHub sentryHub, ILogger<UnhandledExceptionService> logger) : base(logger, bot)
        {
            _hub = sentryHub;
        }

        public ValueTask CommandExecutionFailed(IDiscordCommandContext commandContext, IResult result)
        {
            if (commandContext is not IDiscordGuildCommandContext context) return default;

            var command = context.Command as ApplicationCommand;
            var guild = Bot.GetGuild(context.GuildId);
            var channel = Bot.GetChannel(context.GuildId, context.ChannelId);
            var currentMember = Bot.GetMember(context.GuildId, context.Bot.CurrentUser.Id);
            var module = context.Command.Module as ApplicationModule;
            var parent = module?.Parent;
            var alias = $"{parent?.Alias} {module?.Alias} {context.Command.Name}".TrimStart();
            var failureReason = result.FailureReason;
            var eventId = result.GetType().Name;

            switch (result)
            {
                case ChecksFailedResult checksFailedResult:
                    failureReason = string.Join('\n', checksFailedResult.FailedChecks.Values.Select(x => $"• {x.FailureReason}"));
                    break;
                case ParameterChecksFailedResult parameterChecksFailedResult:
                    failureReason = string.Join('\n', parameterChecksFailedResult.FailedChecks.Values.Select(x => $"• {x.FailureReason}"));
                    break;
                case TypeParseFailedResult typeParseFailedResult:
                    failureReason = typeParseFailedResult.FailureReason;
                    break;
                case ExceptionResult exceptionResult:
                    eventId = exceptionResult.Exception.GetType().Name;

                    failureReason = exceptionResult.Exception switch
                    {
                        ValidationException validation => string.Join('\n', validation.Errors.Select(x => $"• {x.ErrorMessage}")),
                        UnauthorizedAccessException unauthorizedAccess => unauthorizedAccess.Message,
                        _ => exceptionResult.Exception.ToString()
                    };
                    break;
                default:
                    break;
            }

            _hub.CaptureEvent(new SentryEvent()
            {
                ServerName = guild?.Name,
                Logger = $"{guild?.Name}.{channel?.Name}.{alias}",
                Message = new SentryMessage() { Message = failureReason },
            }, scope =>
            {
                scope.AddBreadcrumb(
                    message: $"Error occurred while executing {alias}",
                    category: context.ExecutionStep?.GetType().ToString(),
                    dataPair: ("reason", failureReason),
                    type: "user");

                scope.TransactionName = alias;
                scope.User = new User() { Id = context.Author.Id.ToString(), Username = context.Author.Tag };

                scope.SetTag("eventId", eventId);
                scope.SetTag("GuildId", context.GuildId.ToString());
                scope.SetTag("ChannelId", context.ChannelId.ToString());

                scope.SetExtra("Shard", Bot.ApiClient.GetShardId(context.GuildId));
                scope.SetExtra("Arguments", $"{alias} {(context.Arguments != null ? string.Join(" | ", context.Arguments.Select(x => $"{x.Key.Name}: {x.Value}")) : string.Empty)}");
                scope.SetExtra("Guild Permissions", currentMember.CalculateGuildPermissions(guild).ToString());
                scope.SetExtra("Channel Permissions", currentMember.CalculateChannelPermissions(channel).ToString());
            });

            return default;
        }

        public ValueTask InteractionExecutionFailed(InteractionReceivedEventArgs args, Exception exception)
        {
            var interaction = (IComponentInteraction)args.Interaction;
            var command = interaction.CustomId;
            var reason = "An error occurred while executing the interaction.";
            var step = "processing";
            var eventId = exception.GetType().Name;

            switch (exception)
            {
                case ValidationException validationException:
                    reason = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));
                    step = "validation";
                    break;
                case UnauthorizedAccessException unauthorizedAccessException:
                    reason = unauthorizedAccessException.Message;
                    step = "authorization";
                    break;
                default:
                    break;
            }

            try
            {
                _hub.CaptureEvent(new SentryEvent()
                {
                    Message = new SentryMessage() { Message = exception.ToString() },
                    ServerName = interaction.GuildId.ToString(),
                    Logger = $"{interaction.GuildId}.{interaction.ChannelId}.{interaction.CustomId}",
                }, scope =>
                {
                    scope.AddBreadcrumb(
                        message: $"Error occurred while executing {command}",
                        category: step,
                        dataPair: ("reason", reason),
                        type: "user");

                    scope.TransactionName = command;
                    scope.User = new User() { Id = interaction.Author.Id.ToString(), Username = interaction.Author.Tag };

                    scope.SetTag("eventId", eventId);
                    scope.SetTag("GuildId", interaction.GuildId.ToString());
                    scope.SetTag("ChannelId", interaction.ChannelId.ToString());
                    scope.SetTag("MessageId", interaction.Message.Id.ToString());

                    var client = interaction.Client as AgoraBot;
                    var member = interaction.Author as IMember;
                    var channel = client.GetChannel(interaction.GuildId.Value, interaction.ChannelId);

                    scope.SetExtra("Shard", client.ApiClient.GetShardId(interaction.GuildId));
                    scope.SetExtra("Arguments", interaction.CustomId);
                    scope.SetExtra("Guild Permissions", member?.CalculateGuildPermissions().ToString());
                    scope.SetExtra("Channel Permissions", member?.CalculateChannelPermissions(channel).ToString());
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error capturing event");
            }


            return default;
        }

        public static SentryEvent BeforeSend(SentryEvent arg)
        {
            const string Acknowledged = "Interaction has already been acknowledged.";
            const string Responded = "This interaction has already been responded to.";
            const string Permissions = "The bot lacks the necessary permissions";

            const string FormatException = "FormatException";
            const string TimeoutException = "TimeoutException";
            const string RestApiException = "RestApiException";
            const string RateLimitException = "RateLimitException";
            const string ChecksFailedResult = "ChecksFailedResult";
            const string ValidationException = "ValidationException";
            const string TypeParseFailedResult = "TypeParseFailedResult";
            const string CommandRateLimitedResult = "CommandRateLimitedResult";
            const string InvalidOperationException = "InvalidOperationException";
            const string NoMatchFoundException = "Humanizer.NoMatchFoundException";
            const string ParameterChecksFailedResult = "ParameterChecksFailedResult";
            const string UnauthorizedAccessException = "UnauthorizedAccessException";
            const string DbUpdateConcurrencyException = "DbUpdateConcurrencyException";
            const string MultipleCollectionWarning = "Microsoft.EntityFrameworkCore.Query.MultipleCollectionIncludeWarning";
            const string BoolWithDefaultWarning = "Microsoft.EntityFrameworkCore.Model.Validation.BoolWithDefaultWarning";

            if (arg.Environment == "debug") return null;
            if (arg.Exception is not null)
            {
                if (arg.Exception is TimeoutException) return null;
                if (arg.Exception is RateLimitException) return null;
                if (arg.Exception is ValidationException) return null;
                if (arg.Exception is UnauthorizedAccessException) return null;
                if (arg.Exception is InteractionExpiredException) return null;
                if (arg.Exception is Humanizer.NoMatchFoundException) return null;
                if (arg.Exception is RestApiException ex)
                {
                    if (ex.StatusCode == HttpResponseStatusCode.Forbidden) return null;
                    if (ex.StatusCode == HttpResponseStatusCode.NotFound) return null;

                    if (ex.ErrorModel != null && ex.ErrorModel.Message.TryGetValue(out var message))
                    {
                        if (message.Equals(Acknowledged)) return null;
                    }
                }
                if (arg.Exception is InvalidOperationException op)
                {
                    if (op.Message.Equals(Responded)) return null;
                    if (op.Message.Contains(Permissions)) return null;
                }
            }

            if (arg.Tags.TryGetValue("eventId", out var id))
            {
                if (id == ChecksFailedResult || id == ParameterChecksFailedResult || id == TypeParseFailedResult) return null;
                if (id == MultipleCollectionWarning || id == BoolWithDefaultWarning) return null;
                if (id == ValidationException || id == UnauthorizedAccessException) return null;
                if (id == FormatException || id == NoMatchFoundException) return null;
                if (id == DbUpdateConcurrencyException) return null;
                if (id == InvalidOperationException) return null;
                if (id == CommandRateLimitedResult) return null;
                if (id == RateLimitException) return null;
                if (id == TimeoutException) return null;

                if (id == RestApiException && arg.Message?.Message.Contains("Missing Access") == true) return null;

                var data = new Dictionary<string, string>()
                {
                    {"transaction", arg.TransactionName },
                    {"tag_keys", string.Join(",", arg.Tags.Keys) },
                    {"tag_values", string.Join(",", arg.Tags.Values) },
                    {"EventId", arg.EventId.ToString() },
                    {"Exception", arg.Exception?.Message ?? "empty"  }
                };

                arg.AddBreadcrumb(new Breadcrumb($"Uncaught event -> {id}: {arg.Message?.Message}", "info", data)); ;
            }

            if (arg.Level == SentryLevel.Warning && arg.Logger != null && arg.Logger.Contains("Microsoft.EntityFrameworkCore.")) return null;
            if (arg.Level == SentryLevel.Error)
            {
                switch (arg.Logger)
                {
                    case "Disqord.Bot.DiscordBot": return null;
                    case "Disqord.Hosting.DiscordClientMasterService": return null;
                    default: break;
                }
            }

            return arg;
        }
    }
}
