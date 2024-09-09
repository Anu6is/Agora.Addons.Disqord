using Agora.Shared.Attributes;
using Agora.Shared.EconomyFactory;
using Agora.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extension.Economies.UnbelievaBoat;

[AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
public class UnbelievaBoatEconomyProvider(ILogger<UnbelievaBoatEconomyProvider> logger) : AgoraService(logger), IEconomyProvider
{
    public string EconomyType => "UnbelievaBoat";

    public IEconomy CreateEconomy(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<UnbelievaBoatEconomy>();
    }
}