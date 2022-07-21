using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Agora.Shared.Services.EconomyFactory;
using Disqord.Bot;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agora.Addons.Disqord
{
    [AgoraService(AgoraServiceAttribute.ServiceLifetime.Scoped)]
    public class AuthorizationService : AgoraService, IAuthorizationService
    {
        private readonly DiscordBotBase _agora;
        private readonly IUserManager _userManager;
        private readonly IGuildSettingsService _guildSettingsService;

        public AuthorizationService(DiscordBotBase bot, IGuildSettingsService settingsService, IUserManager userManager, ILogger<AuthorizationService> logger) : base(logger)
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
                        if (!await _userManager.IsAdministrator(currentUser)) authorizationError = "Unauthorized access: Manager role reqired.";
                        break;
                    case AuthorizationRole.Broker:
                        if (!await _userManager.IsBroker(currentUser)) authorizationError = "Unauthorized access: Broker role reqired.";
                        break;
                    case AuthorizationRole.Host:
                        if (!await _userManager.IsHost(currentUser)) authorizationError = "Unauthorized access: Merchant role reqired.";
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
            var canModify = request switch
            {
                UpdateAuctionItemCommand command => command.Showroom.Listings.First().CurrentOffer == null || await _userManager.IsAdministrator(currentUser),
                UpdateMarketItemCommand command => command.Showroom.Listings.First().CurrentOffer == null || await _userManager.IsAdministrator(currentUser),
                UndoBidCommand command => command.Showroom.Listings.First().CurrentOffer != null && command.Showroom.Listings.First().CurrentOffer.SubmittedOn.ToUniversalTime().AddSeconds(30) <= SystemClock.Now,
                _ => true
            };

            return canModify ? string.Empty : "Invalid operation: This action is no longer available.";
        }

        private async Task<string> ValidateSubmissionAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);

            var canSubmit = request switch
            {
                CreateBidCommand command => (settings.AllowShillBidding
                || !currentUser.Equals(command.Showroom.Listings.First().Owner))
                && await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                {
                    var cmd = command as CreateBidCommand;
                    var item = cmd.Showroom.Listings.First().Product as AuctionItem;
                    var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                    var userBalance = await economy.GetBalanceAsync(currentUser, item.StartingPrice.Currency);

                    if (cmd.UseMinimum)
                        return userBalance >= item.CurrentPrice.Value + item.BidIncrement.MinValue;

                    if (cmd.UseMaximum)
                        return userBalance >= item.CurrentPrice.Value + item.BidIncrement.MaxValue.Value;

                    return userBalance >= cmd.Amount;
                }),
                CreatePaymentCommand command => !currentUser.Equals(command.Showroom.Listings.First().Owner)
                && await _userManager.ValidateBuyerAsync(currentUser, command, async (currentUser, command) =>
                {
                    var cmd = command as CreatePaymentCommand;
                    var item = cmd.Showroom.Listings.First().Product as MarketItem;
                    var economy = _agora.Services.GetRequiredService<EconomyFactoryService>().Create(settings.EconomyType);
                    var userBalance = await economy.GetBalanceAsync(currentUser, item.Price.Currency);

                    return userBalance >= cmd.PaymentAmount;
                }),
                _ => true
            };

            return canSubmit ? string.Empty : "Transaction Denied: Unable to complete this action.";
        }

        private async Task<string> ValidateManagerAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var isManager = request switch
            {
                WithdrawListingCommand command => currentUser.Equals(command.Showroom.Listings.First().Owner) || await _userManager.IsBroker(currentUser),
                ExtendListingCommand command => currentUser.Equals(command.Showroom.Listings.First().Owner) || await _userManager.IsBroker(currentUser),
                UndoBidCommand command => await _userManager.IsHost(currentUser),
                _ => true
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
                AcceptListingCommand command => currentUser.Equals(command.Showroom.Listings.First().Owner),
                _ => true
            };

            return isOwner ? string.Empty : "Unauthorized access: Only the OWNER can perform this action.";
        }

        public ValueTask<bool> SkipAuthorizationAsync(IEmporiumUser currentUser)
            => ValueTask.FromResult(currentUser != null && currentUser.ReferenceNumber.Value == _agora.CurrentUser.Id.RawValue);
    }
}
