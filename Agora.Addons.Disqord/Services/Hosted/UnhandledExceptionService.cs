using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;
using Sentry;
using Sentry.Extensibility;

namespace Agora.Addons.Disqord
{
    public class UnhandledExceptionService : DiscordBotService
    {
        private readonly IHub _hub;
        
        public UnhandledExceptionService(DiscordBotBase bot, CommandService commandService, IHub sentryHub, ILogger<UnhandledExceptionService> logger) : base(logger, bot)
        {
            _hub = sentryHub;
            
            SentrySdk.ConfigureScope(scope =>
            {
                scope.AddEventProcessor(new SentryEventProcessor(bot.Services.GetRequiredService<IServiceScopeFactory>(), logger));
            });

            commandService.CommandExecutionFailed += CommandExecutionFailed;
        }

        private ValueTask CommandExecutionFailed(object sender, CommandExecutionFailedEventArgs args)
        {
            var context = (DiscordGuildCommandContext)args.Context;
            var command = context.Command;
            var result = args.Result;

            _hub.CaptureEvent(new SentryEvent()
            {
                Message = new SentryMessage() { Message = result.Exception.ToString() },
                ServerName = context.Guild.Name,
                Logger = $"{context.Guild.Name}.{context.Channel.Name}.{context.Command.Name}",
            }, scope =>
            {
                scope.AddBreadcrumb(
                    message: $"Error occurred while executing {command.Name}",
                    category: result.CommandExecutionStep.ToString(),
                    dataPair: ("reason", result.FailureReason),
                    type: "user");

                scope.Platform = "discord";
                scope.TransactionName = command.Name;
                scope.User = new User() { Id = context.Author.Id.ToString(), Username = context.Author.Tag };

                scope.SetTag("GuildId", context.GuildId.ToString());
                scope.SetTag("ChannelId", context.ChannelId.ToString());

                scope.SetExtra("Shard", context.Bot.GetShardId(context.GuildId));
                scope.SetExtra("Arguments", $"{context.Command.FullAliases[0]} {context.RawArguments}");
                scope.SetExtra("Guild Permissions", context.CurrentMember.GetPermissions(context.Guild).ToString());
                scope.SetExtra("Channel Permissions", context.CurrentMember.GetPermissions(context.Channel).ToString());
            });

            return default;
        }

        public ValueTask InteractionExecutionFailed(InteractionReceivedEventArgs args, Exception exception)
        {
            var interaction = (IComponentInteraction)args.Interaction;
            var command = interaction.CustomId;
            var reason = "An error occurred while executing the interaction.";
            var step = "processing";

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

                scope.Platform = "discord";
                scope.TransactionName = command;
                scope.User = new User() { Id = interaction.Author.Id.ToString(), Username = interaction.Author.Tag };

                scope.SetTag("GuildId", interaction.GuildId.ToString());
                scope.SetTag("ChannelId", interaction.ChannelId.ToString());

                scope.SetExtra("Shard", (interaction.Client as AgoraBot)?.GetShardId(interaction.GuildId));
                scope.SetExtra("Arguments", interaction.CustomId);
                scope.SetExtra("Guild Permissions", (interaction.Author as IMember)?.GetPermissions().ToString());
                scope.SetExtra("Channel Permissions", (interaction.Author as IMember)?.GetPermissions().ToString());
            });

            return default;
        }

        private class SentryEventProcessor : ISentryEventProcessor
        {
            private readonly ILogger<UnhandledExceptionService> _logger;
            private readonly IServiceScopeFactory _scopeFactory;

            public SentryEventProcessor(IServiceScopeFactory scopeFactory, ILogger<UnhandledExceptionService> logger)
            {
                _logger = logger;
                _scopeFactory = scopeFactory;
            }

            public SentryEvent Process(SentryEvent @event)
            {
                if (@event.Tags.TryGetValue("eventId", out var id) && id == "Microsoft.EntityFrameworkCore.Query.MultipleCollectionIncludeWarning")
                    return null;

                if (@event.Level == SentryLevel.Error) 
                {
                    switch (@event.Logger)
                    {
                        case "Disqord.Bot.Sharding.DiscordBotSharder":
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
