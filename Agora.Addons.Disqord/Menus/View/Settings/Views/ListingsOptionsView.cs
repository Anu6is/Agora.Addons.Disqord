using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    internal class ListingsOptionsView : BaseGuildSettingsView
    {
        private readonly IDiscordGuildSettings _settings;
        private readonly GuildSettingsContext _context;

        public ListingsOptionsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            var selection = (SelectionViewComponent) EnumerateComponents().First(x => x.Row == 1);

            foreach (var option in selection.Options)
                if (context.Settings.AllowedListings.Any(listing => listing == option.Label)) option.IsDefault = true;
        }

        [Selection(MaximumSelectedOptions = 6, Row = 1, Placeholder = "Select listings to allow")]
        [SelectionOption("Select All", Value = "0", Description = "Allow all available listing options.")]
        [SelectionOption("Standard Auction", Value = "1", Description = "Highest bid wins after the auction end time expires.")]
        [SelectionOption("Live Auction", Value = "2", Description = "Auction ends if a set amount of time passes with no new bids.")]
        [SelectionOption("Vickrey Auction", Value = "3", Description = "Bids are hidden (sealed). Winner pays the second highest bid.")]
        [SelectionOption("Market", Value = "4", Description = "List item(s) for sale at a fixed price.")]
        [SelectionOption("Trade", Value = "5", Description = "List an item for trade, and accept an incoming trade offer.")]
        [SelectionOption("Exchange", Value = "6", Description = "Post an item you want, and agree to a submitted exchange offer.")]
        public ValueTask ListingsSelection(SelectionEventArgs e)
        {
            _settings.AllowedListings.Clear();            
            foreach (var option in e.Selection.Options) option.IsDefault = false;

            if (e.SelectedOptions.Any(x => x.Value == "0"))
            {
                for (var i = 1; i < e.Selection.Options.Count; i++) 
                {
                    e.Selection.Options[i].IsDefault = true;
                    _settings.AllowedListings.Add(e.Selection.Options[i].Label);
                }
            }
            else
            {
                foreach (var option in e.SelectedOptions) 
                {
                    e.Selection.Options[int.Parse(option.Value)].IsDefault = true;
                    _settings.AllowedListings.Add(option.Label);
                }
            }

            TemplateMessage.WithEmbeds(_settings.AsEmbed("Allowed Listings"));
            
            ReportChanges();

            return default;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Emoji = "💾", Row = 2)]
        public async ValueTask SaveSelectedOptions(ButtonEventArgs e)
        {
            using (var scope = _context.Services.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var settings = (DefaultDiscordGuildSettings)_context.Settings;

                settings.AllowedListings = _settings.AllowedListings;

                await mediator.Send(new UpdateGuildSettingsCommand(settings));

                TemplateMessage.WithEmbeds(settings.AsEmbed("Allowed Listings", new LocalEmoji("📖")));
            }

            foreach (var component in EnumerateComponents().OfType<SelectionViewComponent>())
            {
                if (component.Row == null) continue;
                
                component.IsDisabled = true;
            }

            e.Button.IsDisabled = true;

            return;
        }
    }
}
