using Agora.Shared.Attributes;
using Agora.Shared.EconomyFactory;
using Believe.Net;
using Emporia.Domain.Common;
using Emporia.Domain.Services;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Extension.Economies.UnbelievaBoat
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class UnbelievaBoatEconomy : EconomyService
    {
        private readonly UnbelievaClient _unbelievaClient;

        public UnbelievaBoatEconomy(UnbelievaClient client, ILogger<UnbelievaBoatEconomy> logger) : base(logger)
        {
            _unbelievaClient = client;
        }

        public override async ValueTask<IResult<Money>> GetBalanceAsync(IEmporiumUser user, Currency currency)
        {
            var userBalance = await _unbelievaClient.GetUserBalanceAsync(user.EmporiumId.Value, user.ReferenceNumber.Value);

            if (userBalance == null) return Result<Money>.Failure("Unable to verify user balance");
            if (userBalance.IsRateLimited) return Result<Money>.Failure($"UnbelievaBoat transaction processing is on cooldown. Retry after {userBalance.RetryAfter.Humanize()}");

            return Result.Success(Money.Create(ParseToDecimal(userBalance.Cash < 0 ? userBalance.Total : userBalance.Bank), currency));
        }

        public override async ValueTask<IResult> SetBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var userBalance = await _unbelievaClient.SetUserBankAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (userBalance.IsRateLimited) return Result<Money>.Failure($"UnbelievaBoat transaction processing is on cooldown. Retry after {userBalance.RetryAfter.Humanize()}");

            return Result.Success();
        }

        public override async ValueTask<IResult> DeleteBalanceAsync(IEmporiumUser user, Currency currency, string reason = "")
        {
            var userBalance = await _unbelievaClient.SetUserBankAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, 0, reason);

            if (userBalance.IsRateLimited) return Result<Money>.Failure($"UnbelievaBoat transaction processing is on cooldown. Retry after {userBalance.RetryAfter.Humanize()}");

            return Result.Success();
        }

        public override async ValueTask<IResult<Money>> IncreaseBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var userBalance = await _unbelievaClient.IncreaseUserBankAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (userBalance == null)
            {
                var result = await CheckEconomyAccess(user);

                if (!result.IsSuccessful) return result;
            }

            if (userBalance.IsRateLimited)
            {
                await Task.Delay(userBalance.RetryAfter);

                var result = await IncreaseBalanceAsync(user, amount, reason);

                if (!result.IsSuccessful) return result;
            }

            return Result.Success(Money.Create(ParseToDecimal(userBalance.Total), amount.Currency));
        }

        public override async ValueTask<IResult<Money>> DecreaseBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var userBalance = await _unbelievaClient.DecreaseUserBankAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (userBalance == null)
            {
                var result = await CheckEconomyAccess(user);

                if (!result.IsSuccessful) return result;
            }

            if (userBalance.IsRateLimited)
                return Result<Money>.Failure($"UnbelievaBoat transaction processing is on cooldown. Retry after {userBalance.RetryAfter.Humanize()}");

            return Result.Success(Money.Create(ParseToDecimal(userBalance.Total), amount.Currency));
        }

        private async ValueTask<IResult<Money>> CheckEconomyAccess(IEmporiumUser user)
        {
            var economyAccess = await _unbelievaClient.HasPermissionAsync(user.EmporiumId.Value, ApplicationPermission.EditEconomy);

            if (!economyAccess)
                return Result<Money>.Failure("Auction Bot needs to be authorized to use UnbelivaBoat economy in this server!");

            return Result<Money>.Failure("Unable to validate user's UnbelievaBoat balance");
        }

        private static decimal ParseToDecimal(double value)
        {
            if (double.IsInfinity(value)) return decimal.MaxValue;
            if (double.IsNaN(value)) return decimal.MinValue;

            try
            {
                return Convert.ToDecimal(value);
            }
            catch (Exception)
            {
                if (value > 0)
                    return decimal.MaxValue;
                else
                    return decimal.MinValue;
            }
        }
    }
}
