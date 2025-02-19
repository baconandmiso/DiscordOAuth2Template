using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = "Discord";
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie()
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "";
    options.SaveTokens = true;
    options.Scope.Add("identify");
    options.Scope.Add("guilds");

    options.CallbackPath = new PathString("/signin-discord");

    options.AuthorizationEndpoint = DiscordAuthenticationDefaults.AuthorizationEndpoint;
    options.TokenEndpoint = DiscordAuthenticationDefaults.TokenEndpoint;
    options.UserInformationEndpoint = DiscordAuthenticationDefaults.UserInformationEndpoint;

    options.ClaimActions.Clear(); // Remove default claims

    // Add discord claims
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");

    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        // 認証チケットが作成されるときに呼び出されます。
        OnCreatingTicket = async context =>
        {
            try
            {
                context.Identity?.AddClaim(new Claim("urn:discord:access_token", context.AccessToken ?? ""));

                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                context.RunClaimActions(user);
            }
            catch (Exception ex)
            {

            }
        },

        // 認証チケットが作成された後に呼び出されます。
        OnTicketReceived = context =>
        {
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// クッキーの設定
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login"; // ログインページのパス
    options.Cookie.SameSite = SameSiteMode.Lax; // クロスサイトに対してはGETのみ送信許可 (同一サイトはPOSTも送信可能)

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // クッキーの有効時間
    options.SlidingExpiration = true; // クッキーの有効時間を延長 (非アクティブの場合除く)
});

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();

app.MapRazorPages();

app.Run();