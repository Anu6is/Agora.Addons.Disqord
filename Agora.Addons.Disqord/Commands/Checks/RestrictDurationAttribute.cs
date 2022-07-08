using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class RestrictDurationAttribute : DiscordParameterCheckAttribute
    {
        private double? _minDuration;
        private double? _maxDuration;

        public RestrictDurationAttribute() { }

        public RestrictDurationAttribute(double minSeconds, double maxSeconds)
        {
            _minDuration = minSeconds;
            _maxDuration = maxSeconds;
        }

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            var duration = (TimeSpan)argument;

            if (!_minDuration.HasValue)
            {
                var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);
                _minDuration = settings.MinimumDuration.TotalSeconds;
                _maxDuration = settings.MaximumDuration.TotalSeconds;
            }

            if (Math.Round(duration.TotalSeconds, 0) < _minDuration)
                return Results.Failure($"The provided time is too short. Minimum duration is {TimeSpan.FromSeconds(_minDuration.Value).Humanize()}");

            if (Math.Round(duration.TotalSeconds, 0) > _maxDuration)
                return Results.Failure($"The provided time is too long. Maximum duration is {TimeSpan.FromSeconds(_maxDuration.Value).Humanize()}");

            return Results.Success;
        }

        public override bool CanCheck(IParameter parameter, object value) => value is TimeSpan;
    }
}
