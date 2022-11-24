using Disqord.Bot.Commands;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Checks
{
    internal class RequireUnregisteredServerAttribute : DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var emporium = await context.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(context.GuildId);

            if (emporium == null) return Results.Success;

            return Results.Failure("Setup Previously Completed: Execute the command </server settings:1013361602499723275> to edit or </server reset:1013361602499723275> to delete.");
        }
    }
}
