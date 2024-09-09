using Agora.Shared.EconomyFactory;
using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public class RequireBalanceAttribute : DiscordParameterCheckAttribute
    {
        public override bool CanCheck(IParameter parameter, object value) => value is double;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object amount)
        {
            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            var economy = context.Services.GetRequiredService<EconomyFactoryService>().Create("AuctionBot");
            var user = await context.Services.GetRequiredService<IEmporiaCacheService>().GetUserAsync(context.GuildId.Value, context.AuthorId);
            var userBalance = await economy.GetBalanceAsync(user.ToEmporiumUser(), settings.DefaultCurrency);

            if (!userBalance.IsSuccessful) return Results.Failure(userBalance.FailureReason);

            if (decimal.TryParse(amount.ToString(), out var result) && userBalance.Data.Value >= result)
                return Results.Success;
            else
                return Results.Failure("You lack the funds required to complete this transaction");
        }
    }
}
