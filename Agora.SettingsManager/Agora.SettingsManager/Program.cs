using Agora.SettingsManager.Components; // Server-side components (Layout, App, etc.)
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect; // For server-side OIDC handling
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents(); // Enable WASM interactivity

// START Authentication Configuration for Server
builder.Services.AddCascadingAuthenticationState(); // Important for Blazor auth state propagation

builder.Services.AddAuthentication(options => {
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme; // Trigger OIDC for login
})
.AddCookie()
.AddOpenIdConnect(options => {
    options.Authority = builder.Configuration["DiscordOidc:Authority"];
    options.ClientId = builder.Configuration["DiscordOidc:ClientId"];
    // options.ClientSecret = builder.Configuration["DiscordOidc:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("identify");
    options.Scope.Add("guilds");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name",
        RoleClaimType = "role"
    };

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            return Task.CompletedTask;
        }
    };
});
// END Authentication Configuration for Server

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery(); // Important for Blazor Web Apps

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Agora.SettingsManager.Client._Imports).Assembly);

app.Run();
