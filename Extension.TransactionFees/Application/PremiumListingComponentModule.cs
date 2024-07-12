using Agora.Addons.Disqord.Extensions;
using Agora.Shared.EconomyFactory;
using Disqord;
using Disqord.Bot.Commands.Components;
using Disqord.Gateway;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Application.Specifications;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Extensions.Discord;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using Qmmands;

namespace Extension.TransactionFees.Application
{
    public sealed class PremiumListingComponentModule : DiscordComponentModuleBase
    {
        private readonly IDataAccessor _dataAccessor;
        private readonly IEmporiaCacheService _cache;
        private readonly IGuildSettingsService _settingsService;

        private readonly EconomyFactoryService _factory;

        public PremiumListingComponentModule(EconomyFactoryService economyFactory, IDataAccessor dataAccessor, IGuildSettingsService settingsService, IEmporiaCacheService cache)
        {
            _cache = cache;
            _factory = economyFactory;
            _dataAccessor = dataAccessor;
            _settingsService = settingsService;
        }

        [ButtonCommand("#PayAuctionFee:*")]

        public async Task<IResult> RegisterForAuction(Guid listingId)
        {
            await Deferral(isEphemeral: true);

            var id = new ListingId(listingId);
            var listing = await _dataAccessor.Transaction<IReadRepository<Listing>>().FirstOrDefaultAsync(new EntitySpec<Listing>(x => x.Id == id, includes: ["Owner", "Product"]));

            var responseMessage = new LocalInteractionMessageResponse().WithIsEphemeral();

            if (listing is null) return Response(responseMessage.WithContent("Unable to process registration"));

            var premiumListing = await _dataAccessor.Transaction<GenericRepository<PremiumListing>>().GetByIdAsync(id);

            if (premiumListing is null) return Response(responseMessage.WithContent("Unable to process registration"));

            var member = Context.Author as IMember;
            var memberRoles = member!.RoleIds.ToList();

            if (memberRoles.Contains(premiumListing.EntryRoleId)) return Response(responseMessage.WithContent("You've already registered for this Auction"));

            var isAdmin = member.CalculateGuildPermissions().HasFlag(Permissions.ManageGuild);
            var restrictedRoles = listing.AccessRoles.Except([premiumListing.EntryRoleId.ToString()]);

            if (!isAdmin && restrictedRoles.Any() && !memberRoles.Select(r => r.ToString()).Intersect(restrictedRoles).Any())
            {
                var roleMentions = restrictedRoles.Select(x => Mention.Role(Snowflake.Parse(x)));
                return Response(responseMessage.WithContent($"Entry restricted to {string.Join(" / ", roleMentions)}"));
            }

            var guildSettings = await _settingsService.GetGuildSettingsAsync(Context.GuildId!.Value);
            var user = await _cache.GetUserAsync(Context.GuildId!.Value, Context.AuthorId);

            if (guildSettings.EconomyType != EconomyType.Disabled.ToString() && member.Id != listing.Owner.ReferenceNumber.Value)
            {
                var fee = Money.Create(premiumListing.EntryFee.Value, listing.Product.Value().Currency);

                var economy = _factory.Create(guildSettings.EconomyType);

                await economy.DecreaseBalanceAsync(user.ToEmporiumUser(), fee, "Auction Registration Fee Deducted");
                await economy.IncreaseBalanceAsync(listing.Owner, fee, "Auction Registration Fee Received");
            }

            memberRoles.Add(premiumListing.EntryRoleId);

            await member.ModifyAsync(x => x.RoleIds = memberRoles);

            premiumListing.EntryList.Add(Context.AuthorId.RawValue);

            await _dataAccessor.Transaction<GenericRepository<PremiumListing>>().UpdateAsync(premiumListing);

            var info = PremiumListingManager.Display(premiumListing);
            var message = (Context.Interaction as IComponentInteraction)!.Message;
            var buttons = message.Components[0].Components.Select(LocalComponent.CreateFrom);
            var response = new LocalInteractionMessageResponse()
                                .WithContent(message.Content)
                                .WithEmbeds(info, LocalEmbed.CreateFrom(message.Embeds[1]))
                                .WithComponents(new LocalRowComponent().WithComponents(buttons));

            await Context.Interaction.ModifyMessageAsync(response);

            return Response(responseMessage.WithContent("Auction registration successful"));
        }
    }
}
