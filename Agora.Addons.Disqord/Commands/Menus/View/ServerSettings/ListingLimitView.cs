using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public sealed class ListingLimitView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public ListingLimitView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Button(Label = "Set Listing Limits", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public async ValueTask SetDuration(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Active Listing Limit Per User")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.TextInput("limit", "Set User Limit", TextInputComponentStyle.Short).WithPlaceholder("example: 5").WithIsRequired(true)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var value = modal.Components.OfType<IRowComponent>().ToArray().First().Components.OfType<ITextInputComponent>().First().Value;

            if (int.TryParse(value, out var limit) && limit <= 0)
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Limit must be greater than 0"));
                return;
            }

            var emporium = await _context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(e.Interaction.GuildId.Value);
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.MaxListingsLimit = limit;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("User Listings Limit Successfully Updated!"));

            await e.Interaction.Followup().ModifyResponseAsync(x => x.Embeds = new[] { settings.ToEmbed("User Listing Limit", new LocalEmoji("🔁")) });

            return;
        }

        [Button(Label = "Reset", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 4)]
        public async ValueTask ResetValue(ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            if (settings.MaxListingsLimit == 0) return;

            settings.MaxListingsLimit = 0;

            using var scope = _context.Services.CreateScope();
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            }

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("User Listing Limit", new LocalEmoji("🔁")));

            ReportChanges();
        }

        //protected override string GetCustomId(InteractableViewComponent component)
        //{
        //    if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

        //    return base.GetCustomId(component);
        //}
    }
}
