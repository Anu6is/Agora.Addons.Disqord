using Agora.Addons.Disqord.Common;
using Emporia.Domain.Services;

namespace Agora.Addons.Disqord.Interfaces
{
    public interface IPluginExtension
    {
        public ValueTask<IResult> Execute(PluginParameters parameters);
    }
}
