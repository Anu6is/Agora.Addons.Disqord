using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    public class RequireMerchantAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var userManager = context.Services.GetRequiredService<IUserManager>();

            if (await userManager.IsHost(EmporiumUser.Create(new EmporiumId(context.GuildId), ReferenceNumber.Create(context.AuthorId)))) return Results.Success;

            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            return Results.Failure($"Only users with the {Mention.Role(settings.MerchantRole)} role can execute this command.");
        }
    }
}
