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
        [SelectionOption("Raid-Helper", Value = "RaidHelper", Description = "Users require DKP to purchase items.")]
        public async ValueTask ListingsSelection(SelectionEventArgs e)
        {
            var selectedEconomy = e.SelectedOptions[0];

            foreach (var component in EnumerateComponents().OfType<ButtonViewComponent>())
                if (component.Label.Contains("Default Balance")) RemoveComponent(component);

            switch (selectedEconomy.Value.Value)
            {
                case "UnbelievaBoat":
                    var ubClient = _context.Services.GetRequiredService<UnbelievaClient>();
                    var economyAccess = await ubClient.HasPermissionAsync(_context.Guild.Id, ApplicationPermission.EditEconomy);

                    if (!economyAccess)
                    {
                        foreach (var option in e.Selection.Options) option.IsDefault = false;

                        await RequestAuthorizationAsync(e.Interaction);

                        return;
                    }
                    break;
                case "RaidHelper":
                    await SetApiKeyAsync(e.Interaction);
                    break;
                case "AuctionBot":
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
                    break;
                default:
                    break;
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
            var content = "Execute </server settings:1013361602499723275> **AFTER** Authorizing";
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

            await (interaction as IComponentInteraction).Message.DeleteAsync();
            await interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(content).AddEmbed(embed).WithIsEphemeral());
        }

        private async ValueTask SetApiKeyAsync(IComponentInteraction interaction)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(interaction.Message.Id.ToString())
                .WithTitle("Set Api Key")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.TextInput("apiKey", "Raid-Helper Api Key", TextInputComponentStyle.Short)
                        .WithPlaceholder("Enter API key provided by Raid-Helper")));

            await interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                interaction.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(5),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var apiKey = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

            _settings.ExternalApiKeys[_context.Guild.Id.ToString()] = apiKey;

            await modal.Response().SendMessageAsync(
                new LocalInteractionMessageResponse()
                    .WithContent($"Raid-Helper API key set to {apiKey}")
                    .WithIsEphemeral());
        }
    }
}
