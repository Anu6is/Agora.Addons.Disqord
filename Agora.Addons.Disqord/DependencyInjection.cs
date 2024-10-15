using Agora.Addons.Disqord.Commands;
using Agora.Addons.Disqord.Interfaces;
using Agora.Addons.Disqord.Parsers;
using Agora.Addons.Disqord.Services;
using Agora.Shared.Attributes;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Emporia;
using Emporia.Application.Common;
using Emporia.Extensions.Discord;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qmmands;
using System.Collections.Immutable;
using System.Reflection;

namespace Agora.Addons.Disqord
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder ConfigureDisqordBotHost(this WebApplicationBuilder builder, string[] args)
        {
            builder.Host.ConfigureDisqordBotHost(args);

            return builder;
        }

        public static IHostBuilder ConfigureDisqordBotHost(this IHostBuilder builder, string[] args)
        {
            builder.UseSystemd()
                   .ConfigureAppConfiguration((context, builder) =>
                   {
                       builder.AddCommandLine(args)
                              .AddJsonFile($"./appsettings.{context.HostingEnvironment.EnvironmentName}.json", true)
                              .AddJsonFile("./tips.json", optional: true, reloadOnChange: true);
                   })
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

            return builder;
        }

        public static IHostBuilder ConfigureDisqordCommands(this IHostBuilder builder)
        {
            return builder.ConfigureServices((context, services) => services.AddDisqordCommands().AddAgoraServices(context.Configuration));
        }

        public static IServiceCollection AddDisqordCommands(this IServiceCollection services)
        {
            services.AddTransient<EmporiumTimeParser>();
            services.AddScoped<ILoggerContext, LoggerContext>();
            services.AddSingleton<ICommandRateLimiter, AgoraCommandRateLimiter>();

            return services;
        }

        public static IServiceCollection AddAgoraServices(this IServiceCollection services, IConfiguration configuration)
        {
            var types = Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsAssignableTo(typeof(AgoraService)) && !type.IsAbstract).ToImmutableArray();

            foreach (Type serviceType in types)
                services.AddAgoraService(serviceType);

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), ServiceLifetime.Scoped);
            services.AddMediatR(x => x.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()).Lifetime = ServiceLifetime.Scoped);

            var addons = configuration.GetSection("Addons").GetChildren().Select(x => x.Value + ".dll").ToArray();
            var addonAssemblies = addons.Select(name => Assembly.LoadFrom(name)).ToArray();

            services.AddMediatR(x => x.RegisterServicesFromAssemblies(addonAssemblies).Lifetime = ServiceLifetime.Scoped);

            services.AddScoped<PluginManagerService>();

            var pluginTypes = addonAssemblies.SelectMany(x => x.GetTypes()).ToArray();

            PluginManagerService.LoadPlugins(pluginTypes, services, configuration);

            foreach (Type pluginType in pluginTypes)
            {
                if (typeof(IPluginExtension).IsAssignableFrom(pluginType) && !pluginType.IsInterface && !pluginType.IsAbstract)
                {
                    services.AddTransient(pluginType);
                }

                if (pluginType.IsAssignableTo(typeof(AgoraService)) && !pluginType.IsAbstract)
                {
                    services.AddPluginService(pluginType);
                }
            }

            return services;
        }

        public static void AddPluginService(this IServiceCollection services, Type type)
        {
            ImmutableArray<Type> immutableArray = type.GetServiceInterfaces().ToImmutableArray();
            AgoraServiceAttribute.ServiceLifetime scope = type.GetCustomAttribute<AgoraServiceAttribute>()?.Scope ?? AgoraServiceAttribute.ServiceLifetime.Singleton;

            services.SetLifetime(scope, type);

            ImmutableArray<Type>.Enumerator enumerator = immutableArray.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Type current = enumerator.Current;
                services.SetLifetime(scope, current, type);
            }
        }

        private static IEnumerable<Type> GetServiceInterfaces(this Type type)
        {
            if (type.BaseType == null)
                return type.GetInterfaces();

            return type.GetInterfaces().Except(type.BaseType.GetInterfaces());
        }

        private static IServiceCollection SetLifetime(this IServiceCollection services, AgoraServiceAttribute.ServiceLifetime scope, Type serviceType, Type implementationType = null)
        {
            return scope switch
            {
                AgoraServiceAttribute.ServiceLifetime.Singleton => implementationType is null ? services.AddSingleton(serviceType) : services.AddSingleton(serviceType, implementationType),
                AgoraServiceAttribute.ServiceLifetime.Transient => implementationType is null ? services.AddTransient(serviceType) : services.AddTransient(serviceType, implementationType),
                AgoraServiceAttribute.ServiceLifetime.Scoped => implementationType is null ? services.AddScoped(serviceType) : services.AddScoped(serviceType, implementationType),
                _ => services,
            };
        }
    }
}
