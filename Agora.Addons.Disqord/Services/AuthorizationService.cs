using Agora.Shared.Attributes;
using Agora.Shared.EconomyFactory;
using Agora.Shared.Services;
using Disqord;
using Disqord.Bot;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Extension;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class AuthorizationService : AgoraService, IAuthorizationService
    {
        public bool IsAuthorized { get; set; }

        private readonly DiscordBotBase _agora;
        private readonly IUserManager _userManager;
        private readonly IGuildSettingsService _guildSettingsService;

        public AuthorizationService(DiscordBotBase bot,
                                    IGuildSettingsService settingsService,
                                    IUserManager userManager,
                                    ILogger<AuthorizationService> logger) : base(logger)
        {
            _agora = bot;
            _userManager = userManager;
            _guildSettingsService = settingsService;
        }

        public async ValueTask<IResult> AuthroizeAsync<TRequest>(TRequest request, IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributes)
        {
            IResult result = Result.Success();

            var authorizeAttributesWithRoles = authorizeAttributes.Where(a => a.Role != AuthorizationRole.None);

            if (authorizeAttributesWithRoles.Any())
                result = await ValidateRolesAsync(currentUser, authorizeAttributesWithRoles);

            if (!result.IsSuccessful) return result;

            var authorizeAttributesWithPolicies = authorizeAttributes.Where(a => a.Policy != AuthorizationPolicy.None);

            if (authorizeAttributesWithPolicies.Any())
                result = await ValaidatePoliciesAsync(request, currentUser, authorizeAttributesWithPolicies);

            return result;
        }

        private async Task<IResult> ValidateRolesAsync(IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributesWithRoles)
        {
            IResult result = Result.Success();

            foreach (var role in authorizeAttributesWithRoles.Select(a => a.Role))
            {
                result = role switch
                {
                    AuthorizationRole.Administrator => await _userManager.IsAdministrator(currentUser),
                    AuthorizationRole.Broker => await _userManager.IsBroker(currentUser),
                    AuthorizationRole.Host => await _userManager.IsHost(currentUser),
                    AuthorizationRole.Buyer => await _userManager.ValidateBuyerAsync(currentUser),
                    _ => null
                };

                if (!result.IsSuccessful) return result;
            }

            return result;
        }

        private async Task<IResult> ValaidatePoliciesAsync<TRequest>(TRequest request, IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributesWithPolicies)
        {
            var authorizationError = string.Empty;

            foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
            {
                authorizationError = policy switch
                {
                    AuthorizationPolicy.CanSubmitOffer => await ValidateSubmissionAsync(currentUser, request),
                    AuthorizationPolicy.CanModify => await ValidateUpdateAsync(currentUser, request),
                    AuthorizationPolicy.Manager => await ValidateManagerAsync(currentUser, request),
                    AuthorizationPolicy.OwnerOnly => await ValidateOwnerAsync(currentUser, request),
                    AuthorizationPolicy.StaffOnly => ValidateStaff(),
                    _ => null
                };

                if (authorizationError.IsNotNull()) return Result.Failure(authorizationError);
            }

            return Result.Success();
        }

        private async Task<string> ValidateUpdateAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            IResult result = null;
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);
            var managerRole = settings.AdminRole == 0 ? Markdown.Bold("Manager Privileges") : Mention.Role(settings.AdminRole);

            if (request is IProductListingBinder binder && binder.Showroom.Listings.FirstOrDefault() is null)
                return "Invalid Action: This listing is no longer available.";

            switch (request)
            {
                case AcceptListingCommand command:
                    if (command.ListingType != ListingType.Auction.ToString()) return string.Empty;
                    if (!settings.Features.AcceptOffers) return "Invalid Operation: This action has been disabled.";

                    var canAcceptFrom = command.Showroom.Listings.First().ScheduledPeriod.ScheduledStart.Add(settings.MinimumDuration).ToUniversalTime();
                    var duration = settings.MinimumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);
                    var remaining = canAcceptFrom.Subtract(SystemClock.Now).Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);

                    if (canAcceptFrom > SystemClock.Now)
                        return $"Invalid Operation: Item has to be listed for at least {duration}. {remaining} remaining.";
                    break;
                case RevertTransactionCommand command:
                    if (command.Showroom.Listings.First() is StandardMarket { AllowOffers: true } market && market.Status < ListingStatus.Withdrawn) return string.Empty;
                    if (!settings.Features.ConfirmTransactions) return "Invalid Operation: This action has been disabled.";
                    break;
                case WithdrawListingCommand command:
                    result = await _userManager.IsAdministrator(currentUser);

                    if (result.IsSuccessful) return string.Empty;
                    if (!settings.Features.RecallListings && command.Showroom.Listings.First().CurrentOffer != null)
                        return "Invalid Operation: Listing cannot be withdrawn once an offer has been submitted.";
                    break;
                case UndoBidCommand command:
                    result = await _userManager.IsAdministrator(currentUser);

                    if (result.IsSuccessful) return string.Empty;

                    var listing = command.Showroom.Listings.First();
                    var currentOffer = listing.CurrentOffer;

                    if (currentOffer == null)
                        return "Invalid Operation: No available bids exist.";

                    if (settings.BiddingRecallLimit == Timeout.InfiniteTimeSpan) return string.Empty;

                    if (settings.BiddingRecallLimit == TimeSpan.Zero)
                        return $"Unauthorized Action: Only users with {managerRole} can undo bids";

                    var limit = settings.BiddingRecallLimit.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);

                    if (currentOffer.SubmittedOn.ToUniversalTime().Add(settings.BiddingRecallLimit) < SystemClock.Now)
                        return $"Invalid Operation: This action is no longer available. Bids can only be withdrawn up to {limit} after submission.";
                    break;
                case UpdateMarketItemCommand command:
                    if (command.Showroom.Listings.First().Status >= ListingStatus.Locked)
                        return $"Invalid Operation: This action is no longer available. Changes cannot be made once an offer was previously submitted.";
                    break;
                case IProductListingBinder command:
                    result = await _userManager.IsAdministrator(currentUser);

                    if (result.IsSuccessful) return string.Empty;
                    if (command.Showroom.Listings.First().CurrentOffer != null)
                        return "Invalid Operation: Updates cannot be made once an offer has been submitted.";
                    break;
                default:
                    break;
            }

            return string.Empty;
        }

        private async Task<string> ValidateSubmissionAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);

            switch (request)
            {
                case CreateCommissionTradeCommand command:
                    var validCommission = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == "Disabled") return Result.Success();

                        var cmd = command as CreateCommissionTradeCommand;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, cmd.TradeItemModel.PreferredOffer.Currency);

                        if (!userBalance.IsSuccessful) return userBalance;

                        if (userBalance.Data >= cmd.TradeItemModel.PreferredOffer) return Result.Success();

                        return Result.Failure("Transaction Denied: Insufficient balance available to request this commission.");
                    });

                    if (!validCommission.IsSuccessful) return validCommission.FailureReason;

                    break;
                case CreateBidCommand command:
                    var listing = command.Showroom.Listings.FirstOrDefault();

                    if (listing == null) return "Invalid Action: Listing is no longer available";

                    if (!settings.Features.AllowShillBidding && currentUser.Equals(listing.Owner))
                        return "Transaction Denied: You cannot bid on an item you listed. Enable **Shill Bidding** in </server settings:1013361602499723275>.";

                    var validBid = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == "Disabled")
                            return Result.Success();

                        var cmd = command as CreateBidCommand;
                        var item = listing.Product as AuctionItem;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, item.StartingPrice.Currency);
                        var balance = userBalance.Data.Value;

                        if (cmd.AuthorizeOnly && !cmd.UseMinimum && !cmd.UseMaximum)
                            return Result.Success();

                        if (cmd.UseMinimum && cmd.UseMaximum && balance >= BuyNowPrice(listing).Value)
                            return Result.Success();

                        if (cmd.UseMinimum && !cmd.UseMaximum && balance >= item.CurrentPrice.Value + item.BidIncrement.MinValue)
                            return Result.Success();

                        if (cmd.UseMaximum && !cmd.UseMinimum && balance >= item.CurrentPrice.Value + item.BidIncrement.MaxValue.Value)
                            return Result.Success();

                        if (cmd.Amount > 0 && balance >= cmd.Amount)
                            return Result.Success();

                        return Result.Failure("Transaction Denied: Insufficient balance available to complete this transaction.");
                    });

                    if (!validBid.IsSuccessful) return validBid.FailureReason;

                    break;

                case CreatePaymentCommand command:
                    var sale = command.Showroom.Listings.FirstOrDefault();

                    if (sale == null) return "Invalid Action: Listing is no longer available";

                    if (currentUser.Equals(sale.Owner))
                        return "Transaction Denied: you cannot purchase an item you listed.";

                    var validPurchase = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == "Disabled") return Result.Success();

                        var cmd = command as CreatePaymentCommand;

                        var item = cmd.Showroom.Listings.First().Product as MarketItem;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, item.Price.Currency);

                        if (userBalance.Data >= cmd.PaymentAmount || (cmd.Offer.HasValue && userBalance.Data >= cmd.Offer.Value)) return Result.Success();

                        return Result.Failure("Transaction Denied: Insufficient balance available to complete this transaction.");
                    });

                    if (!validPurchase.IsSuccessful) return validPurchase.FailureReason;

                    break;
                case CreateTicketCommand command:
                    var giveaway = command.Showroom.Listings.FirstOrDefault();

                    if (giveaway == null) return "Invalid Action: Listing is not longer available";

                    if (giveaway is StandardGiveaway)
                    {
                        var hasAccess = await _userManager.ValidateBuyerAsync(currentUser, command);

                        if (hasAccess.IsSuccessful) return string.Empty;

                        return hasAccess.FailureReason;
                    }

                    var validClaim = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == "Disabled") return Result.Success();

                        var cmd = command as CreateTicketCommand;
                        var item = cmd.Showroom.Listings.First().Product as GiveawayItem;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, item.TicketPrice.Currency);

                        if (userBalance.Data >= item.TicketPrice) return Result.Success();

                        return Result.Failure("Transaction Denied: Insufficient balance available to complete this transaction.");
                    });

                    if (!validClaim.IsSuccessful) return validClaim.FailureReason;

                    break;
                case CreateDealCommand command:
                    var trade = command.Showroom.Listings.FirstOrDefault();

                    if (trade == null) return "Invalid Action: Listing is not longer available";

                    if (currentUser.Equals(trade.Owner))
                        return "Transaction Denied: you cannot trade with yourself.";

                    if (trade.Product is TradeItem item && item.Offers.Any(x => x.UserId == command.CurrentUser.Id))
                        return "Invalid Action: You already made an offer on this item.";

                    var canClaim = await _userManager.ValidateBuyerAsync(currentUser, command);

                    if (!canClaim.IsSuccessful) return canClaim.FailureReason;

                    if (trade is not CommissionTrade) return string.Empty;

                    var validRequest = await _userManager.ValidateBuyerAsync(trade.Owner, command, async (owner, command) =>
                    {
                        if (settings.EconomyType == "Disabled") return Result.Success();

                        var cmd = command as CreateDealCommand;
                        var request = cmd.Showroom.Listings.First() as CommissionTrade;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(owner, request.Commission.Currency);

                        if (userBalance.Data >= request.Commission) return Result.Success();

                        return Result.Failure("Transaction Denied: Requester's remaining balance is insufficient for payout.");
                    });

                    if (!validRequest.IsSuccessful) return validRequest.FailureReason;

                    break;
                default:
                    return string.Empty;
            }

            return string.Empty;
        }

        private static Money BuyNowPrice(Listing listing)
        {
            if (listing is StandardAuction standard) return standard.BuyNowPrice;
            if (listing is LiveAuction live) return live.BuyNowPrice;

            return null;
        }

        private async Task<string> ValidateManagerAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {

            if (currentUser.Equals((request as IProductListingBinder)?.Showroom.Listings.FirstOrDefault()?.Owner)) return string.Empty;

            var result = await _userManager.IsAdministrator(currentUser);

            if (result.IsSuccessful) return string.Empty;

            switch (request)
            {
                case WithdrawListingCommand command:
                    if (currentUser.Equals(command.Showroom.Listings.FirstOrDefault()?.Owner)) return string.Empty;

                    break;
                case UndoBidCommand command:
                    if (command.Showroom.Listings.FirstOrDefault() is VickreyAuction
                        || command.Bidder.Id.Equals(command.Showroom.Listings.FirstOrDefault()?.CurrentOffer.UserId)) return string.Empty;

                    break;
                case ScheduleListingCommand:
                case UnscheduleListingCommand:
                    break;
                case RevertTransactionCommand command:
                    if (currentUser.Id.Equals(command.Showroom.Listings.FirstOrDefault()?.CurrentOffer.UserId)) return string.Empty;

                    break;
                case IProductListingBinder:
                    result = await _userManager.IsBroker(currentUser);

                    if (result.IsSuccessful) return string.Empty;

                    break;
                default:
                    break;
            }

            return "Unauthorized Access: You cannot perform this action.";
        }

        private static string ValidateStaff() => "Not implemented";

        private async Task<string> ValidateOwnerAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var botUserId = _agora.CurrentUser.Id.RawValue;

            var admin = await _userManager.IsAdministrator(currentUser);

            var isOwner = request switch
            {
                AcceptListingCommand command => currentUser.Equals(command.Showroom.Listings.FirstOrDefault()?.Owner) || 
                                                (admin.IsSuccessful && command.Showroom.Listings.FirstOrDefault()?.Owner.ReferenceNumber.Value == botUserId),
                _ => true
            };

            return isOwner ? string.Empty : "Unauthorized access: Only the OWNER can perform this action.";
        }

        public ValueTask<bool> SkipAuthorizationAsync(IEmporiumUser currentUser)
            => ValueTask.FromResult(IsAuthorized || (currentUser != null && currentUser.ReferenceNumber.Value == _agora.CurrentUser.Id.RawValue));
    }
}