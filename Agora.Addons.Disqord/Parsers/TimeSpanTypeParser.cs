using Agora.Addons.Disqord.Parsers;
using Disqord.Bot;
using Emporia.Extensions.Discord;
using HumanTimeParser.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.TypeParsers
{
    public class TimeSpanTypeParser : DiscordTypeParser<TimeSpan>
    {
        public override async ValueTask<TypeParserResult<TimeSpan>> ParseAsync(Parameter parameter, string value, DiscordCommandContext context)
        {
            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId.Value);            
            var result = context.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(value);
                        
            if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult)
                return Failure("Invalid format provided");

            return Success(successfulResult.Value - emporium.LocalTime.DateTime);
        }
    }
}
