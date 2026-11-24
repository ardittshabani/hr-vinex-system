using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vinex.HR.Contracts.Auth;

namespace Vinex.HR.Tests.Integration;

internal static class HttpExtensions
{
    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static async Task<AuthTokensDto> LoginAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokensDto>())!;
    }

    public static async Task<AuthTokensDto> ChangePasswordAsync(this HttpClient client, string token, string current, string next)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(current, next)),
        }.WithBearer(token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokensDto>())!;
    }

    public static Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url).WithBearer(token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return client.SendAsync(request);
    }
}
