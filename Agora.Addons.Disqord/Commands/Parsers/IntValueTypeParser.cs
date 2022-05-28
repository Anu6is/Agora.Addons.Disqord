using Disqord.Bot;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.TypeParsers
{
    public class IntValueTypeParser<TValueObject> : DiscordGuildTypeParser<TValueObject>
        where TValueObject : ValueObject
    {
        public override ValueTask<TypeParserResult<TValueObject>> ParseAsync(Parameter parameter, string value, DiscordGuildCommandContext context)
        {
            var result = (TValueObject)typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { int.Parse(value) });

            return Success(result);
        }
    }
}
