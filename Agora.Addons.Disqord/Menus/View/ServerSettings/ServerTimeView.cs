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
    public class ServerTimeView : ServerSettingsView
    {
        private readonly Emporium _emporium;
        private readonly GuildSettingsContext _context;
        private readonly IDiscordGuildSettings _settings;
        
        public ServerTimeView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions) 
        {
            _context = context;
            _settings = context.Settings.DeepClone();
            _emporium = Emporium.Create(new EmporiumId(context.Guild.Id)).WithLocalTime(Time.From(TimeFromOffset(context.Settings.Offset)));
        }

        [Button(Label = "Decrease", Style = LocalButtonComponentStyle.Primary, Emoji = "⬇️", Row = 1)]
        public ValueTask DecreaseTime(ButtonEventArgs e)
        {
            return UpdateTemplateTime(TimeFromOffset(_settings.Offset, -1));
        }

        [Button(Label = "Increase", Style = LocalButtonComponentStyle.Primary, Emoji = "⬆️", Row = 1)]
        public ValueTask IncreaseTime(ButtonEventArgs e)
        {
            return UpdateTemplateTime(TimeFromOffset(_settings.Offset, 1));
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Emoji = "💾", Row = 1)]
        public async ValueTask SetUpdatedTime(ButtonEventArgs e)
        {
            if (_settings.Offset == _context.Settings.Offset) return;

            var time = TimeFromOffset(_settings.Offset);

            using (var scope = _context.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var dataAccessor = scope.ServiceProvider.GetRequiredService<IDataAccessor>();
                var emporiumId = new EmporiumId(_context.Guild.Id);
                var referenceNumber = ReferenceNumber.Create(e.AuthorId);

                scope.ServiceProvider.GetRequiredService<ICurrentUserService>().CurrentUser = EmporiumUser.Create(emporiumId, referenceNumber);
                
                await dataAccessor.BeginTransactionAsync(async () =>
                {
                    var emporiumId = new EmporiumId(_context.Guild.Id);
                    var emporium = await mediator.Send(new UpdateLocalTimeCommand(emporiumId, Time.From(time)));

                    var settings = (DefaultDiscordGuildSettings)_context.Settings;

                    settings.Offset = emporium.TimeOffset;

                    await mediator.Send(new UpdateGuildSettingsCommand(settings));

                    TemplateMessage.WithEmbeds(settings.ToEmbed("Server Time", new LocalEmoji("🕰")));
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
            
            TemplateMessage.WithEmbeds(_settings.ToEmbed("Server Time"));

            ReportChanges();
            
            return default;
        }

        private static string TimeFromOffset(TimeSpan offset, int shift = 0) 
            => DateTimeOffset.UtcNow.ToOffset(offset).AddHours(shift).ToString("HH:mm");
    }
}
