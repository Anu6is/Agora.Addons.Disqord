using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireRoleAttribute : DiscordParameterCheckAttribute
    {
        private readonly AuthorizationRole _role;
        private readonly bool _author;

        public RequireRoleAttribute(AuthorizationRole role, bool author = true)
        {
            _role = role;
            _author = author;
        }

        public override bool CanCheck(IParameter parameter, object value) => value is IMember;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object member)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);
            
            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            var user = EmporiumUser.Create(new EmporiumId(context.GuildId.Value), ReferenceNumber.Create(_author ? context.AuthorId : (member as IMember).Id));
            var hasAccess = false;

            switch (_role)
            {
                case AuthorizationRole.Administrator:
                    hasAccess = await context.Services.GetRequiredService<IUserManager>().IsAdministrator(user);
                    break;
                case AuthorizationRole.Broker:
                    hasAccess = await context.Services.GetRequiredService<IUserManager>().IsBroker(user);
                    break;
                case AuthorizationRole.Host:
                    hasAccess = await context.Services.GetRequiredService<IUserManager>().IsHost(user);
                    break;
                case AuthorizationRole.Buyer:
                    hasAccess = await context.Services.GetRequiredService<IUserManager>().ValidateBuyerAsync(user);
                    break;
                default:
                    break;
            }

            if (hasAccess)
                return Results.Success;
            else
                return _author ? Results.Failure($"The {_role} role is required to set the {parameter.Name}") : Results.Failure($"The selected member requires the {_role} role");
        }
    }
}
