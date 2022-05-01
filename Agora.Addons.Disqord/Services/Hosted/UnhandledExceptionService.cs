using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;
using Sentry;
using Sentry.Extensibility;

namespace Agora.Addons.Disqord
{
    public class UnhandledExceptionService : DiscordBotService
    {
        public UnhandledExceptionService(DiscordBotBase bot, CommandService commandService, ILogger<UnhandledExceptionService> logger) : base(logger, bot)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.AddEventProcessor(new SentryEventProcessor());
            });

            commandService.CommandExecutionFailed += CommandExecutionFailed;
        }

        private ValueTask CommandExecutionFailed(object sender, CommandExecutionFailedEventArgs args)
        {
            var hub =  args.Context.Services.GetRequiredService<IHub>();
            var context = (DiscordGuildCommandContext)args.Context;
            var command = context.Command;
            var result = args.Result;

            hub.CaptureEvent(new SentryEvent()
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
        
        private class SentryEventProcessor : ISentryEventProcessor
        {
            public SentryEvent Process(SentryEvent @event)
            {
                if (@event.Tags.TryGetValue("eventId", out var id) && id == "Microsoft.EntityFrameworkCore.Query.MultipleCollectionIncludeWarning")
                    return null;

                if (@event.Level == SentryLevel.Error && @event.Logger == "Disqord.Bot.Sharding.DiscordBotSharder")
                {
                    Console.WriteLine($"Discarding event {@event.EventId}: {@event.Message.Message}");
                    return null;
                }

                return @event;
            }
        }
    }
}
