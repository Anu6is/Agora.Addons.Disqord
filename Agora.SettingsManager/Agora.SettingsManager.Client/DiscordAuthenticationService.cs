using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;

namespace Agora.SettingsManager.Client;

// Placeholder for Discord Guild structure
public class DiscordGuild
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public bool Owner { get; set; }
    public long Permissions { get; set; } // Discord permissions are a bitfield
}

public class DiscordAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<DiscordAuthenticationService> _logger;

    // Discord permission constant for 'Manage Server'
    private const long ManageServerPermission = 0x20;

    public DiscordAuthenticationService(IHttpClientFactory clientFactory, IAccessTokenProvider accessTokenProvider, ILogger<DiscordAuthenticationService> logger)
    {
        _httpClient = clientFactory.CreateClient("DiscordApi"); // Use named HttpClient
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
    }

    public async Task<List<DiscordGuild>> GetUserGuildsAsync()
    {
        var tokenResult = await _accessTokenProvider.RequestAccessToken();
        if (tokenResult.TryGetToken(out var token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);
            try
            {
                var guilds = await _httpClient.GetFromJsonAsync<List<DiscordGuild>>("users/@me/guilds");
                return guilds ?? new List<DiscordGuild>();
            }
            catch (AccessTokenNotAvailableException ex)
            {
                _logger.LogError(ex, "Access token not available when fetching guilds.");
                ex.Redirect(); // This will trigger a login redirect if token is not available
            }
            catch (HttpRequestException ex)
            {
                 _logger.LogError(ex, "HTTP request failed when fetching guilds.");
            }
        }
        return new List<DiscordGuild>();
    }

    public async Task<List<DiscordGuild>> GetManagableGuildsAsync()
    {
        var userGuilds = await GetUserGuildsAsync();
        var managableGuilds = new List<DiscordGuild>();

        foreach (var guild in userGuilds)
        {
            if ((guild.Permissions & ManageServerPermission) == ManageServerPermission)
            {
                managableGuilds.Add(guild);
            }
        }
        _logger.LogInformation($"User has {userGuilds.Count} guilds, {managableGuilds.Count} are manageable (bot presence not yet checked).");
        return managableGuilds;
    }
}
