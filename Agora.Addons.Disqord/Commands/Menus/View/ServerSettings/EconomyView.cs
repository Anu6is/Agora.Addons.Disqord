using Agora.Addons.Disqord.Extensions;
using Believe.Net;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Extensions.DependencyInjection;
using static System.Net.Mime.MediaTypeNames;
using System;
using Microsoft.Extensions.Configuration;
using Disqord.Rest;

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
        //[SelectionOption("Basic", Value = "AuctionBot", Description = "Users require a server balance to purchase items.")] TODO - add agora economy
        [SelectionOption("UnbelievaBoat", Value = "UnbelievaBoat", Description = "Users require an UnbelievaBoat balance to purchase items.")]
        public async ValueTask ListingsSelection(SelectionEventArgs e)
        {
            var selectedEconomy = e.SelectedOptions[0];

            if (selectedEconomy.Value == "UnbelievaBoat")
            {
                var economyAccess = await _context.Services.GetRequiredService<UnbelievaClient>().HasPermissionAsync(_context.Guild.Id, ApplicationPermission.EditEconomy);

                if (!economyAccess)
                {
                    await RequestAuthorizationAsync(e.Interaction);
                    return;
                }
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
