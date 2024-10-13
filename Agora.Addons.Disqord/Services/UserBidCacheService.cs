using Agora.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ZiggyCreatures.Caching.Fusion;

namespace Agora.Addons.Disqord
{
    public class UserBidCacheService : AgoraService
    {
        private const int CacheExpirationInSeconds = 1;

        private ConcurrentDictionary<ulong, CancellationTokenSource> Tokens { get; }
        private IFusionCache Cache { get; }

        public UserBidCacheService(IFusionCache cache, ILogger<UserBidCacheService> logger) : base(logger)
        {
            Tokens = new();
            Cache = cache;
        }

        public async ValueTask AddBidAsync(CachedBid userBid)
        {
            if (!Tokens.ContainsKey(userBid.User.ReferenceNumber.Value))
                Tokens.TryAdd(userBid.User.ReferenceNumber.Value, new CancellationTokenSource());

            await Cache.SetAsync($"user:{userBid.User.ReferenceNumber.Value}",
                                         userBid,
                                         TimeSpan.FromSeconds(CacheExpirationInSeconds),
                                         Tokens[userBid.User.ReferenceNumber.Value].Token);
            return;
        }

        public ValueTask<CachedBid> GetLastBidAsync(ulong userId)
        {
            var token = Tokens.GetValueOrDefault(userId)?.Token ?? CancellationToken.None;

            return ValueTask.FromResult(Cache.GetOrDefault<CachedBid>($"user:{userId}", token: token));
        }
    }
}
