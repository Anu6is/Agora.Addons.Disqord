using Disqord.Bot.Commands;
using Disqord.Serialization.Json;
using Qmmands;

namespace Agora.Addons.Disqord.Parsers
{
    public class JsonValueTypeParser<T> : DiscordGuildTypeParser<T> where T : IJsonValue
    {
        private readonly int _maxLength;

        public JsonValueTypeParser(int maxLength) => _maxLength = maxLength;

        public override ValueTask<ITypeParserResult<T>> ParseAsync(IDiscordGuildCommandContext context, IParameter parameter, ReadOnlyMemory<char> value)
        {
            if (value.Length > _maxLength)
                return Failure($"The value is too long. The maximum length is {_maxLength} characters.");

            var result = (T)Activator.CreateInstance(typeof(T), new object[] { value.ToString() });

            return Success(result);
        }
    }
}
