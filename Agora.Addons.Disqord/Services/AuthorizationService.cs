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

        public async ValueTask AuthroizeAsync<TRequest>(TRequest request, IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributes)
        {
            var authorizeAttributesWithRoles = authorizeAttributes.Where(a => a.Role != AuthorizationRole.None);

            if (authorizeAttributesWithRoles.Any())
                await ValidateRolesAsync(currentUser, authorizeAttributesWithRoles);

            var authorizeAttributesWithPolicies = authorizeAttributes.Where(a => a.Policy != AuthorizationPolicy.None);

            if (authorizeAttributesWithPolicies.Any())
                await ValaidatePoliciesAsync(request, currentUser, authorizeAttributesWithPolicies);

            return;
        }

        private async Task ValidateRolesAsync(IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributesWithRoles)
        {
            var authorizationError = string.Empty;

            foreach (var role in authorizeAttributesWithRoles.Select(a => a.Role))
            {
                switch (role)
                {
                    case AuthorizationRole.Administrator:
                        if (!await _userManager.IsAdministrator(currentUser)) authorizationError = "Unauthorized access: Manager role required.";
                        break;
                    case AuthorizationRole.Broker:
                        if (!await _userManager.IsBroker(currentUser)) authorizationError = "Unauthorized access: Broker role required.";
                        break;
                    case AuthorizationRole.Host:
                        if (!await _userManager.IsHost(currentUser)) authorizationError = "Unauthorized access: Merchant role required.";
                        break;
                    case AuthorizationRole.Buyer:
                        if (!await _userManager.ValidateBuyerAsync(currentUser)) authorizationError = "Unathorized access: Buyer role required.";
                        break;
                    default:
                        break;
                }

                if (authorizationError.IsNotNull())
                    throw new UnauthorizedAccessException(authorizationError);
            }

            return;
        }

        private async Task ValaidatePoliciesAsync<TRequest>(TRequest request, IEmporiumUser currentUser, IEnumerable<AuthorizeAttribute> authorizeAttributesWithPolicies)
        {
            var authorizationError = string.Empty;

            foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
            {
                switch (policy)
                {
                    case AuthorizationPolicy.CanModify:
                        authorizationError = await ValidateUpdateAsync(currentUser, request);
                        break;
                    case AuthorizationPolicy.CanSubmitOffer:
                        authorizationError = await ValidateSubmissionAsync(currentUser, request);
                        break;
                    case AuthorizationPolicy.Manager:
                        authorizationError = await ValidateManagerAsync(currentUser, request);
                        break;
                    case AuthorizationPolicy.StaffOnly:
                        authorizationError = ValidateStaff();
                        break;
                    case AuthorizationPolicy.OwnerOnly:
                        authorizationError = ValidateOwner(currentUser, request);
                        break;
                    default:
                        break;
                }

                if (authorizationError.IsNotNull())
                    throw new UnauthorizedAccessException(authorizationError);
            }

            return;
        }

        private async Task<string> ValidateUpdateAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);
            var managerRole = settings.AdminRole == 0 ? Markdown.Bold("Manager Privileges") : Mention.Role(settings.AdminRole);

            switch (request)
            {
                case AcceptListingCommand command:
                    if (!settings.AllowAcceptingOffer) return "Invalid Operation: This action has been disabled.";

                    var canAcceptFrom = command.Showroom.Listings.First().ScheduledPeriod.ScheduledStart.Add(settings.MinimumDuration).ToUniversalTime();
                    var duration = settings.MinimumDuration.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);
                    var remaining = canAcceptFrom.Subtract(SystemClock.Now).Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);

                    if (canAcceptFrom > SystemClock.Now)
                        return $"Invalid Operation: Item has to be listed for at least {duration}. {remaining} remaining.";
                    break;
                case WithdrawListingCommand command:
                    if (await _userManager.IsAdministrator(currentUser)) return string.Empty;
                    if (!settings.AllowListingRecall && command.Showroom.Listings.First().CurrentOffer != null) return "Invalid Operation: Listing cannot be withdrawn once an offer has been submitted.";
                    break;
                case UndoBidCommand command:
                    if (await _userManager.IsAdministrator(currentUser)) return string.Empty;

                    var listing = command.Showroom.Listings.First();
                    var currentOffer = listing.CurrentOffer;

                    if (currentOffer == null) 
                        return "Invalid Operation: No available bids exist.";

                    if (settings.BiddingRecallLimit == TimeSpan.Zero)
                        return $"Unauthorized Action: Only users with {managerRole} can undo bids";

                    var limit = settings.BiddingRecallLimit.Humanize(2, maxUnit: TimeUnit.Day, minUnit: TimeUnit.Second);

                    if (currentOffer.SubmittedOn.ToUniversalTime().Add(settings.BiddingRecallLimit) < SystemClock.Now)
                        return $"Invalid Operation: This action is no longer available. Bids can only be withdrawn up to {limit} after submission.";
                    break;
                case IProductListingBinder command:
                    if (await _userManager.IsAdministrator(currentUser)) return string.Empty;
                    if (command.Showroom.Listings.First().CurrentOffer != null) return "Invalid Operation: Updates cannot be made once an offer has been submitted.";
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
                case CreateBidCommand command:
                    var listing = command.Showroom.Listings.FirstOrDefault();

                    if (listing == null) return "Invalid Action: Listing is no longer available";

                    if (!settings.AllowShillBidding && currentUser.Equals(listing.Owner)) 
                        return "Transaction Denied: You cannot bid on an item you listed. Enable **Shill Bidding** in </server settings:1013361602499723275>.";

                    var validBid = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == EconomyType.Disabled.ToString()) return true;

                        var cmd = command as CreateBidCommand;
                        var item = listing.Product as AuctionItem;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, item.StartingPrice.Currency);

                        if (cmd.UseMinimum)
                            return userBalance >= item.CurrentPrice.Value + item.BidIncrement.MinValue;

                        if (cmd.UseMaximum)
                            return userBalance >= item.CurrentPrice.Value + item.BidIncrement.MaxValue.Value;

                        return userBalance >= cmd.Amount;
                    });

                    if (!validBid) return "Transaction Denied: Insufficient balance available to complete this transaction.";
                    break;

                case CreatePaymentCommand command:
                    var sale = command.Showroom.Listings.FirstOrDefault();

                    if (sale == null) return "Invalid Action: Listing is no longer available";

                    if (currentUser.Equals(sale.Owner))
                        return "Transaction Denied: you cannot purchase an item you listed.";

                    var validPurchase = await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                    {
                        if (settings.EconomyType == EconomyType.Disabled.ToString()) return true;

                        var cmd = command as CreatePaymentCommand;
                        var item = cmd.Showroom.Listings.First().Product as MarketItem;
                        var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                        var userBalance = await economy.GetBalanceAsync(currentUser, item.Price.Currency);

                        return userBalance >= cmd.PaymentAmount;
                    });

                    if (!validPurchase) return "Transaction Denied: Insufficient balance available to complete this transaction.";
                    break;
                case CreateDealCommand command:
                    var trade = command.Showroom.Listings.First();

                    if (currentUser.Equals(trade.Owner)) 
                        return "Transaction Denied: you cannot trade with yourself.";

                    if (trade.Product is TradeItem item && item.Offers.Any(x => x.UserId == command.CurrentUser.Id))
                        return "Invalid Action: You already made an offer on this item.";
                    break;
                default:
                    return string.Empty;
            }

            return string.Empty;
        }

        private async Task<string> ValidateManagerAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var isManager = request switch
            {
                WithdrawListingCommand command => await _userManager.IsAdministrator(currentUser) 
                                               || currentUser.Equals(command.Showroom.Listings.First().Owner),
                UndoBidCommand command => await _userManager.IsAdministrator(currentUser)
                                       || command.Showroom.Listings.First() is VickreyAuction 
                                       || currentUser.Equals(command.Showroom.Listings.First().Owner) 
                                       || command.Bidder.Id.Equals(command.Showroom.Listings.First().CurrentOffer.UserId),
                IProductListingBinder binder => await _userManager.IsBroker(currentUser) 
                                             || currentUser.Equals(binder.Showroom.Listings.First().Owner), 
                _ => false
            };

            return isManager ? string.Empty : "Unauthorized Access: You cannot perform this action.";
        }

        private static string ValidateStaff()
        {
            return "Not implemented";
        }

        private static string ValidateOwner<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var isOwner = request switch
            {
                AcceptListingCommand command => currentUser.Equals(command.Showroom.Listings.FirstOrDefault()?.Owner),
                _ => true
            };

            return isOwner ? string.Empty : "Unauthorized access: Only the OWNER can perform this action.";
        }

        public ValueTask<bool> SkipAuthorizationAsync(IEmporiumUser currentUser)
            => ValueTask.FromResult(IsAuthorized || (currentUser != null && currentUser.ReferenceNumber.Value == _agora.CurrentUser.Id.RawValue));
    }
}
