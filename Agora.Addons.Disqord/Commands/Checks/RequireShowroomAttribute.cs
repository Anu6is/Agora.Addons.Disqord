using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Disqord.Gateway;
using Emporia.Extensions.Discord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    internal class RequireShowroomAttribute : DiscordGuildCheckAttribute
    {
        private readonly string _roomType;

        public RequireShowroomAttribute(string roomType) => _roomType = roomType;

        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Results.Failure("Setup Required: No showrooms have been configured for this server.");

            var listingType = $"{(context.Command as ApplicationCommand).Alias} {_roomType}";

            if (!settings.AllowedListings.Any(listing => listing.Equals(listingType, StringComparison.OrdinalIgnoreCase)))
                return Results.Failure($"{listingType.Pascalize()} Listings are not allowed.{Environment.NewLine}Configure Allowed Listings using the `Server Settings` command.");

            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId);
            var showrooms = emporium.Showrooms.Where(x => x.ListingType.Equals(_roomType, StringComparison.OrdinalIgnoreCase));
            var channel = context.Bot.GetChannel(context.GuildId, context.ChannelId) as ITextChannel;

            if (showrooms.Any(x => x.Id.Value.Equals(context.ChannelId.RawValue) || x.Id.Value.Equals(channel.CategoryId.GetValueOrDefault().RawValue))) return Results.Success;

            return Results.Failure($"Command must be executed in a {_roomType} Room:{Environment.NewLine}{string.Join(" | ", showrooms.Select(x => Mention.Channel(x.Id.Value)))}");
        }
    }
}
