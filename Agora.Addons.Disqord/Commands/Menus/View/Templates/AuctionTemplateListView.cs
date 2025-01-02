using Agora.Addons.Disqord.Extensions;
using Disqord;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Emporia.Extensions.Discord.Features.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Agora.Addons.Disqord.Commands
{
    public class AuctionTemplateListView : AuctionTemplateView
    {
        private List<AuctionTemplate> AuctionTemplates { get; set; }

        public AuctionTemplateListView(CachedEmporium emporium, IEnumerable<AuctionTemplate> templates, CultureInfo locale, IServiceProvider provider) : base(emporium, templates,locale, provider)
        {
            AuctionTemplates = templates.ToList();

            foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
            {
                button.Label = TranslateButton(button.Label);
            }
        }

        [Button(Label = "Delete", Style = LocalButtonComponentStyle.Danger, Row = 3)]
        public async ValueTask Delete(ButtonEventArgs e)
        {
            if (CurrentTemplate is null) return;

            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(e);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new DeleteAuctionTemplateCommand(CurrentTemplate.Id));

            if (result.IsSuccessful)
            {
                AuctionTemplates.Remove(CurrentTemplate);

                var successMessage = new LocalInteractionMessageResponse()
                                            .AddEmbed(new LocalEmbed().WithDescription("Auction template deleted.").WithDefaultColor())
                                            .WithComponents()
                                            .WithIsEphemeral();

                if (AuctionTemplates.Count == 0)
                {
                    await e.Interaction.ModifyMessageAsync(successMessage);
                }
                else
                {
                    PageProvider = new ListPageProvider(AuctionTemplates.Select(template => new Page().AddEmbed(template.CreateEmbed())));

                    await Next(e);
                    await e.Interaction.SendMessageAsync(successMessage);
                }
            }
            else
            {
                var failureMessage = new LocalInteractionMessageResponse()
                                            .AddEmbed(new LocalEmbed().WithDescription($"An error occurred attempting to delete template **{CurrentTemplate.Name}**").WithColor(Color.Red))
                                            .WithComponents()
                                            .WithIsEphemeral();

                await e.Interaction.ModifyMessageAsync(failureMessage);

                if (result is IExceptionResult exResult)
                    await scope.ServiceProvider.GetRequiredService<UnhandledExceptionService>().InteractionExecutionFailed(e, exResult.RaisedException);
            }

            return;
        }

        protected override IRequest<IResult<AuctionTemplate>> SaveCommand() => new UpdateAuctionTemplateCommand(CurrentTemplate);

        [Button(Label = "Back", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public ValueTask Previous(ButtonEventArgs e)
        {
            if (CurrentPageIndex == 0)
                CurrentPageIndex = PageProvider.PageCount - 1;
            else
                CurrentPageIndex--;

            CurrentTemplate = AuctionTemplates[CurrentPageIndex];

            return default;
        }

        [Button(Label = "Next", Style = LocalButtonComponentStyle.Primary, Row = 4)]
        public ValueTask Next(ButtonEventArgs e)
        {
            if (CurrentPageIndex + 1 >= PageProvider.PageCount)
                CurrentPageIndex = 0;
            else
                CurrentPageIndex++;

            CurrentTemplate = AuctionTemplates[CurrentPageIndex];

            return default;
        }

        [Button(Label = "Preview", Style = LocalButtonComponentStyle.Success, Row = 4)]
        public async ValueTask Preview(ButtonEventArgs e)
        {
            var embeds = new List<LocalEmbed>() { PreviewEmbed("Auction") };

            if (!CurrentTemplate.Validate(out var error)) embeds.Add(new LocalEmbed().WithDescription(error).WithColor(Color.Red));

            await e.Interaction.SendMessageAsync(new LocalInteractionMessageResponse().WithEmbeds(embeds).WithIsEphemeral());
        }

        protected override string GetCustomId(InteractableViewComponent component)
        {
            if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

            return base.GetCustomId(component);
        }
    }
}
