using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FixtureManagement.Tests.FullTime;

/// <summary>
/// A tiny web server on localhost for the real page loaders (never FullTime): "/blocked" answers 403,
/// anything else 200, both with a small fixtures table. Records each request's User-Agent.
/// </summary>
public sealed class LocalPageServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly List<string?> userAgents = [];

    public LocalPageServer()
    {
        // 127.0.0.1, not "localhost": a clash with another process (e.g. chromedriver, which also picks a free
        // local port) then fails Start, and we try another port instead of silently sharing one.
        for (var attempt = 1; ; attempt++)
        {
            BaseUrl = $"http://127.0.0.1:{FreePort()}/";
            listener.Prefixes.Clear();
            listener.Prefixes.Add(BaseUrl);
            try
            {
                listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < 5)
            {
            }
        }
        _ = Task.Run(ServeAsync);
    }

    public string BaseUrl { get; private set; } = "";

    public IReadOnlyList<string?> UserAgents
    {
        get { lock (userAgents) { return [.. userAgents]; } }
    }

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }
            if (context.Request.Url!.AbsolutePath == "/favicon.ico")
            {
                context.Response.StatusCode = 404;   // browsers ask for it; don't count it as a request
                context.Response.Close();
                continue;
            }
            lock (userAgents)
            {
                userAgents.Add(context.Request.UserAgent);
            }
            context.Response.StatusCode = context.Request.Url.AbsolutePath == "/blocked" ? 403 : 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            var body = Encoding.UTF8.GetBytes("""<html><body><div class="fixtures-table"><table><tbody><tr><td>L</td></tr></tbody></table></div></body></html>""");
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        listener.Stop();
        listener.Close();
    }
}
