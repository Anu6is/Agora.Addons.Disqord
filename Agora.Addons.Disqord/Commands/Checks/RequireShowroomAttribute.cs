using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public class RequireShowroomAttribute : DiscordGuildCheckAttribute
    {
        private readonly string _roomType;

        public RequireShowroomAttribute(string roomType) => _roomType = roomType;

        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            if (context is IDiscordApplicationCommandContext cmdContext && cmdContext.Interaction.Type == InteractionType.ApplicationCommandAutoComplete) return Results.Success;

            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Results.Failure("Setup Required: No showrooms have been configured for this server.");

            var alias = (context.Command as ApplicationCommand).Alias;
            var listingType = $"{alias} {_roomType}";
            var allow = alias.Equals(_roomType, StringComparison.OrdinalIgnoreCase);

            if (!allow && !settings.AllowedListings.Any(listing => listing.Equals(listingType, StringComparison.OrdinalIgnoreCase)))
                return Results.Failure($"{listingType.Pascalize()} Listings are not allowed.{Environment.NewLine}Configure Allowed Listings using the </server settings:1013361602499723275> command.");

            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId);
            var showrooms = emporium.Showrooms.Where(x => x.ListingType.Equals(_roomType, StringComparison.OrdinalIgnoreCase));

            if (context.Bot.GetChannel(context.GuildId, context.ChannelId) is not ICategorizableGuildChannel channel)
                return Results.Failure($"Command must be executed in a {_roomType} Room:{Environment.NewLine}{string.Join(" | ", showrooms.Select(x => Mention.Channel(x.Id.Value)))}");

            if (showrooms.Any(x => x.Id.Value.Equals(context.ChannelId)
                                || x.Id.Value.Equals(channel.CategoryId.GetValueOrDefault())
                                || channel is IThreadChannel threadchannel && x.Id.Value.Equals(threadchannel.ChannelId))) return Results.Success;

            return Results.Failure($"Command must be executed in a {_roomType} Room:{Environment.NewLine}{string.Join(" | ", showrooms.Select(x => Mention.Channel(x.Id.Value)))}");
        }
    }
}
