using Agora.Addons.Disqord.Extensions;
using Agora.Shared.EconomyFactory;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Events;
using Emporia.Extensions.Discord;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using MediatR;

namespace Extension.TransactionFees.Application;

internal class PremiumListingService(DiscordBot bot, EconomyFactoryService economyFactory,
                                     IMediator mediator,
                                     IEmporiaCacheService cache,
                                     IDataAccessor dataAccessor,
                                     IGuildSettingsService settingsService) : INotificationHandler<ListingRemovedEvent>,
                                                                              INotificationHandler<ListingActivatedNotification>
{
    public async Task Handle(ListingRemovedEvent @event, CancellationToken cancellationToken)
    {
        var listing = @event.ProductListing;

        var premiumListing = await dataAccessor.Transaction<GenericRepository<PremiumListing>>().GetByIdAsync(listing.Id, cancellationToken);

        if (premiumListing is null) return;

        var guildId = listing.Owner.EmporiumId.Value;

        await bot.DeleteRoleAsync(guildId, premiumListing.EntryRoleId, cancellationToken: cancellationToken);

        if (@event.ProductListing.Status == ListingStatus.Sold) return;

        var guildSettings = await settingsService.GetGuildSettingsAsync(guildId);

        if (guildSettings.EconomyType == EconomyType.Disabled.ToString()) return;

        var feesCollected = 0m;
        var fee = Money.Create(premiumListing.EntryFee.Value, listing.Product.Value().Currency);
        var economy = economyFactory.Create(guildSettings.EconomyType);

        foreach (var userId in premiumListing.EntryList)
        {
            if (userId == listing.Owner.ReferenceNumber.Value) continue;

            var user = await cache.GetUserAsync(guildId, userId);

            await economy.IncreaseBalanceAsync(user.ToEmporiumUser(), fee, "Auction Registration Fee Refunded");

            feesCollected += fee.Value;

            await Task.Delay(200, cancellationToken);
        }

        if (feesCollected == 0) return;

        var reimbursement = Money.Create(feesCollected, listing.Product.Value().Currency);

        await economy.DecreaseBalanceAsync(listing.Owner, reimbursement, "Auction Registration Fees Reimbursed");
    }

    public async Task Handle(ListingActivatedNotification notification, CancellationToken cancellationToken)
    {
        var listing = notification.Listing;

        var premiumListing = await dataAccessor.Transaction<GenericRepository<PremiumListing>>().GetByIdAsync(listing.Id, cancellationToken);

        if (premiumListing is null) return;

        var entries = premiumListing.EntryList.Count;

        if (entries == 0 || entries < premiumListing.RequiredEntries)
        {
            listing.UpdateStatus(ListingStatus.Expired);
            await mediator.Send(new CloseListingCommand(listing.Owner.EmporiumId, listing.ShowroomId, listing.Id, ListingType.Auction.ToString()), cancellationToken);
        }
    }
}
