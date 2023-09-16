using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Application.Specifications;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using static Disqord.Discord.Limits.Message.Embed;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class CheckActiveListingsAttribute: DiscordGuildCheckAttribute
    {
        public override async ValueTask<IResult> CheckAsync(IDiscordGuildCommandContext context)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");
            if (settings.MaxListingsLimit == 0) return Results.Success;

            var userReference = ReferenceNumber.Create(context.AuthorId);
            var userManager = context.Services.GetRequiredService<IUserManager>();
            var result = await userManager.IsBroker(EmporiumUser.Create(new EmporiumId(context.GuildId), userReference));

            if (result.IsSuccessful) return Results.Success;

            var data = context.Services.GetRequiredService<IDataAccessor>();
            var user = await context.Services.GetRequiredService<ICurrentUserService>().GetCurrentUserAsync();

            var listings = await data.Transaction<IReadRepository<Listing>>().ListAsync(new EntitySpec<Listing>(x => x.Owner.Equals(user), includes: new[] { "Owner" }));

            if (listings.Count < settings.MaxListingsLimit) return Results.Success;

            return Results.Failure($"You are currently restricted to a maximum of {settings.MaxListingsLimit} active listings");
        }
    }
}
