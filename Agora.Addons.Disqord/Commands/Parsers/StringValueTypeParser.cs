using Disqord.Bot.Commands;
using Emporia.Domain.Common;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class StringValueTypeParser<TValueObject> : DiscordGuildTypeParser<TValueObject> where TValueObject : ValueObject
    {
        private readonly int _maxLength;

        public StringValueTypeParser(int maxLength) => _maxLength = maxLength;

        public override ValueTask<ITypeParserResult<TValueObject>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (value.Length > _maxLength)
                return Failure($"The value is too long. The maximum length is {_maxLength} characters.");

            var result = (TValueObject)typeof(TValueObject).GetMethod("Create").Invoke(null, new object[] { value.ToString() });

            return Success(result);
        }
    }
}
