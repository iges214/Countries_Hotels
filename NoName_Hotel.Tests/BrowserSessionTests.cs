using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NoName_Hotel.Domain.Entities.Models;
using Xunit;

namespace NoName_Hotel.Tests;

public sealed class BrowserSessionTests
{
    [Fact]
    public async Task Idle_session_expires_even_when_access_tokens_are_renewed()
    {
        var clock = new SessionClock();
        using var app = new TestApplication(clock);
        using var client = CreateClient(app);
        var (path, cookie, _) = await SignInAsync(app, client);
        clock.Advance(5);
        var renewed = await RefreshAsync(client, path, cookie, false);
        renewed.EnsureSuccessStatusCode();
        clock.Advance(1);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await RefreshAsync(client, path, ReadCookie(renewed), true)).StatusCode);
    }

    [Fact]
    public async Task Activity_renews_idle_deadline_but_cannot_extend_the_maximum_session()
    {
        var clock = new SessionClock();
        using var app = new TestApplication(clock);
        using var client = CreateClient(app);
        var (path, cookie, _) = await SignInAsync(app, client);
        foreach (var hours in new[] { 5, 5, 5, 5, 3 })
        {
            clock.Advance(hours);
            var renewed = await RefreshAsync(client, path, cookie, true);
            renewed.EnsureSuccessStatusCode();
            cookie = ReadCookie(renewed);
        }
        clock.Advance(1);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await RefreshAsync(client, path, cookie, true)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replay_or_sign_out_revokes_the_rotated_token_and_access(bool signOut)
    {
        using var app = new TestApplication();
        using var client = CreateClient(app);
        var (path, cookie, _) = await SignInAsync(app, client);
        var renewed = await RefreshAsync(client, path, cookie, true);
        renewed.EnsureSuccessStatusCode();
        var replacement = ReadCookie(renewed);
        Assert.NotEqual(cookie, replacement);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await ReadAccessAsync(renewed));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account")).StatusCode);

        if (signOut)
        {
            using var logout = new HttpRequestMessage(HttpMethod.Post, path + "/logout");
            logout.Headers.Add("X-IGES-Session", "1");
            logout.Headers.Add("Cookie", replacement);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await RefreshAsync(client, path, cookie, true)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await RefreshAsync(client, path, replacement, true)).StatusCode);
    }

    [Fact]
    public async Task Password_change_revokes_refresh_and_cross_origin_refresh_is_rejected()
    {
        using var app = new TestApplication();
        using var client = CreateClient(app);
        var (path, cookie, token) = await SignInAsync(app, client);
        using var crossOrigin = new HttpRequestMessage(HttpMethod.Post, path + "/refresh")
        {
            Content = JsonContent.Create(new { Activity = true })
        };
        crossOrigin.Headers.Add("X-IGES-Session", "1");
        crossOrigin.Headers.Add("Sec-Fetch-Site", "same-site");
        crossOrigin.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(crossOrigin)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var changed = await client.PostAsJsonAsync("/api/v1/account/password",
            new { CurrentPassword = "ValidPassword1!", NewPassword = "ChangedPassword1!" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await RefreshAsync(client, path, cookie, true)).StatusCode);
    }

    private static HttpClient CreateClient(TestApplication app) =>
        app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static async Task<(string Path, string Cookie, string Token)> SignInAsync(TestApplication app, HttpClient client)
    {
        await app.SeedAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "session@example.com", Email = "session@example.com" };
            Assert.True((await users.CreateAsync(user, "ValidPassword1!")).Succeeded);
        }
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = "session@example.com", Password = "ValidPassword1!" });
        response.EnsureSuccessStatusCode();
        var access = await ReadAccessAsync(response);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10));
        var path = "/api/v1/auth/session/" + jwt.Claims.Single(c => c.Type == "session_id").Value;
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("secure", setCookie);
        Assert.Contains("samesite=strict", setCookie);
        Assert.Contains("path=" + path, setCookie);
        return (path, ReadCookie(response), access);
    }

    private static string ReadCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private static async Task<string> ReadAccessAsync(HttpResponseMessage response) =>
        (await response.Content.ReadAsStringAsync()).Trim('"');

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string path, string cookie, bool activity)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path + "/refresh")
        {
            Content = JsonContent.Create(new { Activity = activity })
        };
        request.Headers.Add("X-IGES-Session", "1");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int hours) => now = now.AddHours(hours);
    }
}
