using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    public class MaintenanceService : DiscordBotService
    {
        private readonly ILogger<MaintenanceService> _logger;
        private readonly IEmporiaCacheService _emporiumCache;

        public MaintenanceService(DiscordBotBase bot, IEmporiaCacheService emporiumCache, ILogger<MaintenanceService> logger) : base(logger, bot)
        {
            _logger = logger;
            _emporiumCache = emporiumCache;
        }

        protected override async ValueTask OnLeftGuild(LeftGuildEventArgs e)
        {
            var emporium = await _emporiumCache.GetEmporiumAsync(e.GuildId);

            if (emporium == null) return;

            var mediator = Bot.Services.CreateScope().ServiceProvider.GetRequiredService<IMediator>();
            
            await mediator.Send(new DeleteEmporiumCommand(new EmporiumId(emporium.EmporiumId)));

            _logger.LogDebug("Removed data for {guild}", e.Guild.Name);
        }

        protected override async ValueTask OnChannelDeleted(ChannelDeletedEventArgs e)
        {
            if (e.Channel is not ITextChannel) return;

            var emporium = await _emporiumCache.GetEmporiumAsync(e.GuildId);

            if (emporium == null) return;

            var rooms = emporium.Showrooms.Where(x => e.ChannelId.Equals(x.Id.Value)).ToArray();

            if (rooms.Length == 0) return;

            var mediator = Bot.Services.CreateScope().ServiceProvider.GetRequiredService<IMediator>();

            foreach (var room in rooms)
                await mediator.Send(new DeleteShowroomCommand(new EmporiumId(emporium.EmporiumId), room.Id, Enum.Parse<ListingType>(room.ListingType)));

            _logger.LogDebug("Removed showroom data for {guild}", Bot.GetGuild(e.GuildId));
        }
    }
}
