using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord.Bot;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
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
                        default:
                            break;
                    }

                    if (authorizationError.IsNotNull())
                        throw new UnauthorizedAccessException(authorizationError);
                }
            }

            var authorizeAttributesWithPolicies = authorizeAttributes.Where(a => a.Policy != AuthorizationPolicy.None);

            if (authorizeAttributesWithPolicies.Any())
            {
                var authorizationError = string.Empty;

                foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
                {
                    switch (policy)
                    {
                        case AuthorizationPolicy.CanModify:
                            authorizationError = ValidateUpdate(currentUser, request);
                            break;
                        case AuthorizationPolicy.CanSubmitOffer:
                            authorizationError = await ValidateSubmissionAsync(currentUser, request);
                            break;
                        case AuthorizationPolicy.Manager:
                            authorizationError = await ValidateManager(currentUser, request);
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
            }

            return;
        }

        private static string ValidateUpdate<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var canModify = request switch
            {
                UpdateAuctionItemCommand command => command.Showroom.Listings.First().CurrentOffer == null,
                //TODO - when can a market item be modified
                _ => true
            };

            return canModify ? string.Empty : "Unauthorized Access: Listing can no longer be changed. Manager override is possible.";
        }

        private async Task<string> ValidateSubmissionAsync<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);

            var canSubmit = request switch
            {
                CreateBidCommand command => settings.AllowShillBidding || !currentUser.Equals(command.Showroom.Listings.First().Owner),
                _ => true
            };

            return canSubmit ? string.Empty : "Unauthorized access: You cannot submit an offer for an item you own.";
        }

        private async Task<string> ValidateManager<TRequest>(IEmporiumUser currentUser, TRequest request)
        {
            var isManager = request switch
            {
                WithdrawListingCommand command => currentUser.Equals(command.Showroom.Listings.First().Owner) || await _userManager.IsBroker(currentUser),
                _ => true
            };

            return isManager ? string.Empty : "Unauthorized access: Only the OWNER or users with the MANAGER role can perform this action.";
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
