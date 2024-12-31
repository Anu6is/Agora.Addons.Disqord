using Agora.Addons.Disqord.Commands.Checks;
using Agora.Addons.Disqord.Commands.View;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands.Components;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Application.Features.Queries;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Modules;

public sealed class TradeDealComponentModule : DiscordComponentModuleBase
{
    private readonly IMediator _mediator;
    private readonly IUserManager _userManager;
    private readonly IEmporiaCacheService _cache;
    private readonly ICurrentUserService _userService;
    private readonly IServiceScopeFactory _scopeFactory;

    public TradeDealComponentModule(IMediator mediator, ICurrentUserService userService, IUserManager userManager, IEmporiaCacheService cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _mediator = mediator;
        _userManager = userManager;
        _userService = userService;
        _scopeFactory = scopeFactory;
    }

    [ButtonCommand("#offers")]
    public async Task<IResult> ViewOffers()
    {
        await Deferral();

        var emporium = await _cache.GetEmporiumAsync(Context.GuildId.Value);
        var message = (Context.Interaction as IComponentInteraction).Message;

        if (!TryDetermineShowroom(emporium, Context.ChannelId, out var showroomId)) return Results.Failure("Unable to identify showroom");

        var reference = ReferenceNumber.Create(message.Id);
        var response = await _mediator.Send(new GetListingDetailsQuery(new EmporiumId(emporium.EmporiumId), showroomId, "Trade")
        {
            ReferenceNumber = reference
        });

        if (response.Data == null) return Results.Failure("Unable to retrieve listing details!");

        if (response.Data.Listing.Owner.ReferenceNumber.Value != Context.AuthorId)
            return Response(new LocalInteractionMessageResponse().WithIsEphemeral().WithContent("Unauthorized Access: Only the OWNER can view trade offers!"));

        var item = (TradeItem)response.Data.Listing.Product;

        return View(new TradeOffersView(_scopeFactory, response.Data.Listing, item.Offers.OrderBy(x => x.SubmittedOn).ToArray()));
    }

    [ButtonCommand("#closeNegotiations:*:*:*:*")]
    public async Task RejectNegotiations([RequireInvoker] Snowflake user, Snowflake owner, Snowflake showroomId, Snowflake reference)
    {
        try
        {
            var dm = await Context.Bot.CreateDirectChannelAsync(owner);

            await dm.SendMessageAsync(
                new LocalMessage()
                        .AddEmbed(new LocalEmbed().WithDescription($"{Mention.User(user)} declined negotiations!").WithDefaultColor())
                        .AddComponent(LocalComponent.Row(
                            LocalComponent.LinkButton(Discord.MessageJumpLink(Context.GuildId.Value, showroomId, reference), TranslateButton("View Item")))));
        }
        catch (Exception) { }

        await Context.Bot.GetChannel(Context.GuildId.Value, Context.ChannelId).DeleteAsync();
    }

    [ButtonCommand("#openNegotiations:*:*:*:*")]
    public async Task OpenNegotiations([RequireInvoker] Snowflake user, Snowflake owner, Snowflake showroomId, Snowflake reference)
    {
        await Context.Bot.ModifyTextChannelAsync(Context.ChannelId, x =>
        {
            x.Overwrites = new[]
            {
                LocalOverwrite.Member(user, new OverwritePermissions().Allow(Permissions.ViewChannels | Permissions.SendMessages)),
                LocalOverwrite.Member(owner, new OverwritePermissions().Allow(Permissions.ViewChannels | Permissions.SendMessages))
            };
        });

        await Context.Interaction.Response()
            .SendMessageAsync(
                new LocalInteractionMessageResponse()
                    .WithContent($"{Mention.User(owner)}, {Mention.User(user)} has agreed to negotiations.")
                    .AddEmbed(new LocalEmbed().WithDescription($"{Mention.User(user)} when ready, submit your final offer here").WithDefaultColor())
                    .AddComponent(LocalComponent.Row(LocalComponent.Button($"finalize:{user}:{showroomId}:{reference}", TranslateButton("Submit Final Offer")).WithStyle(LocalButtonComponentStyle.Success))));

        await Context.Interaction.Followup().DeleteAsync((Context.Interaction as IComponentInteraction).Message.Id);

        var message = await Context.Interaction.Followup().FetchResponseAsync();
        var currentMember = Bot.GetCurrentMember(Context.GuildId.Value);
        var channel = Bot.GetChannel(Context.GuildId.Value, message.ChannelId);
        var channelPerms = currentMember.CalculateChannelPermissions(channel);

        if (channelPerms.HasFlag(Permissions.ManageMessages)) await message.PinAsync();

        return;
    }

    [ButtonCommand("#reject:*:*:*:*")]
    public async Task RejectOffer(ulong guildId, ulong channelId, ulong messageId, ulong userId)
    {
        var interaction = Context.Interaction as IComponentInteraction;

        var response = new LocalInteractionModalResponse()
            .WithCustomId($"reject:{guildId}:{channelId}:{messageId}:{userId}")
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
        await interaction.Response().SendModalAsync(response);

        return;
    }

