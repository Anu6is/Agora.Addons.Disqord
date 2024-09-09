using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Interfaces;
using Believe.Net;
using Emporia.Domain.Services;
using Extension.Economies.RaidHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Extension.Economies;

public class Plugins : IInjectablePluginExtension
{
    public IServiceCollection Configure(IServiceCollection services, IConfiguration configuration)
    {
        var unbelievaClientConfig = new UnbelievaClientConfig() { Token = configuration["Token:UnbelievaBoat"] };

        services.AddSingleton(unbelievaClientConfig);
        services.AddSingleton<UnbelievaClient>();

        services.AddHttpClient(RaidHelperClient.SectionName);
        services.AddSingleton<RaidHelperClient>();

        return services;
    }

    public ValueTask<IResult> Execute(PluginParameters parameters)
    {
        throw new NotImplementedException();
    }
}
