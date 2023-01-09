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
    internal class RequireManagerAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");

            var userManager = context.Services.GetRequiredService<IUserManager>();

            if (await userManager.IsAdministrator(EmporiumUser.Create(new EmporiumId(context.GuildId), ReferenceNumber.Create(context.AuthorId)))) return Results.Success;

            return Results.Failure($"Only users with the {Mention.Role(settings.AdminRole)} role can execute this command.");
        }
    }
}
