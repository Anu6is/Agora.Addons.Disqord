using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireRoleAttribute : DiscordParameterCheckAttribute
    {
        private readonly AuthorizationRole _role;

        public RequireRoleAttribute(AuthorizationRole role) => _role = role;

        public override bool CanCheck(IParameter parameter, object value) => value is IMember;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object member)
        {
            var user = EmporiumUser.Create(new EmporiumId(context.GuildId.Value), ReferenceNumber.Create((member as IMember).Id));
            var broker = await context.Services.CreateScope().ServiceProvider.GetRequiredService<IUserManager>().IsBroker(user);

            if (broker)
                return Results.Success;
            else
                return Results.Failure($"The {_role} role is required to set the {parameter.Name}");
        }
    }
}
