using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    public partial class AgoraCommandRateLimiter
    {
        public class Bucket
        {
            public RateLimitAttribute RateLimit { get; }

            public int Uses { get; protected set; }

            public DateTimeOffset Window { get; protected set; }

            public DateTimeOffset LastCall { get; protected set; }

            public Bucket(RateLimitAttribute rateLimit)
            {
                RateLimit = rateLimit;
                Uses = rateLimit.Uses;
            }

            public virtual bool IsRateLimited(out TimeSpan retryAfter)
            {
                lock (this)
                {
                    var now = DateTimeOffset.UtcNow;
                    LastCall = now;

                    if (Uses == RateLimit.Uses)
                        Window = now;

                    if (now > Window + RateLimit.Window)
                    {
                        Uses = RateLimit.Uses;
                        Window = now;
                    }

                    if (Uses == 0)
                    {
                        retryAfter = RateLimit.Window - (now - Window);
                        return true;
                    }

                    retryAfter = default;
                    return false;
                }
            }

            public void ReloadState(int uses, DateTimeOffset lastCall)
            {
                Uses = uses;
                Window = lastCall;
                LastCall = lastCall;
            }

            public virtual void Decrement()
            {
                lock (this)
                {
                    var now = DateTimeOffset.UtcNow;
                    Uses--;

                    if (Uses == 0)
                        Window = now;
                }
            }

            public virtual void Reset()
            {
                lock (this)
                {
                    Uses = RateLimit.Uses;
                    LastCall = default;
                    Window = default;
                }
            }
        }
    }
}
