using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using HumanTimeParser.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class DateTimeTypeParser : DiscordGuildTypeParser<DateTime>
    {
        public override async ValueTask<ITypeParserResult<DateTime>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId);
            var result = context.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(value.ToString());

            if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult)
                return Failure((result as IFailedTimeParsingResult)?.ErrorReason);

            return Success(successfulResult.Value);
        }
    }
}
