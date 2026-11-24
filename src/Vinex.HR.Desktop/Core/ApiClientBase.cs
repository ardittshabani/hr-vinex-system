using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vinex.HR.Desktop.Core;

/// <summary>An error returned by the API (RFC 9457 problem details) or a network failure (Status = 0).</summary>
public sealed class ApiException(string message, int status, string? code = null,
    IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}

public abstract class ApiClientBase(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<T> GetAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(HttpMethod.Get, url, null, ct);

    public Task<TResponse> PostAsync<TResponse>(string url, object? body, CancellationToken ct = default) =>
        SendAsync<TResponse>(HttpMethod.Post, url, body, ct);

    public Task PostAsync(string url, object? body = null, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, url, body, ct);

    public Task<TResponse> PutAsync<TResponse>(string url, object? body, CancellationToken ct = default) =>
        SendAsync<TResponse>(HttpMethod.Put, url, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return result ?? throw new ApiException("The server returned an empty response.", (int)response.StatusCode);
    }

    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Cannot reach the server. Check your network connection. ({ex.Message})", 0, "network");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("The server took too long to respond.", 0, "timeout");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        try
        {
            throw await ToExceptionAsync(response, ct);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var message = status switch
        {
            401 => "You are not signed in or your session has expired.",
            403 => "You do not have permission to do that.",
            404 => "The requested item was not found.",
            429 => "Too many attempts. Please wait a moment and try again.",
            >= 500 => "The server reported an error. Please try again.",
            _ => "The request could not be completed.",
        };
        string? code = null;
        Dictionary<string, string[]>? errors = null;

        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    {
                        message = title.GetString() ?? message;
                    }

                    if (root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        code = c.GetString();
                    }

                    if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                    {
                        errors = new Dictionary<string, string[]>();
                        foreach (var prop in errs.EnumerateObject())
                        {
                            errors[prop.Name] = prop.Value.ValueKind == JsonValueKind.Array
                                ? prop.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                                : [prop.Value.ToString()];
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // keep the generic message
        }

        return new ApiException(message, status, code, errors);
    }
}

/// <summary>HTTP client without authentication (login, token refresh).</summary>
public sealed class AnonymousApiClient(HttpClient http) : ApiClientBase(http);

/// <summary>HTTP client that attaches the bearer token and refreshes it once on 401.</summary>
public sealed class ApiClient(HttpClient http) : ApiClientBase(http);
