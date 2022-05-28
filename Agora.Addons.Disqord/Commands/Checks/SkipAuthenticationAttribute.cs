using Disqord.Bot;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class SkipAuthenticationAttribute : DiscordGuildCheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(DiscordGuildCommandContext context)
        {
            context.Services.GetRequiredService<ICurrentUserService>().CurrentUser = EmporiumUser.Create(new EmporiumId(context.GuildId), ReferenceNumber.Create(context.Author.Id));
            return Success();
        }
    }
}
