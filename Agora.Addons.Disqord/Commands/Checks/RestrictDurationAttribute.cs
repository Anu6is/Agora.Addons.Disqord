﻿using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public class RestrictDurationAttribute : DiscordParameterCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            var duration = (TimeSpan)argument;
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            var minDuration = settings.MinimumDuration.TotalSeconds;
            var maxDuration = settings.MaximumDuration.TotalSeconds;

            if (Math.Round(duration.Add(TimeSpan.FromSeconds(1)).TotalSeconds, 0) < minDuration)
                return Results.Failure($"The provided time is too short. Minimum duration is {TimeSpan.FromSeconds(minDuration).Humanize()}");

            if (Math.Round(duration.TotalSeconds, 0) > maxDuration)
                return Results.Failure($"The provided time is too long. Maximum duration is {TimeSpan.FromSeconds(maxDuration).Humanize()}");

            return Results.Success;
        }

        public override bool CanCheck(IParameter parameter, object value) => value is TimeSpan;
    }
}
