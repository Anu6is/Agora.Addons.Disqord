using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public sealed class MinimumBidView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public MinimumBidView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;
        }

        [Button(Label = "Default Minimum Bid Limit", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public async ValueTask SetBidLimit(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Minimal Bid Increase")
                .WithComponents(
                    LocalComponent.Row(
                        LocalComponent.TextInput("absolute", "Fixed Amount", TextInputComponentStyle.Short).WithPlaceholder("example: 5").WithIsRequired(false)),
                    LocalComponent.Row(
                        LocalComponent.TextInput("percent", "Percentage of Starting Price", TextInputComponentStyle.Short).WithPlaceholder("example: 3").WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            Delta deltaValue = null;
            var modal = response.Interaction as IModalSubmitInteraction;
            var absolute = modal.Components.OfType<IRowComponent>().ToArray().First().Components.OfType<ITextInputComponent>().First().Value;
            var percent = modal.Components.OfType<IRowComponent>().ToArray().Last().Components.OfType<ITextInputComponent>().First().Value;

            if (await ConfirmSelectionAsync(absolute, percent, modal) == false) return;
            if (await MultiSelectAsync(absolute, percent, modal)) return;

            if (absolute.IsNotNull() && decimal.TryParse(absolute, out var minAmount))
            {
                if (minAmount <= 0)
                {
                    await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Value must be greater than 0"));
                    return;
                }

                deltaValue = new Delta(minAmount, DeltaType.Absolute);
            }

            if (percent.IsNotNull() && decimal.TryParse(percent, out var minPercent))
            {
                if (minPercent <= 0 || minPercent > 100)
                {
                    await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Value must be greater than 0 without exceeding 100"));
                    return;
                }

                deltaValue = new Delta(minPercent, DeltaType.Percent);
            }

            if (deltaValue is null)
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Value must be a number only"));
                return;
            }

            var emporium = await _context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(e.Interaction.GuildId.Value);
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.MinBidIncrease = deltaValue;

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Min Bid Increase Successfully Updated!"));

            await e.Interaction.Followup().ModifyResponseAsync(x => x.Embeds = new[] { settings.ToEmbed("Minimum Bid Limit") });

            return;
        }

        [Button(Label = "Reset", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 4)]
        public async ValueTask ResetValue(ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            if (settings.MinBidIncrease.Amount == 0) return;

            settings.MinBidIncrease = new Delta(0, 0);

            using var scope = _context.Services.CreateScope();
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            }

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Minimum Bid Limit"));

            ReportChanges();
        }

        private static async Task<bool> ConfirmSelectionAsync(string absolute, string percent, IModalSubmitInteraction modal)
        {
            if (absolute.IsNull() && percent.IsNull())
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("An option must be selected"));

                return false;
            }

            return true;
        }

        private static async Task<bool> MultiSelectAsync(string absolute, string percent, IModalSubmitInteraction modal)
        {
            if (absolute.IsNotNull() && percent.IsNotNull())
            {
                await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Only one option can be selected"));

                return true;
            }

            return false;
        }

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
