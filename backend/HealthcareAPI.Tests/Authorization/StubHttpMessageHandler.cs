using System.Net;
using System.Text.Json;

namespace HealthcareAPI.Tests.Authorization;

/// <summary>
/// Capture-and-reply HTTP stub: records each request and replies with the
/// queued responses (or the default fallback). Used to stand in for the
/// DatabaseAPI behind the named IHttpClientFactory client.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _replies = new();

    public List<(HttpMethod Method, string Url)> Requests { get; } = new();

    public Func<HttpRequestMessage, HttpResponseMessage> DefaultReply { get; set; } =
        req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContentAllowed(true)
        };

    public void Enqueue(HttpStatusCode status, object body)
    {
        _replies.Enqueue(_ => new HttpResponseMessage(status) { Content = JsonContentAllowed(body) });
    }

    public void EnqueueJson(HttpStatusCode status, string json)
    {
        _replies.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public void EnqueueException(Exception exception)
    {
        _replies.Enqueue(_ => throw exception);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty));
        var reply = _replies.Count > 0 ? _replies.Dequeue() : DefaultReply;
        return Task.FromResult(reply(request));
    }

    private static HttpContent JsonContentAllowed(object body)
    {
        return new StringContent(JsonSerializer.Serialize(new { allowed = body is bool b && b, reason = body is string ? body : null }),
            System.Text.Encoding.UTF8, "application/json");
    }
}
