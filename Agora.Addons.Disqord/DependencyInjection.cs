using Agora.Addons.Disqord.Commands;
using Agora.Addons.Disqord.Parsers;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qmmands;
using System.Collections.Immutable;
using System.Reflection;

namespace Agora.Addons.Disqord
{
    public static class DependencyInjection
    {
        public static IHostBuilder ConfigureDisqordCommands(this IHostBuilder builder)
        {
            return builder.ConfigureServices((context, services) => services.AddDisqordCommands().AddAgoraServices());
        }

        public static IServiceCollection AddDisqordCommands(this IServiceCollection services)
        {
            services.AddTransient<EmporiumTimeParser>();
            services.AddSingleton<ICommandRateLimiter, AgoraCommandRateLimiter>();

            return services;
        }

        public static IServiceCollection AddAgoraServices(this IServiceCollection services)
        {
            var types = Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsAssignableTo(typeof(AgoraService)) && !type.IsAbstract).ToImmutableArray();

            foreach (Type serviceType in types)
                services.AddAgoraService(serviceType);

            return services;
        }
    }
}
