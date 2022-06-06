using Disqord.Serialization.Json;
using Disqord.Serialization.Json.Default;
using Emporia.Domain.Common;
using Newtonsoft.Json.Linq;

namespace Agora.Addons.Disqord
{
    public class AgoraSubcategory : DefaultJsonNode, IJsonValue
    {
        public new JValue Token => base.Token as JValue;

        public object Value
        {
            get => Token.Value;
            set => Token.Value = value;
        }

        public AgoraSubcategory(string category) : base(JToken.FromObject(category), null) { }

        public override string ToString() => Token.ToString();

        public SubcategoryTitle ToDomainObject() => SubcategoryTitle.Create(Value.ToString());
    }
}
