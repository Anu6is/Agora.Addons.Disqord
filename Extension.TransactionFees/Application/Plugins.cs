using Agora.Addons.Disqord.Common;
using Agora.Addons.Disqord.Extensions;
using Agora.Addons.Disqord.Interfaces;
using Agora.Shared.Extensions;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Services;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;

namespace Extension.TransactionFees.Application;

public class ListingBrokerManager(IDataAccessor dataAccessor) : IPluginExtension
{
    public async ValueTask<IResult> Execute(PluginParameters parameters)
    {
        var id = parameters.GetValue<EmporiumId>("EmporiumId");
        var settings = await dataAccessor.Transaction<GenericRepository<TransactionFeeSettings>>().GetByIdAsync(id);

        if (settings is null || settings.BrokerFee is null || settings.BrokerFee.Value == 0) return Result.Failure("Broker fees are not configured");

        var listingId = parameters.GetValue<ListingId>("ListingId");
        var brokerId = parameters.GetValue<ulong>("BrokerId");

        await dataAccessor.Transaction<GenericRepository<ListingBroker>>().AddAsync(ListingBroker.Create(listingId, brokerId));

        return Result.Success();
    }
}

public class EntryFeeValidator(IDataAccessor dataAccessor) : IPluginExtension
{
    public async ValueTask<IResult> Execute(PluginParameters parameters)
    {
        var id = parameters.GetValue<EmporiumId>("EmporiumId");
        var settings = await dataAccessor.Transaction<GenericRepository<TransactionFeeSettings>>().GetByIdAsync(id);

        if (settings is null || !settings.AllowEntryFee) return Result.Failure("Entry fees are currently disabled");

        return Result.Success();
    }
}

public class PremiumListingManager(DiscordBotBase bot, IDataAccessor dataAccessor) : IPluginExtension
{
    public async ValueTask<IResult> Execute(PluginParameters parameters)
    {
        var listing = parameters.GetValue<Listing>("Listing");
        var roleId = await CreateRoleAsync(listing.Owner.EmporiumId.Value, listing.ReferenceCode.Code());
        var premium = CreatePremiumListing(parameters, roleId);

        await UpdateListingAsync(listing, premium);

        listing.AddAccessRoles([.. listing.AccessRoles, roleId.ToString()]);

        await dataAccessor.Transaction<GenericRepository<PremiumListing>>().AddAsync(premium);
        await dataAccessor.Transaction<GenericRepository<Listing>>().UpdateAsync(listing);

        return Result.Success();
    }

    private async Task<ulong> CreateRoleAsync(ulong guildId, string name)
    {
        var role = await bot.CreateRoleAsync(guildId, x =>
        {
            x.Name = $"Auction {name}";
            x.Color = Color.DarkGoldenrod;
        });

        return role.Id;
    }

    private static PremiumListing CreatePremiumListing(PluginParameters parameters, ulong roleId)
    {
        var listing = parameters.GetValue<Listing>("Listing");
        var entryFee = parameters.GetValue<decimal>("EntryFee");

        var premium = PremiumListing.Create(listing.Id);

        premium.EntryRoleId = roleId;
        premium.EntryFee = TransactionFee.Create(entryFee);
        premium.RequiredEntries = parameters.GetValue<int>("RequiredEntries");

        return premium;
    }

    private async Task UpdateListingAsync(Listing listing, PremiumListing premiumListing)
    {
        var channelReference = listing.ReferenceCode.Reference();
        var channelId = channelReference == 0 ? listing.ShowroomId.Value : channelReference;
        var messageId = listing.Product.ReferenceNumber.Value;
        var msg = await bot.FetchMessageAsync(channelId, messageId) as IUserMessage;
        var components = msg!.Components.Select(LocalRowComponent.CreateFrom).ToList();
        var embeds = msg!.Embeds.Select(LocalEmbed.CreateFrom).ToList();
        var premiumEmbed = Display(premiumListing);
        var registerButton = new LocalButtonComponent()
            .WithCustomId($"#PayAuctionFee:{listing.Id.Value}")
            .WithLabel("Pay Entry Fee")
            .WithStyle(LocalButtonComponentStyle.Success);

        embeds.Insert(0, premiumEmbed);
        components.First().AddComponent(registerButton);

        await bot.ModifyMessageAsync(channelId, messageId, message =>
        {
            message.Embeds = embeds;
            message.Components = components;
        });
    }

    public static LocalEmbed Display(PremiumListing premium)
    {
        var embed = new LocalEmbed()
            .WithDefaultColor()
            .WithTitle($"Required Entry Fee: {premium.EntryFee.Value}")
            .AddField("Entry Role Granted", Mention.Role(premium.EntryRoleId));

        if (premium.RequiredEntries > 0)
            embed.WithFooter($"Required Entries: {Math.Min(premium.EntryList.Count, premium.RequiredEntries)}/{premium.RequiredEntries}");

        return embed;
    }
}
