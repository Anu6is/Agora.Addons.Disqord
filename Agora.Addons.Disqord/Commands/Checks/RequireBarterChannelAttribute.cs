using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    internal class RequireBarterChannelAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            CachedEmporiumProduct product = null;

            var cache = context.Services.GetRequiredService<IEmporiaCacheService>();
            var commandChannel = context.Bot.GetChannel(context.GuildId, context.ChannelId);
            
            if (commandChannel is ITextChannel textChannel)
            {
                var emporium = await cache.GetEmporiumAsync(context.GuildId);

                if (emporium == null)
                {
                    var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

                    if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

                    await cache.RemoveEmporiumAsync(context.GuildId);

                    return Results.Failure("Unable to complete this action. Please try again."); 
                }

                if (!emporium.Showrooms.Any(x => x.Id.Value.Equals(textChannel.CategoryId.GetValueOrDefault().RawValue)))
                    return Results.Failure("This command can only be used in a thread linked to an item.");

                var pinnedMessages = await textChannel.FetchPinnedMessagesAsync();
                var productMessage = pinnedMessages.FirstOrDefault(x =>
                {
                    return x.Author.Id.Equals(context.Bot.CurrentUser.Id) 
                        && x.Embeds.FirstOrDefault().Footer.Text.StartsWith("Reference Code:");
                });

                product = await cache.GetProductAsync(context.GuildId, productMessage.ChannelId, productMessage.Id, uniqueRoom: true);
            }
            else if (commandChannel is IThreadChannel thread)
            {
                if (thread.GetChannel() is IForumChannel)
                    product = await cache.GetProductAsync(context.GuildId, thread.Id, thread.Id);
                else
                    product = await cache.GetProductAsync(context.GuildId, thread.ChannelId, thread.Id);
            }

            if (product == null) 
                return Results.Failure("This command can only be used in a thread/channel linked to an item.");

            return Results.Success;
        }
    }
}
