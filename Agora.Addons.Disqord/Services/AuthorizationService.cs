using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Disqord.Bot;
using Emporia.Application.Common;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
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
            var settings = await _guildSettingsService.GetGuildSettingsAsync(currentUser.EmporiumId.Value);
            var authorizeAttributesWithRoles = authorizeAttributes.Where(a => a.Role != AuthorizationRole.None);

            if (authorizeAttributesWithRoles.Any())
            {
                var authorized = false;

                foreach (var role in authorizeAttributesWithRoles.Select(a => a.Role))
                {
                    switch (role)
                    {
                        case AuthorizationRole.Administrator:
                            authorized = await _userManager.IsAdministrator(currentUser);
                            break;
                        case AuthorizationRole.Broker:
                            authorized = await _userManager.IsBroker(currentUser);
                            break;
                        case AuthorizationRole.Host:
                            authorized = await _userManager.IsHost(currentUser);
                            break;
                        default:
                            break;
                    }

                    if (!authorized)
                        throw new UnauthorizedAccessException();
                }
            }
            
            var authorizeAttributesWithPolicies = authorizeAttributes.Where(a => a.Policy != AuthorizationPolicy.None);

            if (authorizeAttributesWithPolicies.Any())
            {
                var authorized = false;

                foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
                {
                    switch (policy)
                    {
                        case AuthorizationPolicy.CanModify:
                            authorized = request switch
                            {
                                UpdateAuctionItemCommand command => command.Showroom.Listings.First().CurrentOffer == null,// || await _userManager.IsAdministrator(currentUser),
                                _ => true
                            };
                            break;
                        case AuthorizationPolicy.CanOffer:
                            authorized = request switch
                            {
                                CreateBidCommand command => settings.AllowShillBidding || !currentUser.Equals(command.Showroom.Listings.First().Owner),
                                _ => true
                            };
                            break;
                        case AuthorizationPolicy.Manager:
                            break;
                        case AuthorizationPolicy.StaffOnly:
                            break;
                        case AuthorizationPolicy.OwnerOnly:
                            break;
                        default:
                            break;
                    }

                    if (!authorized)
                        throw new UnauthorizedAccessException();
                }
            }

            return;
        }
        
        public ValueTask<bool> SkipAuthorizationAsync(IEmporiumUser currentUser)
        {
            return ValueTask.FromResult(currentUser != null && currentUser.ReferenceNumber.Value == _agora.CurrentUser.Id.RawValue);
        }
    }
}
