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
using Disqord.Gateway;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using Emporia.Extensions.Discord.Features.MessageBroker;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using Qommon;
using static Disqord.Discord.Limits;

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
        [SlashGroup("room")]
        [Description("Manage server assigned showrooms")]
        public sealed class ShowroomCommands : AgoraModuleBase
        {
            public enum RoomType { Auction, Market = 2, Trade = 3, Giveaway = 4 }

            [SlashCommand("add")]
            [Description("Assign a new showroom (channel)")]
            public async Task<IResult> AddShowroom(
                [Description("Select the type of room")] RoomType showroom,
                [Description("Select a category channel")] Snowflake category, 
                [Description("Select a text/forum channel")] Snowflake channel)
            {
                try
                {
                    await Data.BeginTransactionAsync(async () =>
                    {
                        var room = await Base.ExecuteAsync(new CreateShowroomCommand(EmporiumId, new ShowroomId(channel), (ListingType)showroom));

                        var settings = (DefaultDiscordGuildSettings)Settings;

                        if (settings.AvailableRooms.Add(ListingType.Auction.ToString()))
                            await Base.ExecuteAsync(new UpdateGuildSettingsCommand(settings));
                    });
                 
                    return Response(new LocalInteractionMessageResponse().WithContent($"New {showroom} showroom registered - {Mention.Channel(channel)}").WithIsEphemeral());
                }
                catch (Exception ex)
                {
                    var message = ex switch
                    {
                        ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                        _ => "An error occured while processing this action. If this persists, please contact support."
                    };
                    return Response(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                }
            }

            [SlashCommand("remove")]
            [Description("Remove an existing showroom (channel)")]
            public async Task<IResult> RemoveShowroom(
                [Description("Select the type of room")] RoomType showroom,
                [Description("Select a category channel")] Snowflake category,
                [Description("Select a text/forum channel")] Snowflake channel)
            {
                try
                {
                    await Base.ExecuteAsync(new DeleteShowroomCommand(EmporiumId, new ShowroomId(channel), (ListingType)showroom));


                    return Response(new LocalInteractionMessageResponse().WithContent($"Removed {showroom} showroom - {Mention.Channel(channel)}").WithIsEphemeral());
                }
                catch (Exception ex)
                {
                    var message = ex switch
                    {
                        ValidationException validationException => string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}")),
                        _ => "An error occured while processing this action. If this persists, please contact support."
                    };
                    return Response(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                }
            }

            [AutoComplete("add")]
            [AutoComplete("remove")]
            public Task AutoComplete(AutoComplete<string> category, AutoComplete<string> channel)
            {
                var categoryChannels = Guild.GetChannels().Values.OfType<CachedCategoryChannel>();

                if (category.IsFocused)
                {
                    if (!categoryChannels.Any())
                        category.Choices.Add("No category channels found!", "0");
                    else if (category.RawArgument == string.Empty)
                        category.Choices.AddRange(categoryChannels.Select((x, index) => KeyValuePair.Create($"[{index + 1}] {x.Name}", x.Id.ToString()))
                                                                  .ToArray());
                    else
                        category.Choices.AddRange(categoryChannels.Where(x => x.Name.Contains(category.RawArgument, StringComparison.OrdinalIgnoreCase))
                                                                  .Select((x, index) => KeyValuePair.Create($"[{index + 1}] {x.Name}", x.Id.ToString()))
                                                                  .ToArray());
                }
                else if (channel.IsFocused)
                {
                    if (!category.Argument.TryGetValue(out var channelCategory))
                        channel.Choices.Add("Select a category to populate suggestions.", "0");
                    else
                    {
                        var textChannels = Guild.GetChannels().Values.OfType<ICategorizableGuildChannel>()
                                            .Where(x => x.CategoryId.ToString().Equals(channelCategory)
                                                     && (x.Type == ChannelType.Text || x.Type == ChannelType.News || x.Type == ChannelType.Forum));

                        if (!textChannels.Any())
                            channel.Choices.Add("No suitable channels exist in the selected category!", "0");
                        else if (channel.RawArgument == string.Empty)
                            channel.Choices.AddRange(textChannels.Select((x, index) => KeyValuePair.Create($"[{index + 1}] {x.Name}", x.Id.ToString()))
                                                                 .ToArray());
                        else
                            channel.Choices.AddRange(textChannels.Where(x => x.Name.Contains(channel.RawArgument, StringComparison.OrdinalIgnoreCase))
                                                                 .Select((x, index) => KeyValuePair.Create($"[{index + 1}] {x.Name}", x.Id.ToString()))
                                                                 .ToArray());
                    }
                }

                return Task.CompletedTask;
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
