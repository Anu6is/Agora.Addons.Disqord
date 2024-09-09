using Agora.Shared.Attributes;
using Agora.Shared.EconomyFactory;
using Emporia.Domain.Common;
using Emporia.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Extension.Economies.RaidHelper
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
    public class RaidHelperEconomy : EconomyService
    {
        private readonly RaidHelperClient _raidHelperClient;

        public RaidHelperEconomy(RaidHelperClient client, ILogger<RaidHelperEconomy> logger) : base(logger)
        {
            _raidHelperClient = client;
        }

        public override async ValueTask<IResult<Money>> GetBalanceAsync(IEmporiumUser user, Currency currency)
        {
            var entity = await _raidHelperClient.GetUserBalanceAsync(user.EmporiumId.Value, user.ReferenceNumber.Value);

            if (!entity.IsSuccessful) return Result<Money>.Failure("Unable to verify DKP balance");

            _ = decimal.TryParse(entity.Data.Dkp, out var dkp);

            return Result.Success(Money.Create(dkp, currency));
        }

        public override async ValueTask<IResult<Money>> IncreaseBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var entity = await _raidHelperClient.IncreaseUserBalanceAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (!entity.IsSuccessful) return Result<Money>.Failure("Failed to increase DKP balance");

            _ = decimal.TryParse(entity.Data.Dkp, out var dkp);

            return Result.Success(Money.Create(dkp, amount.Currency));
        }

        public override async ValueTask<IResult<Money>> DecreaseBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var entity = await _raidHelperClient.DecreaseUserBalanceAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (!entity.IsSuccessful) return Result<Money>.Failure("Failed to decrease DKP balance");

            _ = decimal.TryParse(entity.Data.Dkp, out var dkp);

            return Result.Success(Money.Create(dkp, amount.Currency));
        }

        public override async ValueTask<IResult> SetBalanceAsync(IEmporiumUser user, Money amount, string reason = "")
        {
            var result = await _raidHelperClient.SetUserBalanceAsync(user.EmporiumId.Value, user.ReferenceNumber.Value, amount.Value, reason);

            if (!result.IsSuccessful) return Result.Failure("Failed to set DKP balance");

            return Result.Success();
        }
    }
}
