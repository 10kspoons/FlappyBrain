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
    public IReadOnlyDictionary<string, string> ContactQuality => _contactQuality;
    public event Action<string, double>? OnMentalCommand;
    public event Action<string>? OnTrainingEvent;
    public event Action<string>? OnTrainingSucceeded;
    public event Action<string, string>? OnTrainingFailed;
    public event Action<IReadOnlyDictionary<string, string>>? OnContactQualityUpdated;

    // 14-channel Epoc X electrode order (matches Cortex dev stream order).
    private static readonly string[] EpocXElectrodes =
        { "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2", "P8", "T8", "FC6", "F4", "F8", "AF4" };
    private readonly Dictionary<string, string> _contactQuality = new();

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

        await RpcAsync("subscribe", new { cortexToken = _token, session = _sessionId, streams = new[] { "com", "met", "sys", "dev" } }, ct);
        Console.WriteLine("[Cortex] Subscribed to com + met + sys + dev streams.");

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

        // System / training events.
        // Cortex sys frame: ["mentalCommand", "MC_Succeeded", ...] or similar.
        if (evt["sys"] is JsonArray sys && sys.Count >= 2)
        {
            var evtType = sys[1]?.GetValue<string>() ?? "";
            OnTrainingEvent?.Invoke(evtType);
            Console.WriteLine($"[Cortex] Training event: {evtType}");

            // Map the well-known mental-command training events to typed events.
            // The "current action" is whatever the most recent StartTrainingAsync set.
            switch (evtType)
            {
                case "MC_Succeeded":
                    OnTrainingSucceeded?.Invoke(_currentTrainingAction);
                    break;
                case "MC_Failed":
                    OnTrainingFailed?.Invoke(_currentTrainingAction, "Cortex rejected the rep");
                    break;
            }
        }

        // Device / contact-quality stream.
        // Frame shape: { "dev": [overallSignal, batteryPercent, [q1..q14], cqOverall], "time": ... }
        if (evt["dev"] is JsonArray dev && dev.Count >= 3 && dev[2] is JsonArray cqs)
        {
            bool updated = false;
            for (int i = 0; i < EpocXElectrodes.Length && i < cqs.Count; i++)
            {
                // Cortex contact quality values: 0=no signal, 1=very bad, 2=poor, 3=fair, 4=good.
                int q = 0;
                try { q = (int)(cqs[i]?.GetValue<double>() ?? 0); }
                catch { try { q = cqs[i]?.GetValue<int>() ?? 0; } catch { } }
                string label = q switch { >= 4 => "good", 3 => "fair", _ => "bad" };
                var key = EpocXElectrodes[i];
                if (!_contactQuality.TryGetValue(key, out var prev) || prev != label)
                {
                    _contactQuality[key] = label;
                    updated = true;
                }
            }
            if (updated)
            {
                OnContactQualityUpdated?.Invoke(_contactQuality);
            }
        }
    }

    private string _currentTrainingAction = "neutral";

    // ── In-app training API ───────────────────────────────────────────────────

    /// <summary>Start training a mental command. action = "neutral" or "push" (or any mapped command).</summary>
    public async Task StartTrainingAsync(string action, CancellationToken ct = default)
    {
        if (_token == null || _sessionId == null) throw new InvalidOperationException("Not connected.");
        _currentTrainingAction = action;
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

    /// <summary>Erase all training data for the given action.</summary>
    public async Task EraseTrainingAsync(string action, CancellationToken ct = default)
    {
        if (_token == null || _sessionId == null) throw new InvalidOperationException("Not connected.");
        await RpcAsync("training", new { cortexToken = _token, session = _sessionId, detection = "mentalCommand", action, status = "erase" }, ct);
    }

    /// <summary>Returns the list of mental-command actions currently active for this profile.</summary>
    public async Task<string[]> GetTrainedActionsAsync(CancellationToken ct = default)
    {
        if (_token == null) throw new InvalidOperationException("Not connected.");
        var resp = await RpcAsync("mentalCommandActiveAction",
            new { cortexToken = _token, status = "get", profile = _config.ProfileName }, ct);
        if (resp?["result"] is JsonArray arr)
        {
            var actions = new List<string>(arr.Count);
            foreach (var node in arr)
            {
                var s = node?.GetValue<string>();
                if (!string.IsNullOrEmpty(s)) actions.Add(s);
            }
            return actions.ToArray();
        }
        return Array.Empty<string>();
    }

    /// <summary>Snapshot of the latest per-electrode contact quality. Returns "good"/"fair"/"bad" per channel.</summary>
    public IReadOnlyDictionary<string, string> GetHeadsetContactQuality()
    {
        // Return a defensive copy so external readers aren't racing on the live dictionary.
        return new Dictionary<string, string>(_contactQuality);
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
