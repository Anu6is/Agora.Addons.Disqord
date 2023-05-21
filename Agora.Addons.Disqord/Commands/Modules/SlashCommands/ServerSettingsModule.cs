using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Commands.Menus.View;
using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Menus;
using Agora.Addons.Disqord.Menus.View;
using Agora.Shared.EconomyFactory;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Emporia.Extensions.Discord.Features.MessageBroker;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [SlashGroup("server")]
    [Description("Configure Auction Bot for your server")]
    [RequireAuthorPermissions(Permissions.ManageGuild)]
    public sealed class ServerSettingsModule : AgoraModuleBase
    {
        [SkipAuthentication]
        [SlashCommand("setup")]
        [RequireUnregisteredServer]
        [Description("Setup the bot for use in your server.")]
        public async Task<IResult> ServerSetup(
            [Description("Log all sold/expired items to this channel.")][ChannelTypes(ChannelType.Text)]
            [RequireChannelPermissions(Permissions.SendMessages | Permissions.SendEmbeds)] IChannel resultLog = null,
            [Description("Log all item activity to this channel.")][ChannelTypes(ChannelType.Text)]
            [RequireChannelPermissions(Permissions.SendMessages | Permissions.SendEmbeds)] IChannel auditLog = null,
            [Description("Default currency symbol.")] string symbol = "$",
            [Description("Number of decimal places to show for prices.")] int decimalPlaces = 2,
            [Description("Current server time (24-Hour format | 15:30). Defaults to UTC")] Time serverTime = null)
        {
            await Deferral();

            DefaultDiscordGuildSettings settings = null;

            var resultLogId = resultLog == null ? 1 : resultLog.Id;

            await Data.BeginTransactionAsync(async () =>
            {
                var time = serverTime ?? Time.From(DateTimeOffset.UtcNow.TimeOfDay);
                var emporium = await Base.ExecuteAsync(new CreateEmporiumCommand(EmporiumId) { LocalTime = time });
                var currency = await Base.ExecuteAsync(new CreateCurrencyCommand(EmporiumId, symbol, decimalPlaces));

                settings = await Base.ExecuteAsync(new CreateGuildSettingsCommand(Context.GuildId, currency, resultLogId)
                {
                    AuditLogChannelId = auditLog?.Id ?? 0ul,
                    Economy = EconomyType.Disabled.ToString(),
                    TimeOffset = emporium.TimeOffset
                });

                await Context.Services.GetRequiredService<IMessageBroker>().TryRegisterAsync(emporium.Id);
                await SettingsService.AddGuildSettingsAsync(settings);
                await Cache.AddEmporiumAsync(emporium);
            });

            await Base.ExecuteAsync(new CreateEmporiumUserCommand(EmporiumId, ReferenceNumber.Create(Context.Author.Id)));

            var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, settings, Context.Services.CreateScope().ServiceProvider);
            var options = new List<GuildSettingsOption>() { };

            return View(new ListingsOptionsView(settingsContext, options));
        }

        [RequireSetup]
        [SlashCommand("settings")]
        [Description("Review and/or modify the current bot settings.")]
        public async Task<IResult> ServerSettings()
        {
            await Cache.GetEmporiumAsync(Context.GuildId);

            var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new MainSettingsView(settingsContext));
        }

        [RequireSetup]
        [SlashCommand("rooms")]
        [Description("Define the channels (rooms) items can be listed in.")]
        public async Task<IResult> ServerRooms()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new MainShowroomView(settingsContext, emporium.Showrooms));
        }

        [RequireSetup]
        [SlashCommand("categories")]
        [Description("Add/Remove category options for this server.")]
        public async Task<IResult> ServerCategories()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, Settings, Context.Services.CreateScope().ServiceProvider);

            return View(new GuildCategoriesView(emporium.Categories, settingsContext));
        }

        [RequireSetup]
        [SlashCommand("currencies")]
        [Description("Add/Remove currency options for this server.")]
        public async Task<IResult> ServerCurrencies()
        {
            var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
            var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, Settings, Context.Services.CreateScope().ServiceProvider);

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

        [RequireSetup]
        [SlashGroup("listing")]
        [Description("Configure rules for listings")]
        public sealed class ListingCommands : AgoraModuleBase
        {
            public enum Listing { Auction = 0, Market = 2, Trade = 3, Giveaway = 4 }

            [SlashCommand("requirements")]
            [Description("Configure which optional listing values are required during creation")]
            public async Task<IResult> ListingRequirements(Listing @for)
            {
                var requirements = await SettingsService.GetListingRequirementsAsync(Context.GuildId, (ListingType)@for);
                var settingsContext = new GuildSettingsContext(Context.AuthorId, Guild, Settings, Context.Services.CreateScope().ServiceProvider);

                return View(new ListingRequirementsView((DefaultListingRequirements)requirements, settingsContext));
            }
        }

        [RequireSetup]
        [SlashGroup("roles")]
        [Description("Manage bot specific server roles")]
        public sealed class BotRoleCommands : AgoraModuleBase
        {
            public enum BotRole { Buyer, Merchant, Broker, Manager }

            [SlashCommand("set")]
            [Description("Set a role to represent a Manager, Broker, Merchant or Buyer")]
            public async Task SetRole(BotRole botRole, IRole serverRole)
            {
                UpdateRole(botRole, serverRole.Id);

                await Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response(new LocalInteractionMessageResponse()
                        .AddEmbed(new LocalEmbed().WithDescription($"{botRole} set to {serverRole.Mention}").WithColor(Color.Teal))
                        .WithIsEphemeral());
            }

            [SlashCommand("clear")]
            [Description("Clear a previously set Manager, Broker, Merchant or Buyer role")]
            public async Task ClearRole(BotRole botRole)
            {
                UpdateRole(botRole, botRole <= BotRole.Merchant ? Context.GuildId : 0);

                await Base.ExecuteAsync(new UpdateGuildSettingsCommand((DefaultDiscordGuildSettings)Settings));

                await Response(new LocalInteractionMessageResponse()
                        .AddEmbed(new LocalEmbed().WithDescription($"{botRole} role cleared").WithDefaultColor())
                        .WithIsEphemeral());
            }

            private void UpdateRole(BotRole botRole, ulong value)
            {
                switch (botRole)
                {
                    case BotRole.Buyer:
                        Settings.BuyerRole = value;
                        break;
                    case BotRole.Merchant:
                        Settings.MerchantRole = value;
                        break;
                    case BotRole.Broker:
                        Settings.BrokerRole = value;
                        break;
                    case BotRole.Manager:
                        Settings.AdminRole = value;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
