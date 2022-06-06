using Disqord.Serialization.Json;
using Disqord.Serialization.Json.Default;
using Emporia.Domain.Common;
using Newtonsoft.Json.Linq;

namespace Agora.Addons.Disqord
{
    public class AgoraCategory : DefaultJsonNode, IJsonValue
    {
        public new JValue Token => base.Token as JValue;

        public object Value
        {
            get => Token.Value;
            set => Token.Value = value;
        }

        public AgoraCategory(string category) : base(JToken.FromObject(category), null) { }

        public override string ToString() => Token.ToString();

        public CategoryTitle ToDomainObject() => CategoryTitle.Create(Value.ToString());
    }
}
