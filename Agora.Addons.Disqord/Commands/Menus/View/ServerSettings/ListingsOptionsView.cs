using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ListingsOptionsView : ServerSettingsView
    {
        private readonly IDiscordGuildSettings _settings;
        private readonly GuildSettingsContext _context;

        public ListingsOptionsView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
            _settings = context.Settings.DeepClone();

            var selection = (SelectionViewComponent)EnumerateComponents().First(x => x.Row == 1);

            foreach (var option in selection.Options)
                if (context.Settings.AllowedListings.Any(listing => listing == option.Label)) option.IsDefault = true;
        }

        [Selection(MaximumSelectedOptions = 8, Row = 1, Placeholder = "Select the listing types to allow")]
        [SelectionOption("Select All", Value = "0", Description = "Allow all available listing options.")]
        [SelectionOption("Standard Auction", Value = "1", Description = "Highest bid wins after the auction ends/expires.")]
        [SelectionOption("Live Auction", Value = "2", Description = "Auction ends if a set amount of time passes with no new bids.")]
        [SelectionOption("Sealed Auction", Value = "3", Description = "Bids are hidden (sealed). Winner pays the second highest bid.")]
        [SelectionOption("Standard Market", Value = "4", Description = "List items at a fixed price.")]
        [SelectionOption("Flash Market", Value = "5", Description = "List items at a fixed price with a timed discount period.")]
        [SelectionOption("Bulk Market", Value = "6", Description = "List a large quantity of items. Buyers select their quantity.")]
        [SelectionOption("Standard Trade", Value = "7", Description = "List an item for trade, and accept an incoming trade offer.")]
        //[SelectionOption("Reverse Trade", Value = "8", Description = "Post an item you want, and agree to a submitted exchange offer.")]
        public ValueTask ListingsSelection(SelectionEventArgs e)
        {
            _settings.AllowedListings.Clear();
            foreach (var option in e.Selection.Options) option.IsDefault = false;

            if (e.SelectedOptions.Any(x => x.Value == "0"))
            {
                for (var i = 1; i < e.Selection.Options.Count; i++)
                {
                    e.Selection.Options[i].IsDefault = true;
                    _settings.AllowedListings.Add(e.Selection.Options[i].Label.ToString());
                }
            }
            else
            {
                foreach (var option in e.SelectedOptions)
                {
                    e.Selection.Options[int.Parse(option.Value.ToString())].IsDefault = true;
                    _settings.AllowedListings.Add(option.Label.ToString());
                }
            }

            MessageTemplate = message => message.WithEmbeds(_settings.ToEmbed("Allowed Listings"));

            ReportChanges();

            return default;
        }

        [Button(Label = "Save", Style = LocalButtonComponentStyle.Success, Emoji = "💾", Row = 4)]
        public async ValueTask SaveSelectedOptions(ButtonEventArgs e)
        {
            if (_settings.AllowedListings.Count == 0) return;

            var settings = (DefaultDiscordGuildSettings)_context.Settings;
            settings.AllowedListings = _settings.AllowedListings;

            using (var scope = _context.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Allowed Listings", new LocalEmoji("📖")));
            }

            var requirements = settings.FindMissingRequirement();

            if (requirements != null && requirements.Contains("room (channel)"))
            {
                var cache = _context.Services.GetRequiredService<IEmporiaCacheService>();
                var emporium = await cache.GetEmporiumAsync(_context.Guild.Id);

                Menu.View = new MainShowroomView(_context, emporium.Showrooms);
            }
            else
            {
                e.Button.IsDisabled = true;
            }

            return;
        }
    }
}
