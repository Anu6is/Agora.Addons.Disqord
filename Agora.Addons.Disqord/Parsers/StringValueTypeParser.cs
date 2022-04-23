using Disqord.Bot;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.TypeParsers
{
    public class StringValueTypeParser<TValueObject> : DiscordGuildTypeParser<TValueObject>
        where TValueObject : ValueObject
    {
        public override ValueTask<TypeParserResult<TValueObject>> ParseAsync(Parameter parameter, string value, DiscordGuildCommandContext context)
        {
            var result = (TValueObject) typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { value });

            return Success(result);
        }
    }
}
