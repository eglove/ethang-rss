using System.Net;

namespace EthangRss.Core.Tests;

/// <summary>Serves canned responses so fetch tests never touch the network.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public StubHttpHandler(HttpStatusCode statusCode, string body)
        : this(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(body) })
    {
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_responder(request));
    }
}
