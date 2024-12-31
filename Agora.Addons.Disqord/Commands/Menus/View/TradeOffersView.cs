using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord.Commands.View;

public class TradeOffersView : PagedViewBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<Deal> _deals;
    private readonly ulong _emporiumId;
    private readonly ulong _showroomId;
    private readonly ulong _reference;
    private readonly Listing _listing;

    private Deal _selectedOffer;

    public TradeOffersView(IServiceScopeFactory scopeFactory, Listing listing, IEnumerable<Deal> deals)
        : base(new ListPageProvider(deals.Chunk(5).Select((offers, index) =>
        {
            var shift = index * 5;
            var embed = new LocalEmbed().WithTitle($"Counter offers submitted for this listing: {deals.Count()}");

            foreach (var offer in offers.Select((deal, index) => (deal, idx: index + 1)))
            {
                var details = string.Empty;

                if (!string.IsNullOrWhiteSpace(offer.deal.Details)) details = $"Details: {offer.deal.Details}";

                embed.AddField($"Offer {offer.idx + shift}",
                               $"{Mention.User(offer.deal.UserReference.Value)} is offering {Markdown.Bold(offer.deal.Submission)}\n\n{details}");
            }

            return new Page().AddEmbed(embed.WithDefaultColor());
        }).ToArray()))
    {
        _reference = listing.Product.ReferenceNumber.Value;
        _emporiumId = listing.Owner.EmporiumId.Value;
        _showroomId = listing.ShowroomId.Value;
        _scopeFactory = scopeFactory;
        _deals = deals.ToList();
        _listing = listing;

        UpdateSelection();
        ToggleButtons(true);
    }

    public override void FormatLocalMessage(LocalMessageBase message)
    {
        base.FormatLocalMessage(message);

        if (message is LocalInteractionMessageResponse response)
            response.WithIsEphemeral();
    }

    [Button(Label = "Back", Style = LocalButtonComponentStyle.Primary, Row = 1)]
    public ValueTask Previous(ButtonEventArgs e)
    {
        if (PageProvider.PageCount == 1) return default;

        if (CurrentPageIndex == 0)
            CurrentPageIndex = PageProvider.PageCount - 1;
        else
            CurrentPageIndex--;

        UpdateSelection();
        ToggleButtons(true);

        return default;
    }

    [Button(Label = "Next", Style = LocalButtonComponentStyle.Primary, Row = 1)]
    public ValueTask NextTip(ButtonEventArgs e)
    {
        if (PageProvider.PageCount == 1) return default;

        if (CurrentPageIndex + 1 == PageProvider.PageCount)
            CurrentPageIndex = 0;
        else
            CurrentPageIndex++;

        UpdateSelection();
        ToggleButtons(true);

        return default;
    }

    [Button(Label = "Close", Style = LocalButtonComponentStyle.Secondary, Row = 1)]
    public ValueTask CloseView(ButtonEventArgs e) => default;

    [Selection(MaximumSelectedOptions = 1, Row = 2)]
    public ValueTask SelectOffer(SelectionEventArgs e)
    {
        if (e.SelectedOptions.Count == 0) return default;

        var trader = e.SelectedOptions[0].Value.Value;

        e.Selection.Options.First(x => x.Value.Value == trader).WithIsDefault();

        _selectedOffer = _deals.First(x => x.UserReference.Value == ulong.Parse(trader));

        ToggleButtons(false);

        return default;
    }

    [Button(Label = "Reject Offer", Style = LocalButtonComponentStyle.Danger, Row = 3)]
    public async ValueTask RejectOffer(ButtonEventArgs args)
    {
        var response = new LocalInteractionModalResponse()
            .WithCustomId(args.Interaction.Message.Id.ToString())
            .WithTitle("Reject Offer")
            .WithComponents(LocalComponent.Row(new LocalTextInputComponent()
            {
                Style = TextInputComponentStyle.Short,
                CustomId = "reason",
                Label = "Reason",
                Placeholder = "Reason for rejecting this offer",
                MaximumInputLength = 150,
                IsRequired = true
            }));

        await args.Interaction.Response().SendModalAsync(response);

        var reply = await Menu.Interactivity.WaitForInteractionAsync(channelId: args.ChannelId,
                                                                     predicate: x => x.Interaction is IModalSubmitInteraction modal && modal.CustomId == response.CustomId,
                                                                     timeout: TimeSpan.FromMinutes(10),
                                                                     cancellationToken: Menu.StoppingToken);

        if (reply == null) return;

        var modal = reply.Interaction as IModalSubmitInteraction;
        var reason = modal.Components.OfType<IRowComponent>().First().Components.OfType<ITextInputComponent>().First().Value;

        var bot = Menu.Client as DiscordBotBase;

        using var scope = bot.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(args);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new RejectDealCommand(new EmporiumId(_emporiumId), new ShowroomId(_showroomId), ReferenceNumber.Create(_reference), _selectedOffer.User)
        {
            Reason = reason
        });

        _deals.Remove(_selectedOffer);

        await modal.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Offer removed").WithIsEphemeral());

        if (_deals.Count == 0)
        {
            await args.Interaction.Followup().DeleteResponseAsync();
            return;
        }

        UpdatePages();
        UpdateSelection();
        ToggleButtons(true);

        return;
    }

    [Button(Label = "Negotiate Offer", Style = LocalButtonComponentStyle.Primary, Row = 3)]
    public async ValueTask Negotiate(ButtonEventArgs args)
    {
        var currentMember = Menu.Client.GetCurrentMember(_emporiumId);
        var currentPerms = currentMember.CalculateGuildPermissions();
        var perms = Permissions.ManageChannels | Permissions.ManageRoles;

        if (!currentPerms.HasFlag(perms))
        {
            var guild = currentMember.GetGuild();

            await args.Interaction.Response()
                .SendMessageAsync(new LocalInteractionMessageResponse().WithContent($"The bot lacks permissions ({perms & ~currentPerms}) in {guild.Name}"));

            return;
        }

        var showroom = Menu.Client.GetChannel(_emporiumId, _showroomId);
        var categoryId = showroom is ICategoryChannel category ? category.Id : (showroom as ICategorizableGuildChannel).CategoryId;
        var owner = _listing.Owner.ReferenceNumber.Value;
        var trader = _selectedOffer.UserReference.Value;

        var channel = await Menu.Client.CreateTextChannelAsync(_emporiumId, $"trade-{_listing.ReferenceCode.Code()}-negotiations", x =>
        {
            x.CategoryId = categoryId.Value;
            x.Overwrites = new[]
            {
                LocalOverwrite.Member(currentMember.Id, new OverwritePermissions().Allow(Permissions.ViewChannels | Permissions.SendMessages)),
                LocalOverwrite.Role(_emporiumId, new OverwritePermissions().Deny(Permissions.ViewChannels)),
                LocalOverwrite.Member(trader, new OverwritePermissions().Allow(Permissions.ViewChannels).Deny(Permissions.SendMessages)),
                LocalOverwrite.Member(owner, new OverwritePermissions().Allow(Permissions.ViewChannels).Deny(Permissions.SendMessages))
            };
        });

        await channel.SendMessageAsync(
            new LocalMessage()
                .WithContent($"{Mention.User(trader)}, {Mention.User(owner)} would like to open negotiations for {Markdown.Bold(_listing.Product.Title)}")
                .WithComponents(LocalComponent.Row(
                    LocalComponent.LinkButton(Discord.MessageJumpLink(_emporiumId, _showroomId, _reference), "View Item"),
                    LocalComponent.Button($"#closeNegotiations:{trader}:{owner}:{_showroomId}:{_reference}", "Reject").WithStyle(LocalButtonComponentStyle.Danger),
                    LocalComponent.Button($"#openNegotiations:{trader}:{owner}:{_showroomId}:{_reference}", "Accept").WithStyle(LocalButtonComponentStyle.Success))));

        await args.Interaction.Response()
            .SendMessageAsync(new LocalInteractionMessageResponse().WithContent($"Negotiation channel ({channel.Mention}) created").WithIsEphemeral());

        await args.Interaction.Followup().DeleteAsync(args.Interaction.Message.Id);

        return;
    }

    [Button(Label = "Accept Offer", Style = LocalButtonComponentStyle.Success, Row = 3)]
    public async ValueTask Acknowledge(ButtonEventArgs args)
    {
        (_listing as StandardTrade).UpdateCurrentOffer(_selectedOffer);

        var bot = Menu.Client as DiscordBotBase;

        using var scope = bot.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IInteractionContextAccessor>().Context = new DiscordInteractionContext(args);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await mediator.Send(new AcceptListingCommand(new EmporiumId(_emporiumId), new ShowroomId(_showroomId), ReferenceNumber.Create(_reference), ListingType.Trade.ToString()));

            await args.Interaction.Response().ModifyMessageAsync(
                new LocalInteractionMessageResponse()
                    .WithContent("Offer accepted!")
                    .WithEmbeds().WithComponents()
                    .WithIsEphemeral());
        }
        catch (UnauthorizedAccessException ex)
        {
            await args.Interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent(ex.Message).WithIsEphemeral());
        }

        return;
    }

    protected override string GetCustomId(InteractableViewComponent component)
    {
        if (component is ButtonViewComponent buttonComponent) return $"#{buttonComponent.Label}";

        return base.GetCustomId(component);
    }

    private void UpdatePages()
    {
        PageProvider = new ListPageProvider(_deals.Chunk(5).Select((offers, index) =>
        {
            var shift = index * 5;
            var embed = new LocalEmbed().WithTitle($"Counter offers submitted for this listing: {_deals.Count}");

            foreach (var offer in offers.Select((deal, index) => (deal, idx: index + 1)))
            {
                var details = string.Empty;

                if (!string.IsNullOrWhiteSpace(offer.deal.Details)) details = $"Details: {offer.deal.Details}";

                embed.AddField($"Offer {offer.idx + shift}",
                               $"{Mention.User(offer.deal.UserReference.Value)} is offering {Markdown.Bold(offer.deal.Submission)}\n\n{details}");
            }

            return new Page().AddEmbed(embed.WithDefaultColor());
        }).ToArray());
    }

    private void UpdateSelection()
    {
        var selection = EnumerateComponents().OfType<SelectionViewComponent>().First();

        selection.Options.Clear();

        var shift = CurrentPageIndex * 5;
        var offers = _deals.Chunk(5).ToArray()[CurrentPageIndex];

        foreach (var offer in offers.Select((deal, index) => (deal, idx: index + 1)))
        {
            var user = offer.deal.UserReference.Value;
            selection.Options.Add(new LocalSelectionComponentOption($"Offer {offer.idx + shift}", user.ToString()));
        }
    }

    private void ToggleButtons(bool state)
    {
        foreach (var button in EnumerateComponents().OfType<ButtonViewComponent>())
        {
            button.Label = TranslateButton(button.Label);

            if (button.Row == 3) button.IsDisabled = state;
        }
    }

    private string TranslateButton(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        localization.SetCulture(Menu.Client.GetCurrentMember(_emporiumId).GetGuild().PreferredLocale);

        return localization.Translate(key, "ButtonStrings");
    }
}
