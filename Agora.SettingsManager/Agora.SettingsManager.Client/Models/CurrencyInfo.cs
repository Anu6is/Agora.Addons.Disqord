namespace Agora.SettingsManager.Client.Models;

public class CurrencyInfo
{
    public string Id { get; set; }
    public string Symbol { get; set; } = "$";
    public int DecimalPlaces { get; set; } = 2;
    public string Name { get; set; }
}
