using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Rest;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using HumanTimeParser.Core.Parsing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Menus.View
{
    public class ListingDurationView : ServerSettingsView
    {
        private readonly GuildSettingsContext _context;

        public ListingDurationView(GuildSettingsContext context, List<GuildSettingsOption> settingsOptions) : base(context, settingsOptions)
        {
            _context = context;

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                if (button.Position == 2)
                    button.Label = $"Default to {(_context.Settings.MinimumDurationDefault ? "Max" : "Min")}";
            }
        }

        [Button(Label = "Set Duration Limits", Style = LocalButtonComponentStyle.Primary, Position = 1, Row = 4)]
        public async ValueTask SetDuration(ButtonEventArgs e)
        {
            var modalResponse = new LocalInteractionModalResponse().WithCustomId(e.Interaction.Message.Id.ToString())
                .WithTitle("Set Listings Duration")
                .WithComponents(
                    LocalComponent.Row(LocalComponent.TextInput("minimum", "Set Minimum", TextInputComponentStyle.Short).WithPlaceholder("example: 1m").WithIsRequired(false)),
                    LocalComponent.Row(LocalComponent.TextInput("maximum", "Set Maximum", TextInputComponentStyle.Short).WithPlaceholder("example: 7d").WithIsRequired(false)));

            await e.Interaction.Response().SendModalAsync(modalResponse);

            var response = await Menu.Interactivity.WaitForInteractionAsync(
                e.ChannelId,
                x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == modalResponse.CustomId,
                TimeSpan.FromMinutes(10),
                Menu.StoppingToken);

            if (response == null) return;

            var modal = response.Interaction as IModalSubmitInteraction;
            var rows = modal.Components.OfType<IRowComponent>();
            var minimum = rows.First().Components.OfType<ITextInputComponent>().First().Value;
            var maximum = rows.Last().Components.OfType<ITextInputComponent>().First().Value;
            var emporium = await _context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(e.Interaction.GuildId.Value);
            var settings = (DefaultDiscordGuildSettings)_context.Settings;
            
            if (minimum.IsNotNull())
            {
                var result = _context.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(minimum);

                if (result is ISuccessfulTimeParsingResult<DateTime> successfulResult) 
                    settings.MinimumDuration = (successfulResult.Value - emporium.LocalTime.DateTime).Add(TimeSpan.FromSeconds(1));
                else
                {
                    await modal.Response().SendMessageAsync(
                        new LocalInteractionMessageResponse()
                            .WithIsEphemeral()
                            .AddEmbed(
                                new LocalEmbed()
                                    .WithColor(Color.Red)
                                    .WithDescription("Invalid format: Minimum Duration")));
                    return;
                }
            }

            if (maximum.IsNotNull())
            {
                var result = _context.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(maximum);

                if (result is ISuccessfulTimeParsingResult<DateTime> successfulResult)
                        settings.MaximumDuration = (successfulResult.Value - emporium.LocalTime.DateTime).Add(TimeSpan.FromSeconds(1));
                else
                {
                    await modal.Response().SendMessageAsync(
                        new LocalInteractionMessageResponse()
                            .WithIsEphemeral()
                            .AddEmbed(
                                new LocalEmbed()
                                    .WithColor(Color.Red)
                                    .WithDescription("Invalid format: Maximum Duration")));
                    return;
                }
            }

            using var scope = _context.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Listings Duration Successfully Updated!"));

            await e.Interaction.Followup().ModifyResponseAsync(x => x.Embeds = new[] { settings.ToEmbed() });

            return;
        }

        [Button(Label = "Default to", Style = LocalButtonComponentStyle.Primary, Position = 2, Row = 4)]
        public async ValueTask SetDefault(ButtonEventArgs e)
        {
            var settings = (DefaultDiscordGuildSettings)_context.Settings;

            settings.MinimumDurationDefault = !settings.MinimumDurationDefault;

            using var scope = _context.Services.CreateScope();
            {
                scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new UpdateGuildSettingsCommand(settings));
            }

            e.Button.Label = $"Default to {(_context.Settings.MinimumDurationDefault ? "Max" : "Min")}";

            MessageTemplate = message => message.WithEmbeds(settings.ToEmbed("Default Duration", new LocalEmoji("⏲️")));

            ReportChanges();
        }

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
