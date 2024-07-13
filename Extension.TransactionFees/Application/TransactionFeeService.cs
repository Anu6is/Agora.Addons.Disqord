using Agora.Addons.Disqord;
using Agora.Addons.Disqord.Extensions;
using Agora.Shared.EconomyFactory;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Emporia.Application.Common;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using Emporia.Domain.Events;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Extension.TransactionFees.Application;

internal class TransactionFeeService(DiscordBot bot, AuditLogService auditLog,
                                     ICommandContextAccessor commandContextAccessor,
                                     IServiceScopeFactory scopeFactory) : INotificationHandler<ListingAddedEvent>,
                                                                          INotificationHandler<ListingRemovedEvent>
{
    public Task Handle(ListingAddedEvent @event, CancellationToken cancellationToken)
        => HandleTransactionFees(@event.EmporiumId, @event.Listing, postPaid: false, cancellationToken);

    public Task Handle(ListingRemovedEvent notification, CancellationToken cancellationToken)
        => HandleTransactionFees(notification.EmporiumId, notification.ProductListing, postPaid: true, cancellationToken);

    private async Task HandleTransactionFees(EmporiumId emporiumId, Listing listing, bool postPaid, CancellationToken cancellationToken)
    {
        if (listing is StandardGiveaway) return;
        if (listing.Product is TradeItem) return;
        if (listing.Status == ListingStatus.Expired || listing.Status == ListingStatus.Withdrawn) return;

        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var data = services.GetRequiredService<IDataAccessor>();
        var feeSettings = await data.Transaction<GenericRepository<TransactionFeeSettings>>().GetByIdAsync(emporiumId, cancellationToken);

        if (feeSettings is null) return;

        var isPercentage = postPaid;
        var settingsService = services.GetRequiredService<IGuildSettingsService>();
        var guildSettings = await settingsService.GetGuildSettingsAsync(emporiumId.Value);

        if (guildSettings.EconomyType.Equals(EconomyType.Disabled.ToString())) return;

        var economy = services.GetRequiredService<EconomyFactoryService>().Create(guildSettings.EconomyType);

        var result = await CheckUserBalanceAsync(economy, listing, feeSettings);

        if (!result.IsSuccessful) throw new ValidationException(result.FailureReason);

        var cache = services.GetRequiredService<IEmporiaCacheService>();
        var serverOwner = await cache.GetUserAsync(emporiumId.Value, bot.GetGuild(emporiumId.Value)!.OwnerId);

        if (feeSettings.ServerFee?.IsPercentage == isPercentage && serverOwner.ReferenceNumber != listing.Owner.ReferenceNumber.Value)
        {
            await ProcessFee(feeSettings.ServerFee, "Listing", listing, economy, serverOwner.ToEmporiumUser());
        }

        var userId = isPercentage ? await GetBrokerAsync(data, listing) : commandContextAccessor.Context?.AuthorId ?? listing.Owner.ReferenceNumber.Value;

        if (feeSettings.BrokerFee?.IsPercentage == isPercentage && userId.RawValue != listing.Owner.ReferenceNumber.Value)
        {
            var broker = await cache.GetUserAsync(emporiumId.Value, userId);

            await ProcessFee(feeSettings.BrokerFee, "Broker", listing, economy, broker.ToEmporiumUser());
        }
    }

    private static async Task<IResult> CheckUserBalanceAsync(IEconomy economy, Listing listing, TransactionFeeSettings settings)
    {
        if (settings.ServerFee is { IsPercentage: true } && settings.BrokerFee is { IsPercentage: true }) return Result.Success();

        var serverFee = settings.ServerFee?.IsPercentage is false ? settings.ServerFee.Value : 0;
        var brokerFee = settings.BrokerFee?.IsPercentage is false ? settings?.BrokerFee.Value : 0;
        var userBalance = await economy.GetBalanceAsync(listing.Owner, listing.Product.Value().Currency);

        if (userBalance.Data.Value < serverFee + brokerFee)
            return Result.Failure($"Insufficient Balance: Unable to cover the necessary fees");

        return Result.Success();
    }

    private static async Task<Snowflake> GetBrokerAsync(IDataAccessor data, Listing listing)
    {
        var listingBroker = await data.Transaction<GenericRepository<ListingBroker>>().GetByIdAsync(listing.Id);

        return listingBroker?.BrokerId ?? listing.Owner.ReferenceNumber.Value;
    }

    private async Task ProcessFee(TransactionFee? fee, string feeType, Listing listing, IEconomy economy, EmporiumUser collector)
    {
        if (fee is { Value: > 0 })
        {
            var listingPrice = listing.Product.Value();

            if (listingPrice is null) return;

            var calculatedFee = Math.Round(fee.Calculate(listingPrice.Value), listingPrice.Currency.DecimalDigits);
            var feeAmount = Money.Create(calculatedFee == 0 ? 1 : calculatedFee, listingPrice.Currency);

            await TransferTransactionFee(listing, economy, feeAmount, feeType, collector);
        }
    }

    private async Task TransferTransactionFee(Listing listing, IEconomy economy, Money fee, string feeType, EmporiumUser collector)
    {
        await economy.DecreaseBalanceAsync(listing.Owner, fee, $"{feeType} Fee Deducted");

        await LogTransactionAsync(listing, $"{feeType} fee **{fee}** deducted from {Mention.User(listing.Owner.ReferenceNumber.Value)}");

        await economy.IncreaseBalanceAsync(collector, fee, $"{feeType} Fee Received");

        await LogTransactionAsync(listing, $"{feeType} fee **{fee}** paid to {Mention.User(collector.ReferenceNumber.Value)}");
    }

    private async Task LogTransactionAsync(Listing listing, string message)
    {
        var channelId = listing.ShowroomId?.Value ?? commandContextAccessor.Context?.ChannelId.RawValue;

        var logChannel = await auditLog.GetFeedbackChannelAsync(listing.Owner.EmporiumId.Value, channelId!.Value);
        var embed = new LocalEmbed().WithDescription(message).WithFooter($"{listing}").WithColor(Color.Goldenrod);

        if (logChannel == 0) return;

        await auditLog.TrySendMessageAsync(logChannel, new LocalMessage().AddEmbed(embed));
    }
}