using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Interfaces;
using Emporia.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord.Services
{
    public class PluginManagerService
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly Dictionary<string, Type> _plugins = new();

        public PluginManagerService(IServiceScopeFactory scopeFactory, ILogger<PluginManagerService> logger)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public static void LoadPlugins(Type[] pluginTypes)
        {
            foreach (var pluginType in pluginTypes)
                _plugins.TryAdd(pluginType.Name, pluginType);
        }

        public async ValueTask<IResult> ExecutePlugin(string pluginType, PluginParameters parameters)
        {
            _logger.LogDebug("Executing plugin: {pluginName}", pluginType);

            _plugins.TryGetValue(pluginType, out var plugin);

            using var scope = _scopeFactory.CreateScope();
            var pluginExtension = scope.ServiceProvider.GetService(plugin) as IPluginExtension;
            var result = await pluginExtension.Execute(parameters);

            return result;
        }
    }
}
