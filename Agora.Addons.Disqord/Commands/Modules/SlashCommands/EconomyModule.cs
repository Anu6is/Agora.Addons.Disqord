using Agora.Addons.Disqord.Checks;
using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Menus.View;
using Agora.Shared.EconomyFactory;
using Agora.Shared.Persistence.Models;
using Agora.Shared.Features.Queries;
using Agora.Shared.Persistence.Specifications;
using Agora.Shared.Persistence.Specifications.Filters;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Persistence.DataAccess;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    [RequireSetup]
    [RequireEconomy(nameof(EconomyType.AuctionBot))]
    public sealed class EconomyModule : AgoraModuleBase
    {
        private readonly IEconomy _economy;
        private readonly IDataAccessor _dataAccessor;

        public EconomyModule(EconomyFactoryService economyFactory, IDataAccessor dataAccessor)
        {
            _economy = economyFactory.Create(nameof(EconomyType.AuctionBot));
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

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription($"Balance: {userBalance}")).WithIsEphemeral());
        }


        [SlashCommand("give")]
        [Description("Give a portion of your money to another member")]
        public async Task<IResult> Donate(
            [Description("The amount of money to give"), RequireBalance(), Minimum(0)] double amount,
            [Description("The user to give money to"), RequireRole(AuthorizationRole.Buyer, author: false)] IMember user)
        {
            var donation = Money.Create((decimal)amount, Settings.DefaultCurrency);
            var donator = await Cache.GetUserAsync(Context.GuildId, Context.AuthorId);
            var receiver = await Cache.GetUserAsync(Context.GuildId, user.Id);

            await _economy.DecreaseBalanceAsync(donator.ToEmporiumUser(), donation);
            await _economy.IncreaseBalanceAsync(receiver.ToEmporiumUser(), donation);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription($"{Context.Author.Mention} gave {user.Mention} {donation}")));
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

            await _economy.IncreaseBalanceAsync(receiver.ToEmporiumUser(), donation);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription($"{Context.Author.Mention} added {donation} to {user.Mention}")));

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

            await _economy.DecreaseBalanceAsync(member.ToEmporiumUser(), withdrawl);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription($"{Context.Author.Mention} removed {withdrawl} from {user.Mention}")));

        }

        [RequireManager]
        [SlashCommand("reset-balance")]
        [Description("Set a user's balance to zero")]
        public async Task<IResult> ResetBalance([Description("The user to reset")] IMember user)
        {
            var member = await Cache.GetUserAsync(Context.GuildId, user.Id);

            await _economy.DeleteBalanceAsync(member.ToEmporiumUser(), Settings.DefaultCurrency);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription($"{Context.Author.Mention} reset {user.Mention}")));

        }

        [RequireManager]
        [SlashCommand("reset-economy")]
        [Description("Set all balances zero")]
        public async Task<IResult> ResetEconomy()
        {
            var users = await _dataAccessor.Transaction<GenericRepository<DefaultEconomyUser>>().ListAsync(new EconomyUsersSpec(new EmporiumId(Context.GuildId)));

            await _dataAccessor.Transaction<GenericRepository<DefaultEconomyUser>>().DeleteRangeAsync(users);

            return Response(new LocalInteractionMessageResponse().AddEmbed(new LocalEmbed().WithColor(Color.Teal).WithDescription("Economy reset")).WithIsEphemeral());

        }
    }
}
