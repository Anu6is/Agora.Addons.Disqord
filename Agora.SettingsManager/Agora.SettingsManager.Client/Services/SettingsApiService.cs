using Agora.SettingsManager.Client.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Agora.SettingsManager.Client.Services;

public class SettingsApiService : ISettingsApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SettingsApiService> _logger;

    public SettingsApiService(HttpClient httpClient, ILogger<SettingsApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private Task<T?> MockGetData<T>(string logMessage) where T : class, new()
    {
        _logger.LogInformation($"Mocking {logMessage}. Returning default {typeof(T).Name}.");
        return Task.FromResult<T?>(new T());
    }
     private Task<List<T>?> MockGetListData<T>(string logMessage) where T : class, new()
    {
        _logger.LogInformation($"Mocking {logMessage}. Returning empty list of {typeof(T).Name}.");
        return Task.FromResult<List<T>?>(new List<T>());
    }
    private Task<bool> MockSetData(string logMessage)
    {
        _logger.LogInformation($"Mocking {logMessage}. Returning true.");
        return Task.FromResult(true);
    }

    public async Task<GuildSettings?> GetGuildSettingsAsync(ulong guildId)
    {
        _logger.LogInformation($"Attempting to fetch guild settings for {guildId}");
        return await MockGetData<GuildSettings>($"GetGuildSettingsAsync for guild {guildId}");
    }

    public async Task<bool> UpdateGuildSettingsAsync(ulong guildId, GuildSettings settings)
    {
        _logger.LogInformation($"Attempting to update guild settings for {guildId}");
        return await MockSetData($"UpdateGuildSettingsAsync for guild {guildId}");
    }

    public async Task<List<Showroom>?> GetShowroomsAsync(ulong guildId)
    {
        return await MockGetListData<Showroom>($"GetShowroomsAsync for guild {guildId}");
    }

    public async Task<bool> AddShowroomAsync(ulong guildId, Showroom showroom)
    {
        return await MockSetData($"AddShowroomAsync for guild {guildId}");
    }

    public async Task<bool> RemoveShowroomAsync(ulong guildId, string showroomId)
    {
        return await MockSetData($"RemoveShowroomAsync for showroom {showroomId} in guild {guildId}");
    }

    public async Task<List<GuildCategory>?> GetCategoriesAsync(ulong guildId)
    {
        return await MockGetListData<GuildCategory>($"GetCategoriesAsync for guild {guildId}");
    }

    public async Task<bool> AddCategoryAsync(ulong guildId, GuildCategory category)
    {
        return await MockSetData($"AddCategoryAsync for guild {guildId}");
    }

    public async Task<bool> RemoveCategoryAsync(ulong guildId, string categoryId)
    {
        return await MockSetData($"RemoveCategoryAsync for category {categoryId} in guild {guildId}");
    }

    public async Task<List<CurrencyInfo>?> GetCurrenciesAsync(ulong guildId)
    {
        return await MockGetListData<CurrencyInfo>($"GetCurrenciesAsync for guild {guildId}");
    }

    public async Task<bool> AddCurrencyAsync(ulong guildId, CurrencyInfo currency)
    {
        return await MockSetData($"AddCurrencyAsync for guild {guildId}");
    }

    public async Task<bool> RemoveCurrencyAsync(ulong guildId, string currencyId)
    {
        return await MockSetData($"RemoveCurrencyAsync for currency {currencyId} in guild {guildId}");
    }

    public async Task<ListingRequirements?> GetListingRequirementsAsync(ulong guildId, ListingType type)
    {
         var reqs = await MockGetData<ListingRequirements>($"GetListingRequirementsAsync for type {type} in guild {guildId}");
         if (reqs != null) reqs.Type = type;
         return reqs;
    }

    public async Task<bool> UpdateListingRequirementsAsync(ulong guildId, ListingRequirements requirements)
    {
        return await MockSetData($"UpdateListingRequirementsAsync for guild {guildId}");
    }

    public async Task<BotRoleSettings?> GetBotRoleSettingsAsync(ulong guildId)
    {
        return await MockGetData<BotRoleSettings>($"GetBotRoleSettingsAsync for guild {guildId}");
    }

    public async Task<bool> UpdateBotRoleSettingsAsync(ulong guildId, BotRoleSettings roles)
    {
        return await MockSetData($"UpdateBotRoleSettingsAsync for guild {guildId}");
    }

    public async Task<bool> ResetGuildSettingsAsync(ulong guildId)
    {
        return await MockSetData($"ResetGuildSettingsAsync for guild {guildId}");
    }

    public Task<List<DiscordChannelInfo>?> GetDiscordChannelsAsync(ulong guildId)
    {
        _logger.LogInformation($"Mocking GetDiscordChannelsAsync for guild {guildId}. Returning sample channels.");
        var channels = new List<DiscordChannelInfo>
        {
            new DiscordChannelInfo { Id = 123456789012345678, Name = "general", Type = "Text" },
            new DiscordChannelInfo { Id = 123456789012345679, Name = "announcements", Type = "Text" },
            new DiscordChannelInfo { Id = 123456789012345680, Name = "auction-room", Type = "Text" },
            new DiscordChannelInfo { Id = 123456789012345681, Name = "trade-zone", Type = "Text" },
            new DiscordChannelInfo { Id = 123456789012345682, Name = "Text Channels", Type = "Category" }
        };
        return Task.FromResult<List<DiscordChannelInfo>?>(channels);
    }

    public Task<List<DiscordRoleInfo>?> GetDiscordRolesAsync(ulong guildId)
    {
        _logger.LogInformation($"Mocking GetDiscordRolesAsync for guild {guildId}. Returning sample roles.");
        var roles = new List<DiscordRoleInfo>
        {
            new DiscordRoleInfo { Id = 987654321098765432, Name = "Admin" },
            new DiscordRoleInfo { Id = 987654321098765433, Name = "Moderator" },
            new DiscordRoleInfo { Id = 987654321098765434, Name = "Member" },
            new DiscordRoleInfo { Id = 987654321098765435, Name = "Bot Manager" }
        };
        return Task.FromResult<List<DiscordRoleInfo>?>(roles);
    }
}
