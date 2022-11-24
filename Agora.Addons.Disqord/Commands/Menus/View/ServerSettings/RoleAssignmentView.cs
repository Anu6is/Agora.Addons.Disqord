using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class RoleAssignmentView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;
        private readonly string _role;

        public RoleAssignmentView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
            _role = settingsOptions.FirstOrDefault(x => x.IsDefault)?.Name;

            EnumerateComponents().OfType<SelectionViewComponent>().First(selection => selection.Row == 1).Type = SelectionComponentType.Role;
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 1, Row = 1, Placeholder = "Select a role.")]
        [SelectionOption("No available roles", Value = "0")]
        public async ValueTask SelectCategoryChannel(SelectionEventArgs e)
        {
            if (e.SelectedEntities.Count == 1)
            {
                var role = e.SelectedEntities.First();
                var settings = (DefaultDiscordGuildSettings)_context.Settings;

                switch (_role)
                {
                    case "Manager Role": settings.AdminRole = role.Id; break;
                    case "Broker Role": settings.BrokerRole = role.Id; break;
                    case "Merchant Role": settings.MerchantRole = role.Id; break;
                    case "Buyer Role": settings.BuyerRole = role.Id; break;
                    default: throw new InvalidOperationException($"Unsupported role: {_role}");
                }

                using var scope = _context.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));

                MessageTemplate = message => message.WithEmbeds(_context.Settings.ToEmbed());

                ReportChanges();
            }

            return;
        }
    }
}
