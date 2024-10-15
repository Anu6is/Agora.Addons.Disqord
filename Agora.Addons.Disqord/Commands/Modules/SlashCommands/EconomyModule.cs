using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Menus.View;
using Agora.Shared.EconomyFactory;
using Agora.Shared.Features.Queries;
using Agora.Shared.Persistence.Models;
using Agora.Shared.Persistence.Specifications;
using Agora.Shared.Persistence.Specifications.Filters;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Persistence.DataAccess;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireEconomy("AuctionBot")]
    public sealed class EconomyModule : AgoraModuleBase
    {
        private readonly IEconomy _economy;
        private readonly AuditLogService _auditLog;
        private readonly IDataAccessor _dataAccessor;

        public EconomyModule(EconomyFactoryService economyFactory, AuditLogService auditLog, IDataAccessor dataAccessor)
        {
            _economy = economyFactory.Create("AuctionBot");
            _auditLog = auditLog;
            _dataAccessor = dataAccessor;
        }

        [SlashCommand("leaderboard")]
        [Description("View the leaderboard")]
        public async Task<IResult> ViewLeaderboard()
        {
            await Deferral();

            var filter = new LeaderboardFilter(EmporiumId) { PageNumber = 1, IsPagingEnabled = true };

            var response = await Base.ExecuteAsync(new GetEmporiumLeaderboardQuery(filter));

            return View(new LeaderboardView(Settings, response));
        }

        [SlashCommand("balance")]
        [Description("Check current balance")]
        public async Task<IResult> ViewBalance([Description("The user to check the balance of"), RequireRole(AuthorizationRole.Administrator)] IMember user = null)
        {
            user ??= Context.Author;

            var cachedUser = await Cache.GetUserAsync(Context.GuildId, user.Id);
            var userBalance = await _economy.GetBalanceAsync(cachedUser.ToEmporiumUser(), Settings.DefaultCurrency);

            if (!userBalance.IsSuccessful)
                return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(userBalance.FailureReason));

            return Response(new LocalInteractionMessageResponse()
                    .AddEmbed(new LocalEmbed().WithColor(Color.Teal)
                                              .WithDescription($"Balance: {userBalance.Data}"))
                    .WithIsEphemeral());
        }


        [SlashCommand("give")]
        [RequireFeature(SettingsFlags.Donations, invert: true)]
        [Description("Give a portion of your money to another member")]
        public async Task<IResult> Donate(
            [Description("The amount of money to give"), RequireBalance(), Minimum(0)] double amount,
            [Description("The user to give money to"), RequireRole(AuthorizationRole.Buyer, author: false)] IMember user)
        {
            var donation = Money.Create((decimal)amount, Settings.DefaultCurrency);
            var donator = await Cache.GetUserAsync(Context.GuildId, Context.AuthorId);
            var receiver = await Cache.GetUserAsync(Context.GuildId, user.Id);


            var transactionResult = await Data.BeginTransactionAsync(async () =>
            {
                var result = await _economy.DecreaseBalanceAsync(donator.ToEmporiumUser(), donation);

                if (!result.IsSuccessful) return result;

                result = await _economy.IncreaseBalanceAsync(receiver.ToEmporiumUser(), donation);

                if (!result.IsSuccessful) return result;

                return result;
            });

            if (!transactionResult.IsSuccessful)
                ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(transactionResult.FailureReason));

            var message = $"{Context.Author.Mention} gave {user.Mention} **{donation}**";

            await LogActionAsync(message);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription(message)));
        }

        [RequireManager]
        [SlashCommand("add-money")]
        [Description("Add money to a user's balance")]
        public async Task<IResult> AddMoney(
            [Description("The amount of money to add"), Minimum(0)] double amount,
            [Description("The user to add money to")] IMember user)
        {
            var donation = Money.Create((decimal)amount, Settings.DefaultCurrency);
            var receiver = await Cache.GetUserAsync(Context.GuildId, user.Id);

            var result = await _economy.IncreaseBalanceAsync(receiver.ToEmporiumUser(), donation);

            if (!result.IsSuccessful)
                return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(result.FailureReason));

            var message = $"{Context.Author.Mention} added **{donation}** to {user.Mention}";

            await LogActionAsync(message);

            return Response(new LocalInteractionMessageResponse()
                    .AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription(message))
                    .WithIsEphemeral());
        }

        [SlashGroup("Bulk")]
        public sealed class EconomyBulkModule : AgoraModuleBase
        {
            private readonly IEconomy _economy;
            private readonly AuditLogService _auditLog;

            public EconomyBulkModule(EconomyFactoryService economyFactory, AuditLogService dataAccessor)
            {
                _economy = economyFactory.Create("AuctionBot");
                _auditLog = dataAccessor;
            }

            [RequireManager]
            [SlashCommand("add-money")]
            [Description("Add money to a user's balance")]
            public async Task<IResult> AddMoney(
                [Description("The amount of money to add"), Minimum(0)] double amount,
                [Description("Mention users to add money to")] string users)
            {
                var mentionedUsers = Mention.ParseUsers(users);

                if (mentionedUsers is null || !mentionedUsers.Any())
                    return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription("Please provide a list of users"));

                var donation = Money.Create((decimal)amount, Settings.DefaultCurrency);
                var successes = new List<string>();
                var failures = new List<string>();

                foreach (var user in mentionedUsers)
                {
                    var receiver = await Cache.GetUserAsync(Context.GuildId, user);
                    var result = await _economy.IncreaseBalanceAsync(receiver.ToEmporiumUser(), donation);

                    if (result.IsSuccessful)
                        successes.Add(Mention.User(user));
                    else
                        failures.Add($"{Mention.User(user)}: {result.FailureReason}");
                }

                var embeds = new List<LocalEmbed>();

                if (successes.Any())
                {
                    embeds.Add(new LocalEmbed().WithColor(Color.Teal)
                                               .WithTitle($"Successfully add {donation} to")
                                               .WithDescription(string.Join(", ", successes)));

                    await LogActionAsync($"{Context.Author.Mention} added **{donation}** to {string.Join(", ", successes)}");
                }
                else
                {
                    embeds.Add(new LocalEmbed().WithColor(Color.Red)
                                               .WithTitle($"Failed to add {donation} to")
                                               .WithDescription(string.Join(Environment.NewLine, failures)));
                }

                return Response(new LocalInteractionMessageResponse().WithEmbeds(embeds).WithIsEphemeral());
            }

            private async Task LogActionAsync(string message)
            {
                var logChannel = Settings.AuditLogChannelId;

                if (logChannel == 0) return;

                var embed = new LocalEmbed().WithAuthor(Context.Author).WithDescription(message).WithColor(Color.MediumPurple);

                await _auditLog.TrySendMessageAsync(logChannel, new LocalMessage().AddEmbed(embed));
            }
        }

        [RequireManager]
        [SlashCommand("remove-money")]
        [Description("Remove money from a user's balance")]
        public async Task<IResult> RemoveMoney(
            [Description("The amount of money to add"), Minimum(0)] double amount,
            [Description("The user to add money to")] IMember user)
        {
            var withdrawl = Money.Create((decimal)amount, Settings.DefaultCurrency);
            var member = await Cache.GetUserAsync(Context.GuildId, user.Id);

            var result = await _economy.DecreaseBalanceAsync(member.ToEmporiumUser(), withdrawl);

            if (!result.IsSuccessful)
                return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(result.FailureReason));

            var message = $"{Context.Author.Mention} removed **{withdrawl}** from {user.Mention}";

            await LogActionAsync(message);

            return Response(new LocalInteractionMessageResponse()
                    .AddEmbed(new LocalEmbed().WithColor(Color.Teal)
                                              .WithDescription(message))
                    .WithIsEphemeral());
        }

        [RequireManager]
        [SlashCommand("reset-balance")]
        [Description("Set a user's balance to zero")]
        public async Task<IResult> ResetBalance([Description("The user to reset")] IUser user)
        {
            var member = await Cache.GetUserAsync(Context.GuildId, user.Id);

            var result = await _economy.DeleteBalanceAsync(member.ToEmporiumUser(), Settings.DefaultCurrency);

            if (!result.IsSuccessful)
                return ErrorResponse(isEphimeral: true, embeds: new LocalEmbed().WithDescription(result.FailureReason));

            var message = $"{Context.Author.Mention} **reset** {user.Mention}";

            await LogActionAsync(message);

            return Response(new LocalInteractionMessageResponse()
                    .AddEmbed(new LocalEmbed().WithColor(Color.Teal)
                                              .WithDescription(message))
                    .WithIsEphemeral());
        }

        [RequireManager]
        [SlashCommand("reset-economy")]
        [Description("Set all balances zero")]
        public async Task<IResult> ResetEconomy()
        {
            var users = await _dataAccessor.Transaction<GenericRepository<DefaultEconomyUser>>().ListAsync(new EconomyUsersSpec(new EmporiumId(Context.GuildId)));

            await _dataAccessor.Transaction<GenericRepository<DefaultEconomyUser>>().DeleteRangeAsync(users);

            return Response(new LocalInteractionMessageResponse()
                    .AddEmbed(new LocalEmbed().WithColor(Color.Teal)
                                              .WithDescription("Economy reset"))
                    .WithIsEphemeral());
        }

        private async Task LogActionAsync(string message)
        {
            var logChannel = Settings.AuditLogChannelId;

            if (logChannel == 0) return;

            var embed = new LocalEmbed().WithAuthor(Context.Author).WithDescription(message).WithColor(Color.MediumPurple);

            await _auditLog.TrySendMessageAsync(logChannel, new LocalMessage().AddEmbed(embed));
        }
    }
}
