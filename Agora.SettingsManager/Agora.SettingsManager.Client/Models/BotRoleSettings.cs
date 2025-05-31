namespace Agora.SettingsManager.Client.Models;

public class BotRoleSettings
{
    public ulong? ManagerRoleId { get; set; }
    public string ManagerRoleName { get; set; }
    public ulong? BrokerRoleId { get; set; }
    public string BrokerRoleName { get; set; }
    public ulong? MerchantRoleId { get; set; }
    public string MerchantRoleName { get; set; }
    public ulong? BuyerRoleId { get; set; }
    public string BuyerRoleName { get; set; }
}
