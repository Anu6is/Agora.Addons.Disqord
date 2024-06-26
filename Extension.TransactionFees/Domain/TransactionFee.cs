using Emporia.Domain.Common;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Extension.TransactionFees.Domain;

public sealed class TransactionFee : ValueObject
{
    public decimal Value { get; set; }
    public bool IsPercentage { get; set; }

    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public TransactionFee(decimal value, bool isPercentage)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be less than 0");

        Value = value;
        IsPercentage = isPercentage;
    }

    public static TransactionFee Create(decimal value, bool isPercentage = false)
    {
        return new TransactionFee(value, isPercentage);
    }

    public decimal Calculate(decimal transactionPrice)
    {
        if (IsPercentage) return transactionPrice * Value / 100;

        return Value;
    }

    public override string ToString()
    {
        if (Value == 0) return "None";

        return $"{Value}{(IsPercentage ? "%" : string.Empty)}";
    }
}
