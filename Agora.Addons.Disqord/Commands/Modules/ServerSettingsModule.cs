using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Menus;
using Agora.Addons.Disqord.Menus.View;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [SlashGroup("server")]
    [RequireAuthorPermissions(Permission.ManageGuild)]
    public sealed class ServerSettingsModule : AgoraModuleBase
    {
        [SkipAuthentication]
        [SlashCommand("setup")]
        [RequireUnregisteredServer]
        [Description("Setup the bot for use in your server.")]
        public async Task<IResult> ServerSetup(
            [Description("Log all sold/expired items to this channel.")][ChannelTypes(ChannelType.Text)] IChannel resultLog,
            [Description("Log all item activity to this channel.")][ChannelTypes(ChannelType.Text)] IChannel auditLog = null,
            [Description("Default currency symbol.")] string symbol = "$",
            [Description("Number of decimal places to show for prices.")] int decimalPlaces = 2,
            [Description("Current server time (24-Hour format). Defaults to UTC")] Time serverTime = null)
        {
            await Deferral();

            DefaultDiscordGuildSettings settings = null;

            await Data.BeginTransactionAsync(async () =>
            {
                var time = serverTime ?? Time.From(DateTimeOffset.UtcNow.TimeOfDay);
                var emporium = await Base.ExecuteAsync(new CreateEmporiumCommand(EmporiumId) { LocalTime = time });
                var currency = await Base.ExecuteAsync(new CreateCurrencyCommand(EmporiumId, symbol, decimalPlaces));

                settings = await Base.ExecuteAsync(new CreateGuildSettingsCommand(Context.GuildId, currency, resultLog.Id)
                {
                    AuditLogChannelId = auditLog?.Id ?? 0ul,
                    TimeOffset = emporium.TimeOffset
                });

                await SettingsService.AddGuildSettingsAsync(settings);
                await Cache.AddEmporiumAsync(emporium);
            });

            await Base.ExecuteAsync(new CreateEmporiumUserCommand(EmporiumId, ReferenceNumber.Create(Context.Author.Id)));

            var settingsContext = new GuildSettingsContext(Guild, settings, Context.Services.CreateScope().ServiceProvider);
            var options = new List<GuildSettingsOption>() { };

            return View(new ServerSetupView(settingsContext, options));
        }

        [RequireSetup]
        [SlashCommand("settings")]
        [Description("Review and/or modify the current bot settings.")]
        public async Task<IResult> ServerSettings()
        {
            await Cache.GetEmporiumAsync(Context.GuildId);

            var settingsContext = new GuildSettingsContext(Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new MainSettingsView(settingsContext));
        }

        [RequireSetup]
        [SlashCommand("rooms")]
        [Description("Define the channels (rooms) items can be listed in.")]
        public async Task<IResult> ServerRooms()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new MainShowroomView(settingsContext, emporium.Showrooms));
        }

        [RequireSetup]
        [SlashCommand("categories")]
        [Description("Add/Remove category options for this server.")]
        public async Task<IResult> ServerCategories()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new GuildCategoriesView(emporium.Categories, settingsContext));
        }

        [RequireSetup]
        [SlashCommand("currencies")]
        [Description("Add/Remove currency options for this server.")]
        public async Task<IResult> ServerCurrencies()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new GuildCurrenciesView(emporium.Currencies, settingsContext));
        }

        [RequireSetup]
        [SkipAuthentication]
        [SlashCommand("reset")]
        [Description("Clear all current settings for the bot.")]
        public async Task<IResult> ServerReset()
        {
            await Base.ExecuteAsync(new DeleteEmporiumCommand(new EmporiumId(Context.GuildId)));

            Cache.Clear(Context.GuildId);
            SettingsService.Clear(Context.GuildId);

            return Response(new LocalInteractionMessageResponse().WithContent("Server reset successful!").WithIsEphemeral(true));
        }
    }
}
