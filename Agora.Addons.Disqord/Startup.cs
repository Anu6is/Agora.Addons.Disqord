using Agora.API;
using Microsoft.AspNetCore.Builder;
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
        
        public static WebApplication CreateWebHost(string[] args)
            => WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = EnvName })
                   .ConfigureAgoraAPI()
                   .ConfigureDisqordBotHost(args)
                   .Build()
                   .ConfigureApiApplication();


    }
}
