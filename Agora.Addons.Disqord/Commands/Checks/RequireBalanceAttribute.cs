using Agora.Shared.EconomyFactory;
using Disqord.Bot.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireBalanceAttribute : DiscordParameterCheckAttribute
    {
        public override bool CanCheck(IParameter parameter, object value) => value is double;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object amount)
        {
            var settings = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);

            if (settings == null) return Results.Failure("Setup Required: Please execute the `Server Setup` command.");

            var economy = context.Services.GetRequiredService<EconomyFactoryService>().Create(nameof(EconomyType.AuctionBot));
            var userBalance = await economy.GetBalanceAsync(EmporiumUser.Create(new EmporiumId(context.GuildId.Value), ReferenceNumber.Create(context.AuthorId)), settings.DefaultCurrency);

            if (userBalance.Value >= (decimal)amount)
                return Results.Success;
            else
                return Results.Failure("You lack the funds required to complete this transaction");
        }
    }
}
