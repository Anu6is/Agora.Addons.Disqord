using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Emporia;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Agora.Addons.Disqord
{
    public static class Startup
    {
        public static IHostBuilder CreateHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                   .UseSystemd()
#if DEBUG
                   .UseEnvironment("Development")
#endif
                   .ConfigureAppConfiguration(builder => builder.AddCommandLine(args).AddJsonFile("./tips.json", optional: true, reloadOnChange: true))
                   .ConfigureLogging((context, builder) => builder.AddSentry(context, UnhandledExceptionService.BeforeSend).ReplaceDefaultLogger().WithSerilog(context))
                   .ConfigureEmporiaServices()
                   .ConfigureDisqordCommands()
                   .UseEmporiaDiscordExtension()
                   .ConfigureCustomAgoraServices()
                   .ConfigureDiscordBot<AgoraBot>((context, bot) =>
                   {
                       bot.UseMentionPrefix = true;
                       bot.Status = UserStatus.Offline;
                       bot.Token = context.Configuration["Token:Discord"];
                       bot.ReadyEventDelayMode = ReadyEventDelayMode.Guilds;
                       bot.Intents = GatewayIntents.Guilds | GatewayIntents.Integrations;
                       bot.ServiceAssemblies = bot.ServiceAssemblies
                                                  .Append(Assembly.GetExecutingAssembly())
                                                  .Concat(context.Configuration.LoadCommandAssemblies()).ToList();
                   })
                    .UseDefaultServiceProvider(x =>
                    {
                        x.ValidateScopes = true;
                        x.ValidateOnBuild = true;
                    });
    }
}
