using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentry;

namespace Agora.Addons.Disqord.Commands
{
    public abstract class AgoraModuleBase : DiscordApplicationGuildModuleBase, ICommandModuleBase
    {
        private static int _activeCommands;

        public static bool RebootInProgress { get; set; }
        public static bool ShutdownInProgress { get; set; }

        ILogger ICommandModuleBase.Logger => Logger;

        public IDataAccessor Data { get; private set; }
        public IMediator Mediator { get; private set; }
        public ITransaction Transaction { get; private set; }
        public IEmporiaCacheService Cache { get; private set; }
        public IDiscordGuildSettings Settings { get; private set; }
        public IGuildSettingsService SettingsService { get; private set; }

        public ShowroomId ShowroomId { get; private set; }
        public EmporiumId EmporiumId => new(Context.GuildId);
        public CachedGuild Guild => Context.Bot.GetGuild(Context.GuildId);
        public CachedMessageGuildChannel Channel => Context.Bot.GetChannel(Context.GuildId, Context.ChannelId) as CachedMessageGuildChannel;

        public ICommandModuleBase Base => this;

        public override async ValueTask OnBeforeExecuted()
        {
            Interlocked.Increment(ref _activeCommands);

            Data = Context.Services.GetRequiredService<IDataAccessor>();
            Mediator = Context.Services.GetRequiredService<IMediator>();
            Cache = Context.Services.GetRequiredService<IEmporiaCacheService>();
            SettingsService = Context.Services.GetRequiredService<IGuildSettingsService>();

            Transaction = SentrySdk.StartTransaction(Context.Command.Module.Name, Context.Command.Name, $"{Context.Bot.ApiClient.GetShardId(Context.GuildId)}");
            Transaction.User = new User() { Id = Context.Author.Id.ToString(), Username = Context.Author.Tag };
            Transaction.SetTag("guild", Context.GuildId.ToString());
            Transaction.SetTag("channel", Context.ChannelId.ToString());
            Transaction.SetExtra("active_commands", _activeCommands);

            Settings = await SettingsService.GetGuildSettingsAsync(Context.GuildId);

            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

            if (Channel is IThreadChannel thread)
                ShowroomId = new(thread.ChannelId);
            else if (emporium != null && emporium.Showrooms.Any(x => x.Id.Value.Equals(Context.ChannelId.RawValue)))
                ShowroomId = new(Context.ChannelId);
            else
                ShowroomId = new(Channel.CategoryId.GetValueOrDefault());


            if (Context.Command is ApplicationCommand command)
            {
                var commandName = $"{command.Module.Parent?.Name} {command.Module.Alias} {command.Alias}".TrimStart();
                Logger.LogDebug("{Author} executed {Command} in {Guild}", Context.Author.Name, commandName, Context.GuildId);
            }

            await base.OnBeforeExecuted();

            return;
        }

        public override ValueTask OnAfterExecuted()
        {
            Interlocked.Decrement(ref _activeCommands);

            Transaction.Finish();

            return base.OnAfterExecuted();
        }

        public async Task WaitForCommandsAsync(int waitTimeInMinutes)
            => await (this as ICommandModuleBase).WaitForCommandsAsync(() => _activeCommands > 1, waitTimeInMinutes, Context.Bot.StoppingToken);
    }
}
