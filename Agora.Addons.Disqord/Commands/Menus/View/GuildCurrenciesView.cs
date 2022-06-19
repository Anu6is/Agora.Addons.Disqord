using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class GuildCurrenciesView : ViewBase
    {
        private readonly List<Currency> _currencies = new();
        private readonly GuildSettingsContext _context;
        private string _symbol = string.Empty;
        public GuildCurrenciesView(List<Currency> currencies, GuildSettingsContext context)
            : base(message => message.AddEmbed(
                new LocalEmbed()
                    .WithDescription($"Registered currencies {currencies.Count}{Environment.NewLine}{string.Join(Environment.NewLine, currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits}"))}")
                    .WithDefaultColor()))
        {
            _context = context;
            _currencies = currencies;

            var selection = EnumerateComponents().OfType<SelectionViewComponent>().First();

            if (_currencies.Count == 0)
                selection.Options.Add(new LocalSelectionComponentOption("No Currencies Exist", "0"));

            var count = 0;
            foreach (var currency in _currencies)
            {
                count++;
                var option = new LocalSelectionComponentOption(currency.Symbol, count.ToString());
                selection.Options.Add(option);
            }
        }

        [Button(Label = "Remove Currency", Style = LocalButtonComponentStyle.Danger, Position = 1, Row = 0)]
        public async ValueTask RemoveCurrency(ButtonEventArgs e)
        {
            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new DeleteCurrencyCommand(new EmporiumId(_context.Guild.Id), _symbol));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await e.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral(true).WithContent($"Successfully Removed {Markdown.Bold(_symbol)}"));

            _currencies.Remove(Currency.Create(_symbol));

            MessageTemplate = message =>
            {
                message.AddEmbed(new LocalEmbed()
                       .WithDescription($"Registered currencies {_currencies.Count}{Environment.NewLine}{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits}"))}")
                       .WithDefaultColor());
            };

            _symbol = string.Empty;

            ReportChanges();
        }

        [Button(Label = "Edit Currency", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 0)]
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
                    Placeholder = _currencies.First(x => x.Symbol == _symbol).DecimalDigits.ToString(),
                    MaximumInputLength = 25,
                    IsRequired = true
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

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

            try
            {
                await mediator.Send(new UpdateDecimalsCommand(new EmporiumId(_context.Guild.Id), _symbol, decimals));

                _currencies.First(x => x.Symbol == _symbol).WithDecimals(decimals);
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"{Markdown.Bold(_symbol)} Updated to {decimals} Decimals"));

            MessageTemplate = message =>
            {
                message.AddEmbed(new LocalEmbed()
                       .WithDescription($"Registered currencies {_currencies.Count}{Environment.NewLine}{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits}"))}")
                       .WithDefaultColor());
            };

            ReportChanges();

            return;
        }

        [Button(Label = "Add Currency", Style = LocalButtonComponentStyle.Success, Position = 3, Row = 0)]
        public async ValueTask AddCurrency(ButtonEventArgs e)
        {
            var response = new LocalInteractionModalResponse()
                .WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle($"Create a Currency")
                .WithComponents(
                LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = "symbol",
                    Label = "Enter Currency Symbol",
                    Placeholder = "symbol",
                    MaximumInputLength = 25,
                    IsRequired = true
                }),
                LocalComponent.Row(new LocalTextInputComponent()
                {
                    Style = TextInputComponentStyle.Short,
                    CustomId = "decimals",
                    Label = "Enter the Number of Decimal Places",
                    Placeholder = "0",
                    MaximumInputLength = 2,
                    MinimumInputLength = 0
                }));

            await e.Interaction.Response().SendModalAsync(response);

            var reply = await Menu.Interactivity.WaitForInteractionAsync(e.ChannelId, x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId, TimeSpan.FromMinutes(10));

            if (reply == null) return;

            var modal = reply.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>();
            var symbol = rows.First().Components.OfType<ITextInputComponent>().First().Value;
            _ = int.TryParse(rows.Last().Components.OfType<ITextInputComponent>().First().Value, out int decimals);

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                _currencies.Add(await mediator.Send(new CreateCurrencyCommand(new EmporiumId(_context.Guild.Id), symbol, decimals)));
            }
            catch (Exception ex) when (ex is ValidationException validationException)
            {
                var message = string.Join('\n', validationException.Errors.Select(x => $"• {x.ErrorMessage}"));

                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(message).WithIsEphemeral());
                await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, ex);

                return;
            }

            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent($"{Markdown.Bold(symbol)} Successfully Added!"));

            MessageTemplate = message =>
            {
                message.AddEmbed(new LocalEmbed()
                       .WithDescription($"Registered currencies {_currencies.Count}{Environment.NewLine}{string.Join(Environment.NewLine, _currencies.Select(x => $"Symbol: {Markdown.Bold(x.Symbol)} | Decimals: {x.DecimalDigits}"))}")
                       .WithDefaultColor());
            };

            ReportChanges();

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

                _symbol = e.SelectedOptions[0].Label.Value;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
            }

            return default;
        }

        [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Row = 4)]
        public async ValueTask CloseView(ButtonEventArgs e)
        {
            await (e.Interaction.Client as AgoraBot).DeleteMessageAsync(e.ChannelId, e.Interaction.Message.Id);

            return;
        }

        public override ValueTask UpdateAsync()
        {
            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
                if (button.Position == 1)
                    button.IsDisabled = _currencies.Count <= 1 || _symbol == string.Empty;
                else if (button.Position == 2)
                    button.IsDisabled = _symbol == string.Empty;

            return base.UpdateAsync();
        }
    }
}
