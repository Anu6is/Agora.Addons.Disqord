namespace Agora.SettingsManager.Client.Models;

public enum ListingType
{
    Auction = 0,
    Market = 2,
    Trade = 3,
    Giveaway = 4
}

public class Showroom
{
    public string Id { get; set; }
    public ulong ChannelId { get; set; }
    public string ChannelName { get; set; }
    public ListingType Type { get; set; }
    public ulong? CategoryId { get; set; }
    public string CategoryName {get; set; }
}
