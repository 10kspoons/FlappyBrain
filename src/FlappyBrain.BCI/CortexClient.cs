using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace FlappyBrain.BCI;

/// <summary>
/// Emotiv Cortex API v2 client. Connects to wss://localhost:6868,
/// authenticates, and streams mental command events as FlapEvents.
/// Also supports in-app mental command training via the training API.
/// </summary>
public sealed class CortexClient : IAsyncDisposable
{
    private readonly CortexConfig _config;
    private readonly Channel<FlapEvent> _flapChannel = Channel.CreateBounded<FlapEvent>(
        new BoundedChannelOptions(20) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _cts = new();
    private ClientWebSocket? _ws;
    private int _requestId;
    private string? _token;
    private string? _sessionId;
    private string? _headsetId;

    public ChannelReader<FlapEvent> FlapEvents => _flapChannel.Reader;
    public SignalQuality SignalQuality { get; private set; } = SignalQuality.Disconnected;
    public bool IsConnected { get; private set; }
    public double LastCommandPower { get; private set; }
    public string LastCommand { get; private set; } = "";
    public event Action<string, double>? OnMentalCommand;
    public event Action<string>? OnTrainingEvent;

    public CortexClient(CortexConfig config) => _config = config;

    public Task StartAsync() => Task.Run(RunLoop, _cts.Token);

    private async Task RunLoop()
    {
        var backoff = TimeSpan.FromSeconds(2);
        while (!_cts.IsCancellationRequested)
        {
            try { await ConnectAndRunAsync(_cts.Token); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                IsConnected = false;
                SignalQuality = SignalQuality.Disconnected;
                Console.WriteLine($"[Cortex] Disconnected: {ex.Message}. Retrying in {backoff.TotalSeconds}s...");
                await Task.Delay(backoff, _cts.Token);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
            }
        }
        _flapChannel.Writer.TryComplete();
    }

    private async Task ConnectAndRunAsync(CancellationToken ct)
    {
        _ws = new ClientWebSocket();
        _ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await _ws.ConnectAsync(new Uri("wss://localhost:6868"), ct);
        IsConnected = true;
        Console.WriteLine("[Cortex] Connected.");

        await RpcAsync("getCortexInfo", new { }, ct);
        await RpcAsync("requestAccess", new { clientId = _config.ClientId, clientSecret = _config.ClientSecret }, ct);

        var auth = await RpcAsync("authorize", new { clientId = _config.ClientId, clientSecret = _config.ClientSecret, debit = 1 }, ct);
        _token = auth?["result"]?["cortexToken"]?.GetValue<string>()
            ?? throw new InvalidOperationException("[Cortex] Auth failed. Is Emotiv App running? Have you granted access?");

        var headsets = await RpcAsync("queryHeadsets", new { }, ct);
        _headsetId = headsets?["result"]?[0]?["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("[Cortex] No headset found.");
        Console.WriteLine($"[Cortex] Headset: {_headsetId}");

        var session = await RpcAsync("createSession", new { cortexToken = _token, headset = _headsetId, status = "active" }, ct);
        _sessionId = session?["result"]?["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("[Cortex] Session creation failed.");

        if (!string.IsNullOrEmpty(_config.ProfileName))
        {
            try
            {
                await RpcAsync("setupProfile", new { cortexToken = _token, headset = _headsetId, profile = _config.ProfileName, status = "load" }, ct);
                Console.WriteLine($"[Cortex] Profile loaded: {_config.ProfileName}");
            }
            catch { Console.WriteLine("[Cortex] No saved profile found — training required."); }
        }

        await RpcAsync("subscribe", new { cortexToken = _token, session = _sessionId, streams = new[] { "com", "met", "sys" } }, ct);
        Console.WriteLine("[Cortex] Subscribed to com + met + sys streams.");

        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            var evt = await ReceiveAsync(_ws, ct);
            if (evt == null) continue;
            HandleEvent(evt, ct);
        }
    }

    private void HandleEvent(JsonObject evt, CancellationToken ct)
    {
        // Mental command stream
        if (evt["com"] is JsonArray com)
        {
            var action = com[0]?.GetValue<string>() ?? "";
            var power  = com[1]?.GetValue<double>() ?? 0;
            LastCommand = action; LastCommandPower = power;
            OnMentalCommand?.Invoke(action, power);
            if (action == _config.ActionMap && power >= _config.PowerThreshold)
                _flapChannel.Writer.TryWrite(new FlapEvent(DateTimeOffset.UtcNow, action, power));
        }

        // Performance metrics — derive signal quality from focus
        if (evt["met"] is JsonArray met && met.Count > 6)
        {
            try
            {
                var focus = met[6]?.GetValue<double>() ?? 0;
                SignalQuality = focus switch { >= 0.7 => SignalQuality.Excellent, >= 0.5 => SignalQuality.Good, >= 0.3 => SignalQuality.Fair, _ => SignalQuality.Poor };
            }
            catch { }
        }

        // System / training events
        if (evt["sys"] is JsonArray sys && sys.Count >= 2)
        {
            var evtType = sys[1]?.GetValue<string>() ?? "";
            OnTrainingEvent?.Invoke(evtType);
            Console.WriteLine($"[Cortex] Training event: {evtType}");
        }
    }

    // ── In-app training API ───────────────────────────────────────────────────

    /// <summary>Start training a mental command. action = "neutral" or "push" (or any mapped command).</summary>
    public async Task StartTrainingAsync(string action, CancellationToken ct = default)
    {
        if (_token == null || _sessionId == null) throw new InvalidOperationException("Not connected.");
        await RpcAsync("training", new { cortexToken = _token, session = _sessionId, detection = "mentalCommand", action, status = "start" }, ct);
    }

    /// <summary>Accept the most recent training session result.</summary>
    public async Task AcceptTrainingAsync(string action, CancellationToken ct = default)
    {
        if (_token == null || _sessionId == null) throw new InvalidOperationException("Not connected.");
        await RpcAsync("training", new { cortexToken = _token, session = _sessionId, detection = "mentalCommand", action, status = "accept" }, ct);
    }

    /// <summary>Reject the most recent training session result and retry.</summary>
    public async Task RejectTrainingAsync(string action, CancellationToken ct = default)
    {
        if (_token == null || _sessionId == null) throw new InvalidOperationException("Not connected.");
        await RpcAsync("training", new { cortexToken = _token, session = _sessionId, detection = "mentalCommand", action, status = "reject" }, ct);
    }

    /// <summary>Save the current training profile to Emotiv cloud.</summary>
    public async Task SaveProfileAsync(CancellationToken ct = default)
    {
        if (_token == null || _headsetId == null) throw new InvalidOperationException("Not connected.");
        await RpcAsync("setupProfile", new { cortexToken = _token, headset = _headsetId, profile = _config.ProfileName, status = "save" }, ct);
        Console.WriteLine($"[Cortex] Profile saved: {_config.ProfileName}");
    }

    // ── JSON-RPC helpers ──────────────────────────────────────────────────────

    private async Task<JsonObject?> RpcAsync(string method, object @params, CancellationToken ct)
    {
        if (_ws == null) throw new InvalidOperationException("Not connected.");
        var id = Interlocked.Increment(ref _requestId);
        var req = JsonSerializer.Serialize(new { id, jsonrpc = "2.0", method, @params });
        await _ws.SendAsync(Encoding.UTF8.GetBytes(req), WebSocketMessageType.Text, true, ct);
        return await ReceiveAsync(_ws, ct);
    }

    private static async Task<JsonObject?> ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[65536];
        var sb  = new StringBuilder();
        WebSocketReceiveResult r;
        do { r = await ws.ReceiveAsync(buf, ct); sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); }
        while (!r.EndOfMessage);
        return JsonNode.Parse(sb.ToString()) as JsonObject;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _ws?.Dispose();
        _cts.Dispose();
    }
}
