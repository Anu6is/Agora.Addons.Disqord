using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    internal class ServerTimeView : GuildSettingsView
    {
        private readonly Emporium _emporium;
        private readonly GuildSettingsContext _context;
        private readonly DefaultDiscordGuildSettings _settings;
        
        public ServerTimeView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions) 
        {
            _context = context;
            _emporium = Emporium.Create(new EmporiumId(context.GuildId)).WithLocalTime(Time.From(TimeFromOffset(context.Settings.Offset)));
            _settings = DefaultDiscordGuildSettings.Create(context.Settings.GuildId, context.Settings.DefaultCurrency, context.Settings.ResultLogChannelId, 
                                                           context.Settings.AuditLogChannelId, context.Settings.Offset);
            _settings.AdminRole = context.Settings.AdminRole;
            _settings.AllowAbsenteeBidding = context.Settings.AllowAbsenteeBidding;
            _settings.AllowedListings = context.Settings.AllowedListings;
            _settings.AllowShillBidding = context.Settings.AllowShillBidding;
            _settings.BrokerRole = context.Settings.BrokerRole;
            _settings.MerchantRole = context.Settings.MerchantRole;
            _settings.SnipeRange = context.Settings.SnipeRange;
            _settings.SnipeExtension = context.Settings.SnipeExtension;
        }

        [Button(Label = "Decrease", Style = LocalButtonComponentStyle.Primary, Emoji = "⬇️")]
        public ValueTask DecreaseTime(ButtonEventArgs e)
        {
            return UpdateTemplateTime(TimeFromOffset(_settings.Offset, -1));
        }

        [Button(Label = "Increase", Style = LocalButtonComponentStyle.Primary, Emoji = "⬆️")]
        public ValueTask IncreaseTime(ButtonEventArgs e)
        {
            return UpdateTemplateTime(TimeFromOffset(_settings.Offset, 1));
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Emoji = "💾")]
        public async ValueTask SetUpdatedTime(ButtonEventArgs e)
        {
            if (_settings.Offset == _context.Settings.Offset) return;

            var time = TimeFromOffset(_settings.Offset);

            using (var scope = _context.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var dataAccessor = scope.ServiceProvider.GetRequiredService<IDataAccessor>();

                await dataAccessor.BeginTransactionAsync(async () =>
                {
                    var emporiumId = new EmporiumId(_context.GuildId);
                    var emporium = await mediator.Send(new UpdateLocalTimeCommand(emporiumId, Time.From(time)));

                    var settings = (DefaultDiscordGuildSettings)_context.Settings;

                    settings.Offset = emporium.TimeOffset;

                    await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    TemplateMessage.WithEmbeds(settings.AsEmbed("Server Time", new LocalEmoji("🕰")));
                });
            }

            foreach (ButtonViewComponent button in EnumerateComponents().OfType<ButtonViewComponent>())
                button.IsDisabled = true;

            ReportChanges();

            return;
        }

        private ValueTask UpdateTemplateTime(string time)
        {
            _emporium.WithLocalTime(Time.From(time));
            _settings.Offset = _emporium.TimeOffset;
            
            TemplateMessage.WithEmbeds(_settings.AsEmbed("Server Time"));

            ReportChanges();
            
            return default;
        }

        private static string TimeFromOffset(TimeSpan offset, int shift = 0) 
            => DateTimeOffset.UtcNow.ToOffset(offset).AddHours(shift).ToString("HH:mm");
    }
}
