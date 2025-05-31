using Agora.SettingsManager.Client.Models;
using Agora.SettingsManager.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Agora.SettingsManager.Client;
using MudBlazor.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices(); // MudBlazor services

// Add Authentication services
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("DiscordOidc", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("identify");
    options.ProviderOptions.DefaultScopes.Add("guilds");
});

builder.Services.AddHttpClient("DiscordApi", client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/");
    client.DefaultRequestHeaders.Add("User-Agent", "Agora.SettingsManager (Blazor WASM)");
});

builder.Services.AddScoped<DiscordAuthenticationService>();

builder.Services.AddScoped<Agora.SettingsManager.Client.Services.ISettingsApiService, Agora.SettingsManager.Client.Services.SettingsApiService>();
await builder.Build().RunAsync();
