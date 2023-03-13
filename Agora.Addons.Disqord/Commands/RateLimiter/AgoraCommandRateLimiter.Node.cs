using Qmmands;
using Qommon;
using Qommon.Collections;
using Qommon.Collections.ThreadSafe;

namespace Agora.Addons.Disqord.Commands
{
    public partial class AgoraCommandRateLimiter
    {
        public class Node
        {
            public AgoraCommandRateLimiter RateLimiter { get; }

            public IReadOnlyList<RateLimitAttribute> RateLimits { get; }

            public IThreadSafeDictionary<object, Bucket> Buckets { get; }

            public Node(AgoraCommandRateLimiter rateLimiter, IEnumerable<RateLimitAttribute> rateLimits)
            {
                RateLimiter = rateLimiter;
                RateLimits = rateLimits.GetArray();
                Buckets = ThreadSafeDictionary.ConcurrentDictionary.Create<object, Bucket>();
            }

            public virtual async ValueTask<IResult> RateLimit(ICommandContext context)
            {
                ClearExpiredBuckets();

                var rateLimits = RateLimits;
                var rateLimitCount = rateLimits.Count;
                var buckets = new Bucket[rateLimitCount];
                for (var i = 0; i < rateLimitCount; i++)
                {
                    var rateLimit = rateLimits[i];
                    var bucket = await GetBucket(context, rateLimit);
                    buckets[i] = bucket;
                }

                Dictionary<RateLimitAttribute, TimeSpan> rateLimitedBuckets = null;
                foreach (var bucket in buckets)
                {
                    if (bucket != null && bucket.IsRateLimited(out var retryAfter))
                        (rateLimitedBuckets ??= new(buckets.Length)).Add(bucket.RateLimit, retryAfter);
                }

                if (rateLimitedBuckets != null && rateLimitedBuckets.Count > 0)
                    return new CommandRateLimitedResult(rateLimitedBuckets);

                foreach (var bucket in buckets)
                    bucket?.Decrement();

                return Results.Success;
            }

            protected virtual void ClearExpiredBuckets()
            {
                var now = DateTimeOffset.UtcNow;
                var buckets = Buckets.ToArray();
                foreach (var bucket in buckets)
                {
                    if (now > bucket.Value.LastCall + bucket.Value.RateLimit.Window)
                        Buckets.TryRemove(bucket.Key, out _);
                }
            }

            protected virtual async ValueTask<Bucket> GetBucket(ICommandContext context, RateLimitAttribute rateLimit)
            {
                Guard.IsNotNull(RateLimiter.BucketKeyGenerator);

                var key = RateLimiter.BucketKeyGenerator(context, rateLimit.BucketType);

                if (key == null) return null;

                if (Buckets.TryGetValue(key, out var bucket))
                    return bucket;

                bucket = await RateLimiter.CreateBucketAsync(context, rateLimit);

                Buckets.TryAdd(key, bucket);
                
                return bucket;
            }
        }
    }
}
