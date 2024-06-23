using Disqord;
using Disqord.Bot.Commands;
using Emporia.Application.Common;
using Emporia.Application.Specifications;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    public class CheckListingLimitAttribute : DiscordParameterCheckAttribute
    {
        public override bool CanCheck(IParameter parameter, object value) => value is IMember or null;

        public override async ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object memberObj)
        {
            var settings = await context.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(context.GuildId.Value);

            if (settings == null) return Results.Failure("Setup Required: Please execute the </server setup:1013361602499723275> command.");
            if (settings.MaxListingsLimit == 0) return Results.Success;

            EmporiumUser user;

            if (memberObj is IMember { } member)
            {
                var cachedUser = await context.Services.GetRequiredService<IEmporiaCacheService>().GetUserAsync(context.GuildId.Value, member.Id);

                user = cachedUser.ToEmporiumUser();
            }
            else
            {
                user = await context.Services.GetRequiredService<ICurrentUserService>().GetCurrentUserAsync();
                
                var result = await context.Services.GetRequiredService<IUserManager>().IsBroker(user);

                if (!result.IsSuccessful) return Results.Success;
            }

            var data = context.Services.GetRequiredService<IDataAccessor>();
            var listings = await data.Transaction<IReadRepository<Listing>>().ListAsync(new EntitySpec<Listing>(x => x.Owner.Equals(user), includes: new[] { "Owner" }));

            if (listings.Count < settings.MaxListingsLimit) return Results.Success;

            return Results.Failure($"{Mention.User(user.ReferenceNumber.Value)} is restricted to a maximum of {settings.MaxListingsLimit} active listings");
        }
    }
}
