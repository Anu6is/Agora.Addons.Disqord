using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    internal class RestrictTimeoutAttribute : DiscordParameterCheckAttribute
    {
        private readonly double _minDuration;
        private double _maxDuration;

        public RestrictTimeoutAttribute(double minSeconds, double maxSeconds)
        {
            _minDuration = minSeconds;
            _maxDuration = maxSeconds;
        }

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            var duration = (TimeSpan)argument;
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);
            
            if (settings == null) return Results.Failure("Setup Required: Please execute the `Server Setup` command.");
            
            _maxDuration = Math.Min(_maxDuration, settings.MaximumDuration.TotalSeconds);

            if (Math.Round(duration.Add(TimeSpan.FromSeconds(1)).TotalSeconds, 0) < _minDuration)
                return Results.Failure($"The provided time is too short. Minimum duration is {TimeSpan.FromSeconds(_minDuration).Humanize()}");

            if (Math.Round(duration.TotalSeconds, 0) > _maxDuration)
                return Results.Failure($"The provided time is too long. Maximum duration is {TimeSpan.FromSeconds(_maxDuration).Humanize()}");

            return Results.Success;
        }

        public override bool CanCheck(IParameter parameter, object value) => value is TimeSpan;
    }
}
