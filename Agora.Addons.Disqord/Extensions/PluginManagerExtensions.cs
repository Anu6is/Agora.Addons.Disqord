using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Services;
using Emporia.Domain.Common;

namespace Agora.Addons.Disqord.Extensions
{
    public static class PluginManagerExtensions
    {
        public static async Task<bool> StoreBrokerDetailsAsync(this PluginManagerService pluginManager, EmporiumId emporiumId, ListingId listingId, ulong brokerId)
        {
            var parameters = new PluginParameters()
                {
                    { "EmporiumId", emporiumId },
                    { "ListingId", listingId },
                    { "BrokerId", brokerId }
                };

            var result = await pluginManager.ExecutePlugin("ListingBrokerManager", parameters);

            return result.IsSuccessful;
        }
    }
}
