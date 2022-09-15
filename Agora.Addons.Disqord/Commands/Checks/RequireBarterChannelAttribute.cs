using Agora.Shared;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    internal class RequireBarterChannelAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            if (context.Bot.GetChannel(context.GuildId, context.ChannelId) is not IThreadChannel channel)
                return Results.Failure("This command can only be used in a thread linked to an item.");

            var product = await context.Services.GetRequiredService<IEmporiaCacheService>().GetProductAsync(context.GuildId, channel.ChannelId, channel.Id);

            if (product == null) 
                return Results.Failure("This command can only be used in a thread linked to an item.");

            return Results.Success;
        }
    }
}
