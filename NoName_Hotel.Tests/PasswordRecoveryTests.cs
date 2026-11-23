using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Services.AuthServices;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Infrastructure.Data;
using Xunit;

namespace NoName_Hotel.Tests;

public class PasswordRecoveryTests
{
    private const string Email = "guest+reset@example.test";
    private const string Other = "other@example.test";
    private const string Original = "ValidPassword1!";
    private const string Replacement = "RecoveredPassword1!";

    [Theory]
    [InlineData("https://hotel.example.test", "/reset-password")]
    [InlineData("https://hotel.example.test/guests/", "/guests/reset-password")]
    public async Task Reset_email_has_a_valid_route_and_round_trips_the_email_and_identity_token(string frontend, string path)
    {
        using var fixture = await RecoveryFixture.Create(frontend);
        var link = await fixture.RequestLink();
        Assert.Equal(path, link.AbsolutePath);
        var query = QueryHelpers.ParseQuery(link.Query);
        Assert.Equal(Email, query["email"].ToString());
        using var scope = fixture.App.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(Email))!;
        Assert.True(await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose, query["token"].ToString()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Forgot_password_does_not_reveal_accounts_or_delivery_configuration(bool configured, bool failDelivery)
    {
        using var fixture = await RecoveryFixture.Create(configured: configured, failDelivery: failDelivery);
        using var known = await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email });
        using var unknown = await fixture.Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = "missing@example.test" });
        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(configured ? 1 : 0, fixture.Mail.Messages.Count);
        if (configured) Assert.Equal(Email, Assert.Single(fixture.Mail.Messages).Recipient);
    }

    [Fact]
    public async Task Successful_reset_is_single_use_and_invalidates_all_owner_sessions_without_affecting_other_accounts()
    {
        using var fixture = await RecoveryFixture.Create();
        // Seed existing browser sessions without spending the public endpoint's
        // five-request auth allowance on fixture setup. Login is checked below.
        var first = await fixture.CreateSession(Email);
        var second = await fixture.CreateSession(Email);
        var other = await fixture.CreateSession(Other);
        var link = await fixture.RequestLink();
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Reset(link)).StatusCode);
        foreach (var session in new[] { first, second })
        {
            fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", session.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/account")).StatusCode);
            using var refresh = new HttpRequestMessage(HttpMethod.Post, session.Path + "/refresh")
            {
                Content = JsonContent.Create(new { Activity = true })
            };
            refresh.Headers.Add("Cookie", session.Cookie);
            refresh.Headers.Add("X-IGES-Session", "1");
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(refresh)).StatusCode);
        }
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", other.Token);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/account")).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { Email, Password = Original })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Reset(link)).StatusCode);
        await fixture.SignIn(Email, Replacement);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("tampered")]
    [InlineData("other-account")]
    [InlineData("wrong-purpose")]
    [InlineData("expired")]
    [InlineData("weak-password")]
    public async Task Invalid_reset_attempts_do_not_change_passwords_or_invalidate_sessions(string kind)
    {
        using var fixture = await RecoveryFixture.Create(expired: kind == "expired");
        var session = await fixture.SignIn(Email);
        var link = await fixture.RequestLink();
        var query = QueryHelpers.ParseQuery(link.Query);
        var token = query["token"].ToString();
        if (kind == "malformed") token = "not-a-base64-token%";
        if (kind == "tampered") token = token[..10] + (token[10] == 'A' ? "B" : "A") + token[11..];
        if (kind == "wrong-purpose")
        {
            using var scope = fixture.App.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            token = await users.GenerateEmailConfirmationTokenAsync((await users.FindByEmailAsync(Email))!);
        }
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = kind == "other-account" ? Other : Email, Token = token,
            NewPassword = kind == "weak-password" ? "short" : Replacement
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", session.Token);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/account")).StatusCode);
        using var refresh = new HttpRequestMessage(HttpMethod.Post, session.Path + "/refresh")
        {
            Content = JsonContent.Create(new { Activity = true })
        };
        refresh.Headers.Add("Cookie", session.Cookie);
        refresh.Headers.Add("X-IGES-Session", "1");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(refresh)).StatusCode);
        await fixture.SignIn(Email);
        if (kind == "weak-password") Assert.Equal(HttpStatusCode.NoContent, (await fixture.Reset(link)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Missing_reset_tokens_are_rejected_by_the_api(string? token)
    {
        using var fixture = await RecoveryFixture.Create();
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email, Token = token, NewPassword = Replacement
        })).StatusCode);
        await fixture.SignIn(Email);
    }

    [Fact]
    public async Task Invalid_reset_responses_do_not_distinguish_existing_and_unknown_accounts()
    {
        using var fixture = await RecoveryFixture.Create();
        async Task<string> Failure(string email)
        {
            using var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Email = email, Token = "malformed", NewPassword = Replacement
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("detail").GetString()!;
        }
        Assert.Equal(await Failure(Email), await Failure("missing@example.test"));
    }

    private sealed class RecoveryFixture : IDisposable
    {
        private readonly TestApplication root = new();
        public WebApplicationFactory<Program> App { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public RecordingMail Mail { get; private set; } = null!;

        public static async Task<RecoveryFixture> Create(string frontend = "https://hotel.example.test",
            bool configured = true, bool failDelivery = false, bool expired = false)
        {
            var fixture = new RecoveryFixture { Mail = new RecordingMail(configured, failDelivery) };
            fixture.App = fixture.root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PasswordReset:FrontendUrl", frontend);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHotelMailSender>();
                    services.AddSingleton<IHotelMailSender>(fixture.Mail);
                    services.RemoveAll<IDataProtectionProvider>();
                    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                    // Identity uses the system clock. A negative test-only lifetime makes an
                    // authentic issued token expired immediately, without sleeps or a fake validator.
                    if (expired) services.Configure<DataProtectionTokenProviderOptions>(options =>
                        options.TokenLifespan = TimeSpan.FromSeconds(-1));
                });
            });
            using (var scope = fixture.App.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<NoName_hotel_DbContext>().Database.EnsureCreatedAsync();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var email in new[] { Email, Other })
                    Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email }, Original)).Succeeded);
            }
            fixture.Client = fixture.App.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
            return fixture;
        }

        public async Task<Uri> RequestLink()
        {
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email })).StatusCode);
            var message = Assert.Single(Mail.Messages);
            return new Uri(message.Body.Split('\n').Single(line => line.StartsWith("https://", StringComparison.Ordinal)));
        }

        public Task<HttpResponseMessage> Reset(Uri link)
        {
            var query = QueryHelpers.ParseQuery(link.Query);
            return Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                Email = query["email"].ToString(), Token = query["token"].ToString(), NewPassword = Replacement
            });
        }

        public async Task<(string Token, string Cookie, string Path)> SignIn(string email, string password = Original)
        {
            using var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var token = (await response.Content.ReadAsStringAsync()).Trim('"');
            var session = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(claim => claim.Type == "session_id").Value;
            return (token, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0], "/api/v1/auth/session/" + session);
        }

        public async Task<(string Token, string Cookie, string Path)> CreateSession(string email)
        {
            using var scope = App.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tokens = await scope.ServiceProvider.GetRequiredService<BrowserSessionService>()
                .StartAsync((await users.FindByEmailAsync(email))!);
            return (tokens.AccessToken, "iges.refresh=" + tokens.RefreshToken, "/api/v1/auth/session/" + tokens.SessionId);
        }

        public void Dispose() { Client?.Dispose(); App?.Dispose(); root.Dispose(); }
    }

    private sealed class RecordingMail(bool configured, bool failDelivery) : IHotelMailSender
    {
        public bool IsConfigured => configured;
        public List<(string Recipient, string Body)> Messages { get; } = [];
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            Messages.Add((recipient, body));
            return failDelivery ? Task.FromException(new InvalidOperationException("Simulated mail failure")) : Task.CompletedTask;
        }
        public Task SendReplyAsync(string recipient, string subject, string body, string? replyTo, CancellationToken cancellationToken) =>
            SendAsync(recipient, subject, body, cancellationToken);
    }
}
