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
using Emporia.Extensions.Discord;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Extension.TransactionFees.Application;

internal class TransactionFeeManager(DiscordBot bot, AuditLogService auditLog,
                                     ICommandContextAccessor commandContextAccessor,
                                     IInteractionContextAccessor interactionAccessor,
                                     IServiceScopeFactory scopeFactory) : INotificationHandler<ListingAddedEvent>,
                                                                          INotificationHandler<ListingSoldNotification>
{
    public Task Handle(ListingAddedEvent @event, CancellationToken cancellationToken)
        => HandleTransactionFees(@event.EmporiumId, @event.Listing, postPaid:false, cancellationToken);

    public Task Handle(ListingSoldNotification notification, CancellationToken cancellationToken)
        => HandleTransactionFees(notification.EmporiumId, notification.Listing, postPaid:true, cancellationToken);

    private async Task HandleTransactionFees(EmporiumId emporiumId, Listing listing, bool postPaid, CancellationToken cancellationToken)
    {
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

        var cache = services.GetRequiredService<IEmporiaCacheService>();
        var serverOwner = await cache.GetUserAsync(emporiumId.Value, bot.GetGuild(emporiumId.Value)!.OwnerId);

        if (feeSettings.ServerFee?.IsPercentage == isPercentage && serverOwner.ReferenceNumber != listing.Owner.ReferenceNumber.Value)
        {
            await ProcessFee(feeSettings.ServerFee, "Listing", listing, economy, serverOwner.ToEmporiumUser());
        }

        var authorId = commandContextAccessor.Context?.AuthorId ?? interactionAccessor.Context.Author.Id;

        if (feeSettings.BrokerFee?.IsPercentage == isPercentage && authorId.RawValue != listing.Owner.ReferenceNumber.Value)
        {
            var broker = await cache.GetUserAsync(emporiumId.Value, authorId);

            await ProcessFee(feeSettings.BrokerFee, "Broker", listing, economy, broker.ToEmporiumUser());
        }
    }

    private async Task ProcessFee(TransactionFee? fee, string feeType, Listing listing, IEconomy economy, EmporiumUser collector)
    {
        if (fee is { Value: > 0 })
        {
            var listingPrice = listing.Product.Value();
            var calculatedFee = Math.Round(fee.Calculate(listingPrice.Value), listingPrice.Currency.DecimalDigits);
            var feeAmount = Money.Create(calculatedFee, listingPrice.Currency);

            await TransferTransactionFee(listing, economy, feeAmount, feeType, collector);
        }
    }

    private async Task TransferTransactionFee(Listing listing, IEconomy economy, Money fee, string feeType, EmporiumUser collector)
    {
        var userBalance = await economy.GetBalanceAsync(listing.Owner, fee.Currency);

        if (userBalance.Data < fee)
            throw new ValidationException($"Insufficient Balance: Unable to cover the {feeType} Fee");

        await economy.DecreaseBalanceAsync(listing.Owner, fee, $"{feeType} Fee Deducted");

        await LogTransactionAsync(listing, $"{feeType} fee **{fee}** deducted from {Mention.User(listing.Owner.ReferenceNumber.Value)}");

        await economy.IncreaseBalanceAsync(collector, fee, $"{feeType} Fee Received");

        await LogTransactionAsync(listing, $"{feeType} fee **{fee}** paid to {Mention.User(collector.ReferenceNumber.Value)}");
    }

    private async Task LogTransactionAsync(Listing listing, string message)
    {
        var guildId = commandContextAccessor.Context?.GuildId ?? interactionAccessor.Context.GuildId;
        var channelId = commandContextAccessor.Context?.ChannelId ?? interactionAccessor.Context.ChannelId;

        var logChannel = await auditLog.GetFeedbackChannelAsync(guildId!.Value, channelId);
        var embed = new LocalEmbed().WithDescription(message).WithFooter($"{listing}").WithColor(Color.Goldenrod);

        if (logChannel == 0) return;

        await auditLog.TrySendMessageAsync(logChannel, new LocalMessage().AddEmbed(embed));
    }
}