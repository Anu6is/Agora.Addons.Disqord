using Disqord.Bot.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class TimeTypeParser : DiscordGuildTypeParser<Time>
    {
        public override ValueTask<ITypeParserResult<Time>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            try
            {
                var time = Time.From(value.ToString());
                return Success(time);
            }
            catch (Exception ex)
            {
                return Failure(ex.Message);
            }
        }
    }
}
