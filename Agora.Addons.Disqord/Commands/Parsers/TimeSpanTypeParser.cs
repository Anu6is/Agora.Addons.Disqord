using Agora.Addons.Disqord.Parsers;
using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Humanizer;
using HumanTimeParser.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class TimeSpanTypeParser : DiscordGuildTypeParser<TimeSpan>
    {
        public override async ValueTask<ITypeParserResult<TimeSpan>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId);            
            var result = context.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(value.ToString());
                        
            if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult)
                return Failure("Invalid format provided");

            var duration = successfulResult.Value - emporium.LocalTime.DateTime;
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (Math.Round(duration.TotalSeconds, 0) < settings.MinimumDuration.TotalSeconds)
                return Failure($"The provided time is too short. Minimum duration is {settings.MinimumDuration.Humanize()}");

            if (Math.Round(duration.TotalSeconds, 0) > settings.MaximumDuration.TotalSeconds)
                return Failure($"The provided time is too long. Maximum duration is {settings.MaximumDuration.Humanize()}");

            return Success(duration);
        }
    }
}
