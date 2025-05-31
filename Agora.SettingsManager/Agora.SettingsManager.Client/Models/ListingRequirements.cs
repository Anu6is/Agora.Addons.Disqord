namespace Agora.SettingsManager.Client.Models;

public class ListingRequirements
{
    public ListingType Type { get; set; }
    public bool RequireImage { get; set; }
    public bool RequireDescription { get; set; }
    public bool RequirePrice { get; set; }
}
