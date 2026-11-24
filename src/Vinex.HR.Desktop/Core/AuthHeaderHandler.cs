using System.Net;
using System.Net.Http.Headers;

namespace Vinex.HR.Desktop.Core;

/// <summary>Adds the bearer token; on 401 refreshes the session once and retries, otherwise signs the user out.</summary>
public sealed class AuthHeaderHandler(SessionState session, AnonymousApiClient anonymous) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(ct); // allow a safe resend
        }

        var tokenUsed = session.AccessToken;
        Attach(request, tokenUsed);
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized || session.RefreshToken is null)
        {
            return response;
        }

        if (!await TryRefreshAsync(tokenUsed, ct))
        {
            session.SignOut("Your session has expired. Please sign in again.");
            return response;
        }

        response.Dispose();
        using var retry = Clone(request);
        Attach(retry, session.AccessToken);
        return await base.SendAsync(retry, ct);
    }

    private async Task<bool> TryRefreshAsync(string? tokenUsed, CancellationToken ct)
    {
        await RefreshLock.WaitAsync(ct);
        try
        {
            if (session.AccessToken is not null && session.AccessToken != tokenUsed)
            {
                return true; // another request already refreshed
            }

            var refresh = session.RefreshToken;
            if (refresh is null)
            {
                return false;
            }

            var tokens = await anonymous.PostAsync<Vinex.HR.Contracts.Auth.AuthTokensDto>(
                "api/v1/auth/refresh", new Vinex.HR.Contracts.Auth.RefreshRequest(refresh), ct);
            session.UpdateTokens(tokens);
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static void Attach(HttpRequestMessage request, string? token)
    {
        request.Headers.Authorization = string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Content = original.Content };
        foreach (var header in original.Headers.Where(h => h.Key != "Authorization"))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
