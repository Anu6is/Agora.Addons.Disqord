using Agora.Addons.Disqord.Parsers;
using Agora.Addons.Disqord.TypeParsers;
using Agora.Shared.Extensions;
using Agora.Shared.Services;
using Emporia.Domain.Common;
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
            var commandService = new CommandService();

            commandService.AddTypeParser(new IntValueTypeParser<Stock>());
            commandService.AddTypeParser(new StringValueTypeParser<ProductTitle>());
            commandService.AddTypeParser(new StringValueTypeParser<HiddenMessage>());
            commandService.AddTypeParser(new StringValueTypeParser<CategoryTitle>());
            commandService.AddTypeParser(new StringValueTypeParser<SubcategoryTitle>());
            commandService.AddTypeParser(new StringValueTypeParser<ProductDescription>());
            commandService.AddTypeParser(new DateTimeTypeParser());
            commandService.AddTypeParser(new TimeSpanTypeParser());

            services.AddSingleton(commandService);
            services.AddTransient<EmporiumTimeParser>();
            
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
