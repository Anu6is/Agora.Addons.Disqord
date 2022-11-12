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

            var roles = context.Guild.Roles.Values.Where(role => !role.IsManaged);
            var roleSelection = EnumerateComponents().OfType<SelectionViewComponent>().First(selection => selection.Row == 1);

            roleSelection.Type = SelectionComponentType.Role;
        }

        [Selection(MaximumSelectedOptions = 1, MinimumSelectedOptions = 1, Row = 1, Placeholder = "Select a role.")]
        [SelectionOption("No available roles", Value = "0")]
        public async ValueTask SelectCategoryChannel(SelectionEventArgs e)
        {
            if (e.SelectedOptions.Count > 0)
            {
                if (e.Selection.Options.FirstOrDefault(x => x.IsDefault.HasValue && x.IsDefault.Value) is { } defaultOption) defaultOption.IsDefault = false;

                e.Selection.Options.First(x => x.Value == e.SelectedOptions[0].Value).IsDefault = true;
                var role = ulong.Parse(e.SelectedOptions[0].Value.ToString());
                var settings = (DefaultDiscordGuildSettings)_context.Settings;

                switch (_role)
                {
                    case "Manager Role": settings.AdminRole = role; break;
                    case "Broker Role": settings.BrokerRole = role; break;
                    case "Merchant Role": settings.MerchantRole = role; break;
                    case "Buyer Role": settings.BuyerRole = role; break;
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