    [ModalCommand("reject:*:*:*:*")]
    public async Task RejectOfferModal(ulong guildId, ulong channelId, ulong messageId, ulong userId, string reason)
    {
        var trader = await _cache.GetUserAsync(guildId, userId);
        var owner = await _cache.GetUserAsync(guildId, Context.AuthorId);

        _userService.CurrentUser = owner.ToEmporiumUser();
        (_userManager as UserManagerService).SetGuildId(guildId);

        try
        {
            await _mediator.Send(new RejectDealCommand(new EmporiumId(guildId), new ShowroomId(channelId), ReferenceNumber.Create(messageId), trader.ToEmporiumUser())
            {
                Reason = reason
            });

            await Context.Interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse().WithContent("**Rejected**").WithComponents());
        }
        catch (Exception)
        {
            await Context.Interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse().WithComponents());
        }

    }

    [ButtonCommand("#negotiate:*:*:*:*")]
    public async Task<IResult> NegotiateOffer([RequireGuildPermissions(Permissions.ManageChannels | Permissions.ManageRoles)] ulong guildId,
                                              ulong channelId,
                                              ulong messageId,
                                              ulong userId)
    {
        await Deferral();

        var response = await _mediator.Send(new GetListingDetailsQuery(new EmporiumId(guildId), new ShowroomId(channelId), "Trade")
        {
            ReferenceNumber = ReferenceNumber.Create(messageId)
        });

        if (response.Data == null) return Response(new LocalEmbed().WithDescription("Unable to locate listing").WithDefaultColor());

        var showroom = Context.Bot.GetChannel(guildId, channelId);

        if (showroom == null) return Response(new LocalEmbed().WithDescription("Unable to locate showroom").WithDefaultColor()); ;

        var categoryId = showroom is ICategoryChannel category ? category.Id : (showroom as ICategorizableGuildChannel).CategoryId;
        var listing = response.Data.Listing;
        var owner = Context.AuthorId;

        var channel = await Context.Bot.CreateTextChannelAsync(guildId, $"trade-{listing.ReferenceCode.Code()}-negotiations", x =>
        {
            x.CategoryId = categoryId.Value;
            x.Overwrites = new[]
            {
                LocalOverwrite.Member(Context.Bot.CurrentUser.Id, new OverwritePermissions().Allow(Permissions.ViewChannels | Permissions.SendMessages)),
                LocalOverwrite.Role(guildId, new OverwritePermissions().Deny(Permissions.ViewChannels)),
                LocalOverwrite.Member(userId, new OverwritePermissions().Allow(Permissions.ViewChannels).Deny(Permissions.SendMessages)),
                LocalOverwrite.Member(owner, new OverwritePermissions().Allow(Permissions.ViewChannels).Deny(Permissions.SendMessages))
            };
        });

        await channel.SendMessageAsync(
            new LocalMessage() 
                .WithContent($"{Mention.User(userId)}, {Mention.User(owner)} would like to open negotiations for {Markdown.Bold(listing.Product.Title)}")
                .WithComponents(LocalComponent.Row(
                    LocalComponent.LinkButton(Discord.MessageJumpLink(guildId, channelId, messageId), TranslateButton("View Item")),
                    LocalComponent.Button($"#closeNegotiations:{userId}:{owner}:{channelId}:{messageId}", TranslateButton("Reject")).WithStyle(LocalButtonComponentStyle.Danger),
                    LocalComponent.Button($"#openNegotiations:{userId}:{owner}:{channelId}:{messageId}", TranslateButton("Accept")).WithStyle(LocalButtonComponentStyle.Success))));

        await Context.Interaction.Followup().ModifyResponseAsync(x =>
        {
            x.Content = "Negetiations requested";
            x.Components = Array.Empty<LocalRowComponent>();
        });

        return Results.Success;
    }

    [ButtonCommand("#acknowledge:*:*:*:*")]
    public async Task<IResult> AcceptOffer(ulong guildId, ulong channelId, ulong messageId, ulong userId)
    {
        await Deferral();

        var owner = await _cache.GetUserAsync(guildId, Context.AuthorId);

        _userService.CurrentUser = owner.ToEmporiumUser();
        (_userManager as UserManagerService).SetGuildId(guildId);

        var response = await _mediator.Send(new GetListingDetailsQuery(new EmporiumId(guildId), new ShowroomId(channelId), "Trade")
        {
            ReferenceNumber = ReferenceNumber.Create(messageId)
        });

        if (response.Data == null) return Response(new LocalEmbed().WithDescription("Unable to locate listing").WithDefaultColor());

        var listing = response.Data.Listing;
        var selectedOffer = (listing.Product as TradeItem).Offers.First(x => x.UserReference.Value == userId);

        (listing as StandardTrade).UpdateCurrentOffer(selectedOffer);

        try
        {
            await _mediator.Send(new AcceptListingCommand(new EmporiumId(guildId), new ShowroomId(channelId), ReferenceNumber.Create(messageId), "Trade"));

            await Context.Interaction.Followup().ModifyResponseAsync(x =>
            {
                x.Content = "Offer Accepted";
                x.Components = Array.Empty<LocalRowComponent>();
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            await Context.Interaction.Followup().SendAsync(new LocalInteractionFollowup().WithContent(ex.Message));
        }

        return Results.Success;
    }

    private bool TryDetermineShowroom(CachedEmporium emporium, ulong channelId, out ShowroomId id)
    {
        id = null;

        if (emporium == null) return false;

        if (emporium.Showrooms.Any(x => x.Id.Value.Equals(channelId)))
            id = new ShowroomId(channelId);
        else
            id = Context.Bot.GetChannel(emporium.EmporiumId, channelId) switch
            {
                ITextChannel textChannel => new ShowroomId(textChannel.CategoryId.GetValueOrDefault()),
                IThreadChannel threadChannel => new ShowroomId(threadChannel.ChannelId),
                _ => null
            };

        return id != null;
    }

    private string TranslateButton(string key)
    {
        using var scope = _scopeFactory.CreateScope();
        var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        localization.SetCulture(Context.GuildLocale);

        return localization.Translate(key, "ButtonStrings");
    }
}
