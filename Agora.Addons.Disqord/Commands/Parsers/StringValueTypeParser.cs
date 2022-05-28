using Disqord.Bot;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.TypeParsers
{
    public class StringValueTypeParser<TValueObject> : DiscordGuildTypeParser<TValueObject> where TValueObject : ValueObject
    {
        private readonly int _maxLength;

        public StringValueTypeParser(int maxLength) => _maxLength = maxLength;

        public override ValueTask<TypeParserResult<TValueObject>> ParseAsync(Parameter parameter, string value, DiscordGuildCommandContext context)
        {
            if (value.Length > _maxLength)
                return Failure($"The value is too long. The maximum length is {_maxLength} characters.");

            var result = (TValueObject) typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { value });

            return Success(result);
        }
    }
}
