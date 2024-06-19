using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agora.Addons.Disqord
{
    public static class Startup
    {
#if DEBUG
        private const string EnvName = "Development";
#else
        private const string EnvName = "Production";
#endif

        public static IHost CreateGenericHost(string[] args)
            => Host.CreateDefaultBuilder(args)
                   .ConfigureAppConfiguration((context, config) => context.HostingEnvironment.EnvironmentName = EnvName)
                   .ConfigureDisqordBotHost(args)
                   .Build();

        public static WebApplication CreateWebHost(string[] args, Action<WebHostBuilderContext, WebApplicationBuilder> configureWebApplicationBuilder = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = EnvName });

            Action<WebHostBuilderContext, WebApplicationBuilder> builderConfiguration = configureWebApplicationBuilder;

            builder.WebHost.ConfigureServices(delegate (WebHostBuilderContext context, IServiceCollection services)
            {
                builderConfiguration?.Invoke(context, builder);
            });

            builder.ConfigureDisqordBotHost(args);

            return builder.Build();
        }
    }
}
