using Disqord.Bot;
using Disqord.Bot.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class IntValueTypeParser<TValueObject> : DiscordGuildTypeParser<TValueObject>
        where TValueObject : ValueObject
    {
        public override ValueTask<ITypeParserResult<TValueObject>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            var result = (TValueObject)typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { int.Parse(value.Span) });

            return Success(result);
        }
    }
}
