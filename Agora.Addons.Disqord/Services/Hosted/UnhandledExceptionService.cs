using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Qmmands;
using Sentry;
using Sentry.Extensibility;

namespace Agora.Addons.Disqord
{
    public class UnhandledExceptionService : DiscordBotService
    {
        private readonly IHub _hub;

        public UnhandledExceptionService(DiscordBotBase bot, IHub sentryHub, ILogger<UnhandledExceptionService> logger) : base(logger, bot)
        {
            _hub = sentryHub;

            SentrySdk.ConfigureScope(scope => scope.AddEventProcessor(new SentryEventProcessor()));
        }

        public ValueTask CommandExecutionFailed(IDiscordCommandContext commandContext, IResult result)
        {
            var context = commandContext as IDiscordGuildCommandContext;
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

            _hub?.CaptureEvent(new SentryEvent()
            {
                ServerName = guild.Name,
                Logger = $"{guild.Name}.{channel.Name}.{alias}",
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

            return default;
        }

        private class SentryEventProcessor : ISentryEventProcessor
        {
            public SentryEvent Process(SentryEvent @event)
            {
                if (@event.Tags.TryGetValue("eventId", out var id))
                {
                    if (id == "Microsoft.EntityFrameworkCore.Query.MultipleCollectionIncludeWarning") return null;
                    if (id == "ChecksFailedResult" || id == "ParameterChecksFailedResult" || id == "TypeParseFailedResult") return null;
                    if (id == "ValidationException" || id == "UnauthorizedAccessException") return null;
                    if (id == "FormatException") return null;
                };

                if (@event.SentryExceptions.Any(e => e.Type.Equals("System.InvalidOperationException") && e.Value.Contains("DefaultBotCommandsSetup")))
                    return @event;

                if (@event.Level == SentryLevel.Error)
                {
                    switch (@event.Logger)
                    {
                        case "Disqord.Bot.DiscordBot":
                            return null;
                        case "Disqord.Hosting.DiscordClientMasterService":
                            return null;
                        default:
                            break;
                    }
                }

                return @event;
            }
        }
    }
}
