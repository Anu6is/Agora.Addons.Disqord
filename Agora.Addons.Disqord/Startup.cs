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
                   .ConfigureAppConfiguration(builder => builder.AddCommandLine(args))
                   .ConfigureLogging((context, builder) => builder.AddSentry(context).ReplaceDefaultLogger().WithSerilog(context))
                   .ConfigureEmporiaServices()
                   .ConfigureDisqordCommands()
                   .UseEmporiaDiscordExtension()
                   .ConfigureCustomAgoraServices()
                   .ConfigureDiscordBotSharder<AgoraBot>((context, bot) =>
                   {
                       bot.UseMentionPrefix = true;
                       bot.Status = UserStatus.Offline;
                       bot.Token = context.Configuration["Discord:Token"];
                       bot.ServiceAssemblies.Add(Assembly.GetExecutingAssembly());
                       bot.Intents = GatewayIntent.Guilds | GatewayIntent.Integrations
                                   | GatewayIntent.GuildMessages | GatewayIntent.GuildReactions
                                   | GatewayIntent.DirectMessages | GatewayIntent.DirectReactions;
                   })
                    .UseDefaultServiceProvider(x =>
                    {
                        x.ValidateScopes = true;
                        x.ValidateOnBuild = true;
                    });
    }
}
