using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
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
                return Failure("Invalid format provided: Expected format 5s | 10m | 2h | 7d");

            var duration = successfulResult.Value - emporium.LocalTime.DateTime;

            return Success(duration);
        }
    }
}