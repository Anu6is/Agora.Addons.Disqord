using Agora.Addons.Disqord.Common;
using Emporia.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Interfaces
{
    public interface IPluginExtension
    {
        public ValueTask<IResult> Execute(PluginParameters parameters);
    }

    public interface IInjectablePluginExtension : IPluginExtension
    {
        public IServiceCollection Configure(IServiceCollection services, IConfiguration configuration);
    }
}
