namespace Loupedeck.HaTTaPticPlugin
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal HTTP server using TcpListener on 127.72.80.84:8080.
    /// Uses raw sockets instead of HttpListener to avoid System.Net.HttpListener
    /// dependency which is not available in the Logi Plugin Service runtime.
    /// </summary>
    public class HttpHapticServer : IDisposable
    {
        private static readonly System.Net.IPAddress ListenAddress = System.Net.IPAddress.Loopback; // 127.0.0.1
        private const int ListenPort = 18274; // "HPT" port — unique enough to avoid conflicts

        private readonly HapticEventRegistry _registry;
        private readonly SemaphoreSlim _hapticLock = new SemaphoreSlim(1, 1);
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public HttpHapticServer(HapticEventRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Start()
        {
            try
            {
                _listener = new TcpListener(ListenAddress, ListenPort);
                _listener.Start();

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoop(_cts.Token));

                PluginLog.Info($"HTTP server started on http://{ListenAddress}:{ListenPort}/");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to start HTTP server on {ListenAddress}:{ListenPort}");
                throw;
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleClient(client), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        PluginLog.Error(ex, "Error accepting TCP connection");
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII))
                {
                    // Read the request line (e.g. "GET /haptic/knock HTTP/1.1")
                    var requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine))
                    {
                        return;
                    }

                    // Consume remaining headers (read until empty line)
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        // discard headers
                    }

                    // Parse request line
                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        SendResponse(stream, 400, "{\"error\":\"Bad request\"}");
                        return;
                    }

                    var method = parts[0];
                    var rawPath = parts[1].TrimEnd('/');

                    // Split path and query string
                    var queryIndex = rawPath.IndexOf('?');
                    var path = queryIndex >= 0 ? rawPath.Substring(0, queryIndex) : rawPath;
                    var query = queryIndex >= 0 ? rawPath.Substring(queryIndex + 1) : "";

                    PluginLog.Verbose($"HTTP {method} {path}");

                    if (method != "GET")
                    {
                        SendResponse(stream, 405, "{\"error\":\"Method not allowed. Use GET.\"}");
                        return;
                    }

                    // Route
                    if (path == "/health")
                    {
                        SendResponse(stream, 200, "{\"status\":\"ok\"}");
                        return;
                    }

                    if (path == "/waveforms")
                    {
                        SendResponse(stream, 200, BuildWaveformsJson());
                        return;
                    }

                    if (path.StartsWith("/haptic/"))
                    {
                        var waveform = path.Substring("/haptic/".Length);

                        if (string.IsNullOrEmpty(waveform))
                        {
                            SendResponse(stream, 400, "{\"error\":\"Missing waveform name. Use /haptic/{waveform}\"}");
                            return;
                        }

                        if (!_registry.IsKnown(waveform))
                        {
                            SendResponse(stream, 404,
                                $"{{\"error\":\"Unknown waveform\",\"requested\":\"{EscapeJson(waveform)}\",\"available\":{BuildWaveformArray()}}}");
                            return;
                        }

                        var waitMs = ParseWaitParam(query);
                        _hapticLock.Wait();
                        try
                        {
                            _registry.Trigger(waveform);
                            if (waitMs > 0)
                            {
                                Thread.Sleep(waitMs);
                            }
                        }
                        finally
                        {
                            _hapticLock.Release();
                        }

                        SendResponse(stream, 200,
                            $"{{\"status\":\"ok\",\"waveform\":\"{EscapeJson(waveform)}\",\"waited\":{waitMs}}}");

                        return;
                    }

                    SendResponse(stream, 404,
                        "{\"error\":\"Not found\",\"routes\":[\"/haptic/{waveform}\",\"/waveforms\",\"/health\"]}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error handling HTTP request");
            }
        }

        private static void SendResponse(NetworkStream stream, int statusCode, string jsonBody)
        {
            var statusText = statusCode switch
            {
                200 => "OK",
                400 => "Bad Request",
                404 => "Not Found",
                405 => "Method Not Allowed",
                500 => "Internal Server Error",
                _ => "Unknown"
            };

            var body = Encoding.UTF8.GetBytes(jsonBody);
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string BuildWaveformsJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"waveforms\":");
            sb.Append(BuildWaveformArray());
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildWaveformArray()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < HapticEventRegistry.Waveforms.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(EscapeJson(HapticEventRegistry.Waveforms[i]));
                sb.Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static int ParseWaitParam(string query)
        {
            const int MaxWaitMs = 5000;

            foreach (var param in query.Split('&'))
            {
                var kv = param.Split('=');
                if (kv.Length == 2
                    && kv[0].Equals("wait", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kv[1], out int ms))
                {
                    return ms > MaxWaitMs ? MaxWaitMs : (ms < 0 ? 0 : ms);
                }
            }

            return 0;
        }

        private static string EscapeJson(string value) =>
            value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listenTask?.Wait(TimeSpan.FromSeconds(2));
                PluginLog.Info("HTTP server stopped");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error stopping HTTP server");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _hapticLock?.Dispose();
        }
    }
}
