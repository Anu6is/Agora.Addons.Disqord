using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Common.Enums;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using static Disqord.Discord.Limits.Component;

namespace Agora.Addons.Disqord.Menus.View
{
    public class GuildCurrenciesView : ViewBase
    {
        private readonly List<Currency> _currencies = new();
        private readonly GuildSettingsContext _context;
        private readonly SelectionViewComponent _selection;
        private string _code = string.Empty;

        public GuildCurrenciesView(List<Currency> currencies, GuildSettingsContext context)
            : base(message => message.AddEmbed(
                new LocalEmbed()
                    .WithTitle($"Registered currencies {currencies.Count}")
                    .WithDescription(currencies.Count == 0
                        ? "No Currencies Exist"
                        : $"{string.Join(Environment.NewLine, currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits} | Format: {x}"))}")
                    .WithDefaultColor()))
        {
            _context = context;
            _currencies = currencies;
            _selection = EnumerateComponents().OfType<SelectionViewComponent>().First();

            if (_currencies.Count == 0)
                _selection.Options.Add(new LocalSelectionComponentOption("No Currencies Exist", "0"));

            foreach (var currency in _currencies.Take(25))
                _selection.Options.Add(new LocalSelectionComponentOption(currency.Code, Guid.NewGuid().ToString()));
        }

        [Button(Label = "Delete", Style = LocalButtonComponentStyle.Danger, Position = 0, Row = 0)]
        public async ValueTask RemoveCurrency(ButtonEventArgs e)
        {
            if (_code == _context.Settings.DefaultCurrency.Code)
            {
                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Default currency cannot be deleted").WithIsEphemeral());
                return;
            }

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new DeleteCurrencyCommand(new EmporiumId(_context.Guild.Id), _code));

            if (!result.IsSuccessful)
            {
                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(result.FailureReason).WithIsEphemeral());
            }
            else
            {
                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral(true).WithContent($"Successfully Removed {Markdown.Bold(_code)}"));

                _currencies.Remove(Currency.Create(_code));
                _selection.Options.Remove(_selection.Options.First(x => x.IsDefault.HasValue && x.IsDefault.Value));

                MessageTemplate = message =>
                {
                    message.AddEmbed(
                    new LocalEmbed()
                        .WithTitle($"Registered currencies {_currencies.Count}")
                        .WithDescription(_currencies.Count == 0
                            ? "No Currencies Exist"
                            : $"{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits}  | Format: {x}"))}")
                        .WithDefaultColor());
                };

                _code = string.Empty;

                ReportChanges();
            }
        }

        [Button(Label = "Update", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 0)]
        public async ValueTask EditCurrency(ButtonEventArgs e)
        {
            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle($"Edit Currency")
                .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = e.Interaction.CustomId,
                    Label = "Update Decimal Places",
                    Placeholder = _currencies.First(x => x.Code == _code).DecimalDigits.ToString(),
                    MaximumInputLength = 25,
                    IsRequired = true
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync
                (e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var modalInput = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

            if (!int.TryParse(modalInput, out int decimals))
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Decimal places must be a number").WithIsEphemeral());
                return;
            };

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new UpdateDecimalsCommand(new EmporiumId(_context.Guild.Id), _code, decimals));

            if (result.IsSuccessful)
            {
                var currency = _currencies.First(x => x.Code == _code).WithDecimals(decimals);

                if (_code == _context.Settings.DefaultCurrency.Code) await UpdateDefaultCurrency(currency, e);
            }
            else
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(result.FailureReason).WithIsEphemeral());

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"{Markdown.Bold(_code)} Updated to {decimals} Decimals"));

            MessageTemplate = message =>
            {
                message.AddEmbed(
                new LocalEmbed()
                    .WithTitle($"Registered currencies {_currencies.Count}")
                    .WithDescription(_currencies.Count == 0
                        ? "No Currencies Exist"
                        : $"{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits} | Format: {x}"))}")
                    .WithDefaultColor());
            };

            ReportChanges();

            return;
        }

        [Button(Label = "Format", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 0)]
        public async ValueTask FormatCurrency(ButtonEventArgs e)
        {
            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var currency = _currencies.First(x => x.Code == _code);
            var format = currency.Format == CurrencyFormat.RightAlign ? CurrencyFormat.LeftAlign : CurrencyFormat.RightAlign;

            await mediator.Send(new UpdateFormatCommand(new EmporiumId(_context.Guild.Id), _code, format));

            currency = currency.WithFormat(format);

            if (_code == _context.Settings.DefaultCurrency.Code) await UpdateDefaultCurrency(currency, e);

            MessageTemplate = message =>
            {
                message.AddEmbed(
                new LocalEmbed()
                    .WithTitle($"Registered currencies {_currencies.Count}")
                    .WithDescription(_currencies.Count == 0
                        ? "No Currencies Exist"
                        : $"{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits} | Format: {x}"))}")
                    .WithDefaultColor());
            };

            ReportChanges();

            return;
        }

        [Button(Label = "Set Default", Style = LocalButtonComponentStyle.Primary, Position = 3, Row = 0)]
        public async ValueTask SetAsDefault(ButtonEventArgs e)
        {
            if (_code == _context.Settings.DefaultCurrency.Code)
            {
                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Currency is already the Default").WithIsEphemeral());
                return;
            }

            var currency = _currencies.First(x => x.Code == _code);

            await UpdateDefaultCurrency(currency, e);

            await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent($"Default currency set to {currency.Symbol}").WithIsEphemeral());

            return;
        }


        [Button(Label = "Register", Style = LocalButtonComponentStyle.Success, Position = 4, Row = 0)]
        public async ValueTask AddCurrency(ButtonEventArgs e)
        {
            if (_currencies.Count == 25)
            {
                await e.Interaction.Response().SendMessageAsync(
                    new LocalInteractionMessageResponse()
                        .AddEmbed(new LocalEmbed().WithDescription("Currency limit reached. Only 25 currencies can be registered!").WithDefaultColor()));
                return;
            }

            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle($"Create a Currency")
                .WithComponents(
                LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = "symbol",
                    Label = "Enter Currency Symbol",
                    Placeholder = "symbol or :emojiName:",
                    MaximumInputLength = 25,
                    IsRequired = true
                }),
                LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = "decimals",
                    Label = "Enter the Number of Decimal Places",
                    PrefilledValue = _context.Settings.DefaultCurrency.DecimalDigits.ToString(),
                    MaximumInputLength = 2,
                    MinimumInputLength = 0,
                    IsRequired = false,
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>();
            var code = rows.First().Components.OfType<ITextInputComponent>().First().Value;
            _ = int.TryParse(rows.Last().Components.OfType<ITextInputComponent>().First().Value, out int decimals);

            var symbol = code;
            if (code.StartsWith(':') && code.EndsWith(':'))
            {
                var bot = Menu.Client as DiscordBot;
                var emojis = bot.GetGuild(_context.Guild.Id).Emojis.Values;
                var emoji = emojis.FirstOrDefault(x => x.Name.Equals(code.Trim(':'), StringComparison.OrdinalIgnoreCase));

                if (emoji == null)
                {
                    emojis = await bot.FetchGuildEmojisAsync(_context.Guild.Id);
                    emoji = emojis.FirstOrDefault(x => x.Name.Equals(code.Trim(':'), StringComparison.OrdinalIgnoreCase));
                }

                if (emoji != null) symbol = emoji.ToString();
            }

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new CreateCurrencyCommand(new EmporiumId(_context.Guild.Id), code, decimals, symbol));

            if (result.IsSuccessful)
            {
                _currencies.Add(result.Data);

                var option = new LocalSelectionComponentOption(code, Guid.NewGuid().ToString());
                _selection.Options.Add(option);

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"{Markdown.Bold(code)} Successfully Added!"));

                MessageTemplate = message =>
                {
                    message.AddEmbed(
                    new LocalEmbed()
                        .WithTitle($"Registered currencies {_currencies.Count}")
                        .WithDescription(_currencies.Count == 0
                            ? "No Currencies Exist"
                            : $"{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits} | Format: {x}"))}")
                        .WithDefaultColor());
                };

                ReportChanges();
            }
            else
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(result.FailureReason).WithIsEphemeral());

                if (result is IExceptionResult exResult)
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, exResult.RaisedException);
            }

            return;
        }

        [Selection(MaximumSelectedOptions = 1, Placeholder = "Select a currency", Row = 1)]
        public ValueTask SelectCurrency(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count == 1)
            {
                if (e.SelectedOptions[0].Value == "0") return default;
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption)
                    defaultOption.IsDefault = false;

                _code = e.SelectedOptions[0].Label.Value;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            }

            ReportChanges();

            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Position = 4, Row = 4)]
        public ValueTask CloseView(ButtonEventArgs e) => default;

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }

        public override ValueTask UpdateAsync()
        {
            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 0)
                    button.IsDisabled = _code == _context.Settings.DefaultCurrency.Code || _code == string.Empty;
                else if (button.Position < 4)
                    button.IsDisabled = _code == string.Empty;

                if (!button.IsDisabled && button.Position == 2)
                {
                    var currency = _currencies.First(x => x.Code == _code);

                    if (currency.Format == CurrencyFormat.RightAlign)
                        button.Label = "Left Align";
                    else
                        button.Label = "Right Align";
                }

                if (button.Position == 3 && _code == _context.Settings.DefaultCurrency.Code) button.IsDisabled = true;
            }

            return base.UpdateAsync();
        }

        private async ValueTask UpdateDefaultCurrency(Currency currency, ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;
            settings.DefaultCurrency = currency;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            }
        }
    }
}
