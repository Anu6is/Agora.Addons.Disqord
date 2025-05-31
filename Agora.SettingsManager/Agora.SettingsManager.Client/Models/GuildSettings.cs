namespace Agora.SettingsManager.Client.Models;

public class GuildSettings
{
    public ulong GuildId { get; set; }
    public ulong? ResultLogChannelId { get; set; }
    public ulong? AuditLogChannelId { get; set; }
    public string DefaultCurrencySymbol { get; set; } = "$";
    public int CurrencyDecimalPlaces { get; set; } = 2;
    public string? ServerTimezone { get; set; }
    public TimeSpan? ServerTimeOffset { get; set; }
}
