using Agora.Shared.Attributes;
using Agora.Shared.EconomyFactory;
using Agora.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Extension.Economies.RaidHelper;

[AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
public class RaidHelperEconomyProvider(ILogger<RaidHelperEconomyProvider> logger) : AgoraService(logger), IEconomyProvider
{
    public string EconomyType => "RaidHelper";

    public IEconomy CreateEconomy(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<RaidHelperEconomy>();
    }
}