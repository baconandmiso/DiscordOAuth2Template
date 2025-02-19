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
        // �F�؃`�P�b�g���쐬�����Ƃ��ɌĂяo����܂��B
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

        // �F�؃`�P�b�g���쐬���ꂽ��ɌĂяo����܂��B
        OnTicketReceived = context =>
        {
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// �N�b�L�[�̐ݒ�
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login"; // ���O�C���y�[�W�̃p�X
    options.Cookie.SameSite = SameSiteMode.Lax; // �N���X�T�C�g�ɑ΂��Ă�GET�̂ݑ��M���� (����T�C�g��POST�����M�\)

    options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // �N�b�L�[�̗L������
    options.SlidingExpiration = true; // �N�b�L�[�̗L�����Ԃ����� (��A�N�e�B�u�̏ꍇ����)
});

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();

app.MapRazorPages();

app.Run();