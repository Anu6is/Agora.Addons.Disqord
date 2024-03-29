﻿using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class DefaultCurrencyView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly Currency[] _currencies = Array.Empty<Currency>();
        public DefaultCurrencyView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions = null) : base(context, settingsOptions)
        {
            _context = context;

            var selection = EnumerateComponents().OfType<SelectionViewComponent>().First(x => x.Row == 1);
            var cacheService = context.Services.GetRequiredService<IEmporiaCacheService>();
            var emporium = cacheService.GetCachedEmporium(context.Guild.Id);

            selection.Options.Clear();

            if (emporium == null)
                selection.Options.Add(new LocalSelectionComponentOption("Error loading currencies", "0"));
            else if (emporium.Currencies != null)
                _currencies = emporium.Currencies.Distinct(new CurrencyEqualityComparer()).ToArray();

            foreach (var currency in _currencies)
            {
                var symbol = currency.Symbol;

                if (symbol.StartsWith("<:") && symbol.IndexOf(":", 2) > 0) symbol = symbol.Split(':')[1];

                var option = new LocalSelectionComponentOption(symbol, currency.Code);
                selection.Options.Add(option);
            }
        }

        [Selection(MinimumSelectedOptions = 1, MaximumSelectedOptions = 1, Row = 1, Placeholder = "Select a Currency")]
        [SelectionOption("Error loading currencies", Value = "0")]
        public async ValueTask SelectCurrency(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                if (e.SelectedOptions[0].Value == "0") return;
                if (e.SelectedOptions[0].Value == _context.Settings.DefaultCurrency.Code) return;

                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;

                var currency = _currencies.FirstOrDefault(x => x.Code == e.SelectedOptions[0].Value);

                if (currency == null) return;

                await UpdateDefaultCurrency(currency, e);

                e.Selection.IsDisabled = true;
            }

            return;
        }

        private async ValueTask UpdateDefaultCurrency(Currency currency, SelectionEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;
            settings.DefaultCurrency = currency;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Default Currency", new LocalEmoji("💰")));
            }

            ReportChanges();

            return;
        }

    }
}
