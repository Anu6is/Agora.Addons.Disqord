using Agora.Addons.Disqord.Extensions;
using Believe.Net;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Domain.Common;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class EconomyView : ServerSettingsView
    {
        private readonly DefaultDiscordGuildSettings _settings;
        private readonly GuildSettingsContext _context;


        public EconomyView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings as DefaultDiscordGuildSettings;

            var selection = (SelectionViewComponent)EnumerateComponents().First(x => x.Row == 1);

            foreach (var option in selection.Options)
                if (context.Settings.EconomyType.Equals(option.Value)) option.IsDefault = true;
        }

        [Selection(MinimumSelectedOptions = 1, MaximumSelectedOptions = 1, Row = 1)]
        [SelectionOption("Disable", Value = "Disabled", Description = "Users do not require a balance to purchase items.")]
        [SelectionOption("Basic", Value = "AuctionBot", Description = "Users require a server balance to purchase items.")]
        [SelectionOption("UnbelievaBoat", Value = "UnbelievaBoat", Description = "Users require an UnbelievaBoat balance to purchase items.")]
        public async ValueTask ListingsSelection(SelectionEventArgs e)
        {
            var selectedEconomy = e.SelectedOptions[0];

            foreach (var component in EnumerateComponents().OfType<ButtonViewComponent>())
                if (component.Label.Contains("Default Balance")) RemoveComponent(component);

            if (selectedEconomy.Value == "UnbelievaBoat")
            {
                var ubClient = _context.Services.GetRequiredService<UnbelievaClient>();
                var economyAccess = await ubClient.HasPermissionAsync(_context.Guild.Id, ApplicationPermission.EditEconomy);

                if (!economyAccess)
                {
                    await RequestAuthorizationAsync(e.Interaction);

                    foreach (var option in e.Selection.Options) option.IsDefault = false;

                    ReportChanges();

                    return;
                }
            }
            else if (selectedEconomy.Value == "AuctionBot")
            {
                AddComponent(new ButtonViewComponent(ClearDefaultBalance)
                {
                    Label = "Remove Default Balance",
                    Position = 1,
                    Row = 4,
                    Style = LocalButtonComponentStyle.Danger,
                    IsDisabled = _settings.DefaultBalance == 0
                });
                AddComponent(new ButtonViewComponent(SetDefaultBalance)
                {
                    Label = "Set Default Balance",
                    Position = 2,
                    Row = 4,
                    Style = LocalButtonComponentStyle.Primary
                });
            }

            foreach (var option in e.Selection.Options) option.IsDefault = false;

            selectedEconomy.IsDefault = true;
            _settings.EconomyType = selectedEconomy.Value.ToString();

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(_settings));

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Server Economy", new LocalEmoji("💰")));

            ReportChanges();

            return;
        }

        private async ValueTask ClearDefaultBalance(ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.DefaultBalance = 0;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            
            await e.Interaction.Response().SendMessageAsync(
                new LocalInteractionMessageResponse().WithIsEphemeral()
                    .AddEmbed(new LocalEmbed().WithDescription("Default balance reset to 0").WithDefaultColor()));

            var reset = EnumerateComponents().OfType<ButtonViewComponent>().First(x => x.Label == "Remove Default Balance").IsDisabled = true;

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Server Economy", new LocalEmoji("💰")));

            return;
        }

        private async ValueTask SetDefaultBalance(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Set Default Balance")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.TextInput("defaultBalance", "Set Default", TextInputComponentStyle.Short)
                                      .WithPlaceholder("Enter the default amount to give new users")));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var defaultBalance = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

            if (!decimal.TryParse(defaultBalance, out var balance))
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent($"{defaultBalance} isn't a valid number.").WithIsEphemeral());
                return;
            }

            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.DefaultBalance = balance;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

            await modal.Response().SendMessageAsync(
                new LocalInteractionMessageResponse()
                    .WithContent($"Default Balance: {(settings.DefaultCurrency == null ? balance : Money.Create(balance, settings.DefaultCurrency))}")
                    .WithIsEphemeral());

            var reset = EnumerateComponents().OfType<ButtonViewComponent>().FirstOrDefault(x => x.Label == "Remove Default Balance");

            if (reset != null) reset.IsDisabled = false;

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Server Economy", new LocalEmoji("💰")));

            return;
        }

        private async ValueTask RequestAuthorizationAsync(IInteraction interaction)
        {
            var authUrl = _context.Services.GetRequiredService<IConfiguration>()["Url:UnbelievaBoat"];
            var message = $"{_context.Guild.Client.CurrentUser.Name} needs to be  {Markdown.Link("**Authorized**", authUrl)} with UnbelievaBoat in order to link economies.";
            var embed = new LocalEmbed()
            {
                Title = "Authorization Required",
                Description = message,
                Url = authUrl,
                Footer = new LocalEmbedFooter().WithText("UnbelievaBoat must be in the Server to access it's features!"),
                Color = Color.Teal
            };

            await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().AddEmbed(embed).WithIsEphemeral());
        }
    }
}
