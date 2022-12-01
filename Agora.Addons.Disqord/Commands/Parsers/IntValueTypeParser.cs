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
            if (!int.TryParse(value.Span, out var number)) return Failure("Invalid value provided");

            var result = (TValueObject)typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { number });

            return Success(result);
        }
    }
}
