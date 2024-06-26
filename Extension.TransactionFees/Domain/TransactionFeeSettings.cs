using Emporia.Domain.Common;

namespace Extension.TransactionFees.Domain;

public sealed class TransactionFeeSettings : Entity<EmporiumId>
{
    public TransactionFee? ServerFee { get; set; }
    public TransactionFee? BrokerFee { get; set; }
    public bool AllowEntryFee {  get; set; }

    private TransactionFeeSettings(EmporiumId id) : base(id) { }

    public static TransactionFeeSettings Create(ulong guildId)
    {
        return new TransactionFeeSettings(new EmporiumId(guildId));
    }
}
