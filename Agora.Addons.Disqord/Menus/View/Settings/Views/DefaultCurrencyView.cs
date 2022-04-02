using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    internal class DefaultCurrencyView : BaseGuildSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly SelectionViewComponent _selection;
        private readonly Currency[] _currencies = Array.Empty<Currency>();
        public DefaultCurrencyView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;
            
            var cacheService = context.Services.GetRequiredService<IEmporiaCacheService>();
            var emporium = cacheService.GetCachedEmporium(context.GuildId);  
            
            _selection = new SelectionViewComponent(HandleSelection) 
            {
                MinimumSelectedOptions = 1,
                MaximumSelectedOptions = 1,
                Row = 1
            };

            if (emporium == null)
                _selection.Options.Add(new LocalSelectionComponentOption("Error loading existing currencies", "0"));
            else if (emporium.Currencies != null)
                _currencies = emporium.Currencies.ToArray();

            foreach (var currency in _currencies)
            {
                var option = new LocalSelectionComponentOption($"Symbol: {currency.Symbol} | Decimals: {currency.DecimalDigits} | Format: {currency}", currency.Code);
                _selection.Options.Add(option);
            }
           
            AddComponent(_selection);
        }
        
        private async ValueTask HandleSelection(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                if (_selection.Options.FirstOrDefault(x => x.IsDefault) is { } defaultOption)
                    defaultOption.IsDefault = false;

                _selection.Options.FirstOrDefault(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                var currency = _currencies.FirstOrDefault(x => x.Code == e.SelectedOptions[0].Value);

                if (currency != null)
                    await UpdateDefaultCurrency(currency);
            }

            return;
        }

        private async ValueTask UpdateDefaultCurrency(Currency currency) 
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;
            
            using (var scope = _context.Services.CreateScope())
            {
                settings.DefaultCurrency = currency;

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                
                await mediator.Send(new UpdateGuildSettingsCommand(settings));
                
                TemplateMessage.WithEmbeds(settings.AsEmbed("Default Currency", new LocalEmoji("💰")));
            }

            ReportChanges();

            return;
        }

    }
}
