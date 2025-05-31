using Agora.SettingsManager.Client.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agora.SettingsManager.Client.Services;

public interface ISettingsApiService
{
    Task<GuildSettings?> GetGuildSettingsAsync(ulong guildId);
    Task<bool> UpdateGuildSettingsAsync(ulong guildId, GuildSettings settings);

    Task<List<Showroom>?> GetShowroomsAsync(ulong guildId);
    Task<bool> AddShowroomAsync(ulong guildId, Showroom showroom);
    Task<bool> RemoveShowroomAsync(ulong guildId, string showroomId);

    Task<List<GuildCategory>?> GetCategoriesAsync(ulong guildId);
    Task<bool> AddCategoryAsync(ulong guildId, GuildCategory category);
    Task<bool> RemoveCategoryAsync(ulong guildId, string categoryId);

    Task<List<CurrencyInfo>?> GetCurrenciesAsync(ulong guildId);
    Task<bool> AddCurrencyAsync(ulong guildId, CurrencyInfo currency);
    Task<bool> RemoveCurrencyAsync(ulong guildId, string currencyId);

    Task<ListingRequirements?> GetListingRequirementsAsync(ulong guildId, ListingType type);
    Task<bool> UpdateListingRequirementsAsync(ulong guildId, ListingRequirements requirements);

    Task<BotRoleSettings?> GetBotRoleSettingsAsync(ulong guildId);
    Task<bool> UpdateBotRoleSettingsAsync(ulong guildId, BotRoleSettings roles);

    Task<bool> ResetGuildSettingsAsync(ulong guildId);

    Task<List<DiscordChannelInfo>?> GetDiscordChannelsAsync(ulong guildId);
    Task<List<DiscordRoleInfo>?> GetDiscordRolesAsync(ulong guildId);
}

public class DiscordChannelInfo
{
    public ulong Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}

public class DiscordRoleInfo
{
    public ulong Id { get; set; }
    public string Name { get; set; }
}
