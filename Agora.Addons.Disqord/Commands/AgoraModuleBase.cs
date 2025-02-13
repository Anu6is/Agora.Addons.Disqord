﻿using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Services;
using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;

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
        public ITransactionTracer Transaction { get; private set; }
        public IEmporiaCacheService Cache { get; private set; }
        public IDiscordGuildSettings Settings { get; private set; }
        public IGuildSettingsService SettingsService { get; private set; }
        public PluginManagerService PluginManagerService { get; private set; }

        public ShowroomId ShowroomId { get; private set; }
        public EmporiumId EmporiumId => new(Context.GuildId);
        public CachedGuild Guild => Context.Bot.GetGuild(Context.GuildId);
        public CachedMessageGuildChannel Channel => Context.Bot.GetChannel(Context.GuildId, Context.ChannelId) as CachedMessageGuildChannel;

        public ICommandModuleBase Base => this;

        public override async ValueTask OnBeforeExecuted()
        {
            Interlocked.Increment(ref _activeCommands);

            SetLogContext();

            var contextService = Context.Services.GetService<AgoraContextService>();

            if (contextService?.Context is IAgoraContext ctx && contextService.Context.Interaction.AuthorId == Context.AuthorId)
            {
                Logger.LogDebug("Using sudo context");

                Context = ctx.FromContext(Context);
            }

            Data = Context.Services.GetRequiredService<IDataAccessor>();
            Mediator = Context.Services.GetRequiredService<IMediator>();
            Cache = Context.Services.GetRequiredService<IEmporiaCacheService>();
            SettingsService = Context.Services.GetRequiredService<IGuildSettingsService>();
            PluginManagerService = Context.Services.GetRequiredService<PluginManagerService>();

            Transaction = SentrySdk.StartTransaction(Context.Command.Module.Name, Context.Command.Name, $"{Context.Bot.ApiClient.GetShardId(Context.GuildId)}");
            Transaction.User = new SentryUser() { Id = Context.AuthorId.ToString(), Username = Context.Author.Tag };
            Transaction.SetTag("guild", Context.GuildId.ToString());
            Transaction.SetTag("channel", Context.ChannelId.ToString());
            Transaction.SetExtra("active_commands", _activeCommands);

            Settings = await SettingsService.GetGuildSettingsAsync(Context.GuildId);

            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);

            if (Channel is null)
            {
                throw new ValidationException("The bot does not have permissions to access this channel");
            }

            if (Channel is IThreadChannel thread)
                ShowroomId = new(thread.ChannelId);
            else if (emporium != null && emporium.Showrooms.Any(x => x.Id.Value.Equals(Context.ChannelId)))
                ShowroomId = new(Context.ChannelId);
            else
                ShowroomId = new(Channel.CategoryId.GetValueOrDefault(Context.ChannelId));

            await base.OnBeforeExecuted();

            return;
        }

        private void SetLogContext()
        {
            var loggerContext = Context.Services.GetRequiredService<ILoggerContext>();

            loggerContext.ContextInfo.Add("ID", Context.Interaction?.Id);

            if (Context.Command is ApplicationCommand command)
                loggerContext.ContextInfo.Add("Command", $"{Context.Interaction?.CommandType}: {Context.Interaction?.CommandName} {command.Module.Alias} {command.Alias}");

            loggerContext.ContextInfo.Add("User", Context.Author.GlobalName);
            loggerContext.ContextInfo.Add($"{Channel?.Type}Channel", Context.ChannelId);
            loggerContext.ContextInfo.Add("Guild", Context.GuildId);
        }

        public override ValueTask OnAfterExecuted()
        {
            Interlocked.Decrement(ref _activeCommands);

            Transaction.Finish();

            return base.OnAfterExecuted();
        }

        protected IResult OkResponse(bool isEphimeral = false, string content = "", params LocalEmbed[] embeds)
        {
            var message = new LocalInteractionMessageResponse();

            if (isEphimeral) message.WithIsEphemeral();
            if (content.IsNotNull()) message.WithContent(content);
            if (embeds.Length != 0) message.WithEmbeds(embeds.Select(x => x.WithDefaultColor()));

            return Response(message);
        }

        protected IResult SuccessResponse(bool isEphimeral = false, string content = "", params LocalEmbed[] embeds)
        {
            var message = new LocalInteractionMessageResponse();

            if (isEphimeral) message.WithIsEphemeral();
            if (content.IsNotNull()) message.WithContent(content);
            if (embeds.Length != 0) message.WithEmbeds(embeds.Select(x => x.WithColor(Color.Teal)));

            return Response(message);
        }

        protected IResult ErrorResponse(bool isEphimeral = false, string content = "", params LocalEmbed[] embeds)
        {
            var message = new LocalInteractionMessageResponse();

            if (isEphimeral) message.WithIsEphemeral();
            if (content.IsNotNull()) message.WithContent(content);
            if (embeds.Length != 0) message.WithEmbeds(embeds.Select(x => x.WithColor(Color.Red)));

            return Response(message);
        }

        public bool TryOverrideSchedule(out (DayOfWeek Weekday, TimeSpan Time)[] schedule)
        {
            schedule = null;

            var channel = Context.Bot.GetChannel(Context.GuildId, Context.ChannelId) as ITopicChannel ?? Context.Bot.GetChannel(Guild.Id, ShowroomId.Value) as ITopicChannel;

            var scheduleOverride = channel != null
                                && channel.Topic.IsNotNull()
                                && channel.Topic.StartsWith("Schedule", StringComparison.OrdinalIgnoreCase);

            if (scheduleOverride)
            {
                var scheduledTime = channel.Topic.Replace("Schedule", "", StringComparison.OrdinalIgnoreCase).TrimStart(new[] { ':', ' ' });

                if (string.IsNullOrWhiteSpace(scheduledTime)) return false;

                schedule = scheduledTime.Split(';')
                    .Select(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Select(x => (Weekday: Enum.Parse<DayOfWeek>(x[0]), Time: TimeOnly.Parse(x[1]).ToTimeSpan()))
                    .OrderBy(x => x.Weekday).ToArray();
            }

            return scheduleOverride;
        }

        public async Task WaitForCommandsAsync(int waitTimeInMinutes)
            => await (this as ICommandModuleBase).WaitForCommandsAsync(() => _activeCommands > 1, waitTimeInMinutes, Context.Bot.StoppingToken);
    }
}
