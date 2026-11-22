using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GPTBus;

internal static class DesktopLogin
{
    private static string Url64(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static async Task<string> SignIn(HttpClient http, Action<string> pairing, CancellationToken stop)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(8);
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var verifier = Url64(RandomNumberGenerator.GetBytes(32));
        var state = Url64(RandomNumberGenerator.GetBytes(32));
        var challenge = Url64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        pairing(state[..8]);
        var page = $"{ProviderConfig.Site}/connect/authorize#port={port}&state={state}&challenge={challenge}";
        Process.Start(new ProcessStartInfo(page) { UseShellExecute = true });
        while (true)
        {
            using var socket = await listener.AcceptTcpClientAsync(stop);
            using var stream = socket.GetStream();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            string? target;
            try { target = await ReadCallback(stream, port, deadline.Token); }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested) { continue; }
            catch (IOException) { continue; }
            if (target == null) continue;
            var query = System.Web.HttpUtility.ParseQueryString(target);
            if (query["state"] != state) continue;
            if (query["error"] == "access_denied")
            {
                await Respond(stream, "已取消。你可以关闭此页并返回桌面工具。", stop);
                throw new InvalidOperationException("已取消登录。");
            }
            var code = query["code"];
            if (code?.Length != 43 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) continue;
            using var content = new StringContent($"{{\"code\":\"{code}\",\"verifier\":\"{verifier}\"}}", Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(ProviderConfig.Site + "/connect/exchange", content, stop);
            if (!response.IsSuccessStatusCode)
            {
                await Respond(stream, "连接未完成。请返回桌面工具重新登录。", stop);
                throw new InvalidOperationException("授权已过期或无法创建设备密钥，请重新登录。");
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(stop));
            var key = json.RootElement.GetProperty("key").GetString();
            if (string.IsNullOrEmpty(key) || !key.StartsWith("sk-", StringComparison.Ordinal))
                throw new InvalidOperationException("未获得设备密钥，请重新登录。");
            await Respond(stream, "已授权给 GPTBus Connect。请返回桌面工具选择模型。", stop);
            return key;
        }
    }

    private static async Task<string?> ReadCallback(NetworkStream stream, int port, CancellationToken ct)
    {
        var bytes = new byte[4096];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(count), ct);
            if (read == 0) return null;
            count += read;
            var input = Encoding.ASCII.GetString(bytes, 0, count);
            int end = input.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end < 0) continue;
            var lines = input[..end].Split("\r\n");
            var first = lines[0].Split(' ');
            if (first.Length != 3 || first[0] != "GET" || !first[1].StartsWith("/callback?", StringComparison.Ordinal)
                || !lines.Skip(1).Any(h => h.Equals($"Host: 127.0.0.1:{port}", StringComparison.OrdinalIgnoreCase)))
                return null;
            return first[1]["/callback?".Length..];
        }
        return null;
    }

    private static async Task Respond(NetworkStream stream, string message, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes("<!doctype html><html lang=zh-CN><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>GPTBus Connect</title><main style='max-width:32em;margin:15vh auto;padding:24px;font:18px system-ui'><h1>GPTBus Connect</h1><p>" + message + "</p></main></html>");
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'\r\nConnection: close\r\n\r\n");
        try { await stream.WriteAsync(head, ct); await stream.WriteAsync(body, ct); }
        catch (IOException) { /* The browser may close after the exchange has already succeeded. */ }
    }
}
