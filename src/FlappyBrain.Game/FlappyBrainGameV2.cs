using System;
using FlappyBrain.BCI;
using FlappyBrain.Scenes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FlappyBrain;


// ===== THEME PALETTE SYSTEM =====
public record struct ThemePalette(
    Color SkyTop, Color SkyMid, Color SkyBot,
    Color GroundTop, Color GroundBot,
    Color RuinCol, Color PipeBody, Color PipeStripe, Color PipeCap,
    Color BirdColor, Color BirdDark,
    Color DustColor, Color SpeedLine
);

public static class Themes
{
    public static ThemePalette Get(string name) => name.ToLower() switch
    {
        "safari" => new ThemePalette(
            new Color(0x1A, 0x3A, 0x5A), new Color(0x6B, 0x8E, 0x23), new Color(0xE8, 0xC4, 0x40),
            new Color(0x8B, 0x69, 0x14), new Color(0x4A, 0x35, 0x0A),
            new Color(0x5C, 0x4A, 0x1A), new Color(0x5A, 0x7A, 0x2A), new Color(0x6A, 0x8A, 0x3A), new Color(0x3A, 0x5A, 0x1A),
            new Color(0xFF, 0x8C, 0x00), new Color(0xCC, 0x5A, 0x00),
            new Color(0xD4, 0xB8, 0x6A, 0x60), new Color(0xE8, 0xC4, 0x40)),
        "steampunk" => new ThemePalette(
            new Color(0x1A, 0x0A, 0x05), new Color(0x4A, 0x2A, 0x0A), new Color(0x7A, 0x45, 0x10),
            new Color(0x3A, 0x20, 0x10), new Color(0x1A, 0x0A, 0x05),
            new Color(0x2A, 0x15, 0x08), new Color(0x8B, 0x45, 0x13), new Color(0xA0, 0x55, 0x25), new Color(0x5A, 0x2A, 0x05),
            new Color(0xD4, 0xA0, 0x17), new Color(0x9A, 0x68, 0x05),
            new Color(0xB4, 0x85, 0x4A, 0x60), new Color(0xD4, 0xA0, 0x17)),
        "postapoc" => new ThemePalette(
            new Color(0x0A, 0x15, 0x05), new Color(0x1A, 0x3A, 0x0A), new Color(0x4A, 0x6A, 0x15),
            new Color(0x1A, 0x3A, 0x10), new Color(0x0A, 0x1A, 0x05),
            new Color(0x15, 0x2A, 0x08), new Color(0x4A, 0x6A, 0x20), new Color(0x5A, 0x8A, 0x30), new Color(0x2A, 0x4A, 0x10),
            new Color(0xE8, 0xE8, 0x40), new Color(0xB8, 0xB8, 0x00),
            new Color(0x8A, 0xD4, 0x6A, 0x60), new Color(0x8A, 0xE8, 0x40)),
        "landmarks" => new ThemePalette(
            new Color(0x00, 0x44, 0x99), new Color(0x00, 0x88, 0xCC), new Color(0x44, 0xCC, 0xFF),
            new Color(0xCC, 0x99, 0x00), new Color(0x88, 0x66, 0x00),
            new Color(0x66, 0x44, 0x00), new Color(0x00, 0x66, 0x00), new Color(0x00, 0x88, 0x00), new Color(0x00, 0x44, 0x00),
            new Color(0xFF, 0xCC, 0x00), new Color(0xCC, 0x88, 0x00),
            new Color(0xFF, 0xEE, 0x88, 0x60), new Color(0xFF, 0xCC, 0x00)),
        "spoons" => new ThemePalette(
            new Color(0x00, 0x2D, 0x2D), new Color(0x00, 0x55, 0x55), new Color(0x00, 0x88, 0x88),
            new Color(0x00, 0x40, 0x40), new Color(0x00, 0x20, 0x20),
            new Color(0x00, 0x2A, 0x2A), new Color(0x00, 0x80, 0x80), new Color(0x00, 0xA0, 0xA0), new Color(0x00, 0x50, 0x50),
            new Color(0xFF, 0xFF, 0xFF), new Color(0xCC, 0xCC, 0xCC),
            new Color(0x88, 0xFF, 0xFF, 0x60), new Color(0x00, 0xFF, 0xFF)),
        // "outback" is the default
        _ => new ThemePalette(
            new Color(0x1A, 0x0F, 0x08), new Color(0x6B, 0x30, 0x20), new Color(0xC4, 0x62, 0x2D),
            new Color(0x3A, 0x20, 0x10), new Color(0x1A, 0x0A, 0x05),
            new Color(0x2A, 0x15, 0x08), new Color(0x5A, 0x30, 0x10), new Color(0x6B, 0x40, 0x20), new Color(0x4A, 0x28, 0x08),
            new Color(0xE8, 0xC4, 0x40), new Color(0xB8, 0x85, 0x10),
            new Color(0xD4, 0x95, 0x6A, 0x60), new Color(0xC4, 0x62, 0x2D))
    };
}

public class FlappyBrainGameV2 : Game
{
    // ===== Logical resolution =====
    static int LogW = 800;   // updated at startup for fullscreen to match display ratio
    const int LogH = 600;

    // ===== Section config =====
    const int TOTAL_SECTIONS = 20;
    const float SECTION_DURATION = 10f;
    const float TRANSITION_DURATION = 3f;
    const float SECTION_BONUS = 5f;
    const float OBSTACLE_POINTS = 1f;
    const int RNG_SEED = 20260509;

    // ===== Bird physics =====
    const float GRAVITY = 0.15f;
    const float FLAP = -6.5f;
    const float TERMINAL = 10f;
    const float BIRD_WIDTH = 40f;
    const float BIRD_HEIGHT = 36f;
    // Outback theme uses the large koala sprite (160x130) — hitbox ~70% of visual
    const float OUTBACK_BIRD_W = 110f;
    const float OUTBACK_BIRD_H = 90f;
    // Gap bonus for outback mode (sprite is much larger than pixel bird)
    const float OUTBACK_GAP_BONUS = 80f;

    // ===== Pipe =====
    const float PIPE_W = 90f;

    // ===== State machine =====
    enum GameState { LaunchMenu, Menu, Playing, GoreAnimation, SectionRetry, SectionTransition, SectionComplete, Victory, Paused, Leaderboard, Training, InAppTraining }

    enum LaunchMode { TrainAndPlay, ExistingTraining, Keyboard }
    LaunchMode _launchMode = LaunchMode.TrainAndPlay;
    int _launchMenuSelection = 0; // 0, 1, 2, or 3

    GameState _state = GameState.Leaderboard;
    bool _skipTraining;
    TrainingScene? _trainingScene;
    float _stateTimer = 0f;
    float _leaderboardScroll = 0f;
    float _leaderboardTitlePulse = 0f;
    float _gameOverHoldTimer = 0f;

    // ===== HTTP control server =====
    enum GameCommand { Start, Pause, Stop, Train }
    readonly ConcurrentQueue<(GameCommand Cmd, string? Player)> _commandQueue = new();
    HttpListener? _httpListener;
    CancellationTokenSource? _httpCts;
    Task? _httpTask;
    volatile bool _trainingRequested = false;
    string? _pendingPlayerName = null;

    // ===== Session leaderboard =====
    readonly List<(string Name, int Score)> _sessionScores = new();
    string _currentPlayerName = "PLAYER";

    // ===== Section state =====
    int _currentSection = 0;
    int _currentPipeIndex = 0;
    float _sectionTimer = 0f;
    float _spawnTimer = 0f;
    int _totalScore = 0;
    int _sectionScore = 0;
    int _sectionAttempts = 1;
    int _bestScore = 0;

    // Pre-generated pipe layout for each section
    float[][] _sectionPipes = Array.Empty<float[]>();

    // ===== Bird =====
    float _birdX, _birdY, _birdVel, _birdRot;
    float _deathX, _deathY;

    // ===== Pipes =====
    class Pipe { public float X; public float GapY; public float GapH; public bool Scored; }
    readonly List<Pipe> _pipes = new();

    // ===== Gore =====
    enum GoreType { BloodBlob, FurChunk, Feather, Bone, BloodSpray }

    class GoreParticle
    {
        public GoreType Type;
        public float X, Y;
        public float VX, VY;
        public float Gravity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
        public float Rotation;
        public float RotSpeed;
        public bool Landed;
        public List<(float X, float Y, float Age)> Trail = new();
    }

    readonly List<GoreParticle> _gore = new();
    readonly List<(float X, float Y, float R, Color C)> _bloodStains = new();

    // ===== Confetti for victory =====
    class Confetti { public float X, Y, VX, VY, Rot, RotSpeed, Size; public Color Color; }
    readonly List<Confetti> _confetti = new();

    // ===== Graphics =====
    GraphicsDeviceManager _graphics = null!;
    SpriteBatch _spriteBatch = null!;
    Texture2D _pixel = null!;
    bool _fullscreen;
    string _themeName = "";
    ThemePalette _themePalette;

    // ===== Gravity slowdown / BCI integration =====
    float _gravityScale = 1.0f;
    float _gravitySlowTimer = 0f;          // seconds remaining for slow-gravity boost
    const float GRAVITY_SLOW_DURATION = 2.5f;  // seconds of gravity reduction per BCI event
    const float GRAVITY_SLOW_SCALE = 0.45f;    // multiplier when slowed
    bool _slowGravityMode = false;         // --slow-gravity flag: always slow
    CortexClient? _cortexClient;
    bool _bciEnabled = false;
    bool _skipTrainingCliOverride = false;
    bool _aiBypassesLaunchMenu = false;
    int _displayW, _displayH;

    RenderTarget2D? _renderTarget;

    // ===== Input =====
    KeyboardState _prevKb;

    // ===== RNG for gore (non-deterministic, doesn't affect layout) =====
    readonly Random _goreRng = new();

    bool _aiMode;
    bool _outbackTheme;
    Texture2D? _koalaTexture;
    const string KOALA_ASSET = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    const string BG_ASSET    = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";
    Texture2D? _bgTexture;
    float _bgScroll = 0f;
    float _demoCollisionTimer = 0f;   // counts real gameplay seconds
    readonly Random _demoRng = new Random(12345);
    bool _learnedMode;
    float[,]? _qTable;

    public FlappyBrainGameV2(bool aiMode = false, bool learnedMode = false, bool outbackTheme = false, bool fullscreen = false, string theme = "", bool slowGravity = false, bool enableBci = false, bool skipTraining = false)
    {
        _skipTraining = skipTraining;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = LogW,
            PreferredBackBufferHeight = LogH,
            SynchronizeWithVerticalRetrace = true,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        _aiMode = aiMode;
        _outbackTheme = outbackTheme || !string.IsNullOrEmpty(theme);
        _learnedMode = learnedMode;
        _fullscreen = fullscreen;
        _themeName = string.IsNullOrEmpty(theme) ? (outbackTheme ? "outback" : "") : theme;
        _themePalette = string.IsNullOrEmpty(_themeName) ? Themes.Get("outback") : Themes.Get(_themeName);

        // Gravity slowdown / BCI init (deferred — actual BCI start happens after launch menu selection)
        _slowGravityMode = slowGravity;
        if (_slowGravityMode) _gravityScale = GRAVITY_SLOW_SCALE;
        _bciEnabled = enableBci;
        _skipTrainingCliOverride = skipTraining;
        _aiBypassesLaunchMenu = aiMode || learnedMode;

        if (fullscreen)
        {
            // Use native display mode — actual dimensions set in Initialize() after device is ready
            _displayW = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _displayH = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            // Expand logical canvas width to match display aspect ratio — fills screen without bars
            // Keep LogH=600 fixed; LogW scales to match the display's width/height ratio
            LogW = (int)Math.Round(LogH * _displayW / (float)_displayH);
            // LogW will be ~960 for 16:10 (1707x1067), ~1067 for 16:9 (1920x1080), etc.
            TrainingScene.LogW = LogW;
        }
        if (_learnedMode)
        {
            _qTable = TryLoadQTable();
            if (_qTable == null)
            {
                Console.WriteLine("[--ai-learned] qtable.bin not found, falling back to geometry controller");
                _learnedMode = false;
            }
            else
            {
                Console.WriteLine("[--ai-learned] loaded Q-table");
            }
        }
        Window.Title = "Flappy Brain V2 — 20 Sections of Pain";
    }

    static float[,]? TryLoadQTable()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "qtable.bin");
        if (!File.Exists(path)) return null;
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            int stateCount = br.ReadInt32();
            int actionCount = br.ReadInt32();
            var q = new float[stateCount, actionCount];
            for (int i = 0; i < stateCount; i++)
                for (int a = 0; a < actionCount; a++)
                    q[i, a] = br.ReadSingle();
            return q;
        }
        catch { return null; }
    }

    protected override void Initialize()
    {
        if (_fullscreen)
        {
            _graphics.PreferredBackBufferWidth  = _displayW;
            _graphics.PreferredBackBufferHeight = _displayH;
            _graphics.HardwareModeSwitch = false;   // borderless (not exclusive fullscreen)
            _graphics.IsFullScreen = true;
            _graphics.ApplyChanges();
            // Read back the ACTUAL back buffer size MonoGame allocated
            _displayW = GraphicsDevice.PresentationParameters.BackBufferWidth;
            _displayH = GraphicsDevice.PresentationParameters.BackBufferHeight;
            // Recompute LogW from the ACTUAL back buffer ratio (may differ from display mode)
            LogW = (int)Math.Round(LogH * _displayW / (float)_displayH);
            TrainingScene.LogW = LogW;
        }
        PreGenerateSectionLayout();

        // Launch menu is the default entry — unless CLI overrides bypass it.
        if (_aiBypassesLaunchMenu)
        {
            // --ai / --ai-learned: skip menu, go straight to keyboard/AI mode (no BCI).
            _bciEnabled = false;
            _skipTraining = true;
            ResetToLeaderboard();
        }
        else if (_skipTrainingCliOverride && _bciEnabled)
        {
            // --skip-training (with BCI available): option 1 — BCI on, skip training, go to Menu.
            _skipTraining = true;
            StartBciOrGame();
        }
        else
        {
            _state = GameState.LaunchMenu;
        }

        StartHttpControlServer();
        base.Initialize();
    }

    protected override void UnloadContent()
    {
        StopHttpControlServer();
        _cortexClient?.DisposeAsync().GetAwaiter().GetResult();
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        StopHttpControlServer();
        base.Dispose(disposing);
    }

    void StartHttpControlServer()
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://+:5001/");
            _httpListener.Start();
            _httpCts = new CancellationTokenSource();
            _httpTask = Task.Run(() => HttpListenerLoop(_httpCts.Token));
            Console.WriteLine("[http] control server listening on :5001");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[http] failed to start control server: {ex.Message}");
            _httpListener = null;
        }
    }

    void StopHttpControlServer()
    {
        try
        {
            _httpCts?.Cancel();
            _httpListener?.Stop();
            _httpListener?.Close();
        }
        catch { }
        _httpListener = null;
    }

    async Task HttpListenerLoop(CancellationToken ct)
    {
        while (_httpListener != null && _httpListener.IsListening && !ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _httpListener.GetContextAsync().ConfigureAwait(false); }
            catch { break; }

            _ = Task.Run(() => HandleHttpRequest(ctx));
        }
    }

    void HandleHttpRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;

            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";

            if (req.HttpMethod != "POST")
            {
                WriteJson(res, 405, "{\"ok\":false,\"error\":\"method not allowed\"}");
                return;
            }

            string? playerName = null;
            try
            {
                if (req.HasEntityBody)
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                    string body = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        const string key = "\"player\"";
                        int idx = body.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            int colon = body.IndexOf(':', idx + key.Length);
                            int q1 = body.IndexOf('"', colon + 1);
                            int q2 = q1 >= 0 ? body.IndexOf('"', q1 + 1) : -1;
                            if (q1 >= 0 && q2 > q1) playerName = body.Substring(q1 + 1, q2 - q1 - 1);
                        }
                    }
                }
            }
            catch { /* body parse failures fall through with null name */ }

            GameCommand? cmd = path switch
            {
                "/control/start" => GameCommand.Start,
                "/control/pause" => GameCommand.Pause,
                "/control/stop"  => GameCommand.Stop,
                "/control/train" => GameCommand.Train,
                _ => null,
            };

            if (cmd == null)
            {
                WriteJson(res, 404, "{\"ok\":false,\"error\":\"unknown route\"}");
                return;
            }

            _commandQueue.Enqueue((cmd.Value, playerName));
            WriteJson(res, 200, "{\"ok\":true}");
        }
        catch (Exception ex)
        {
            try { WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"" + JsonEscape(ex.Message) + "\"}"); } catch { }
        }
    }

    static void WriteJson(HttpListenerResponse res, int status, string body)
    {
        res.StatusCode = status;
        res.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(body);
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.Close();
    }

    static string JsonEscape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        if (_fullscreen)
        {
            _renderTarget = new RenderTarget2D(GraphicsDevice, LogW, LogH);
        }
        if (_outbackTheme && System.IO.File.Exists(BG_ASSET))
        {
            try { _bgTexture = Texture2D.FromFile(GraphicsDevice, BG_ASSET); }
            catch { }
        }
        if (_outbackTheme && System.IO.File.Exists(KOALA_ASSET))
        {
            try { _koalaTexture = Texture2D.FromFile(GraphicsDevice, KOALA_ASSET); }
            catch { /* fallback to pixel art */ }
        }
        _pixel.SetData(new[] { Color.White });
    }

    void PreGenerateSectionLayout()
    {
        _sectionPipes = new float[TOTAL_SECTIONS][];
        for (int s = 0; s < TOTAL_SECTIONS; s++)
        {
            var rng = new Random(RNG_SEED + s);
            float spawnInt = SectionSpawnInt(s);
            int count = (int)MathF.Ceiling(SECTION_DURATION / spawnInt) + 2;
            var arr = new float[count];
            float gap = SectionGap(s);
            float minY = gap / 2f + 60f;
            float maxY = LogH - gap / 2f - 60f;
            for (int i = 0; i < count; i++)
            {
                arr[i] = minY + (float)rng.NextDouble() * (maxY - minY);
            }
            _sectionPipes[s] = arr;
        }
    }

    static float SectionSpeed(int s) => 160f + s * 16f;
    static float SectionGap(int s) => MathF.Max(100f, 210f - s * 5.5f);
    static float SectionSpawnInt(int s) => MathF.Max(0.8f, 4.6f - s * 0.2f);

    void ResetToMenu()
    {
        _state = GameState.Menu;
        _stateTimer = 0;
        _currentSection = 0;
        _currentPipeIndex = 0;
        _sectionTimer = 0;
        _spawnTimer = 0;
        _totalScore = 0;
        _sectionScore = 0;
        _sectionAttempts = 1;
        _pipes.Clear();
        _gore.Clear();
        _bloodStains.Clear();
        _confetti.Clear();
        _birdX = 200;
        _birdY = LogH / 2f;
        _birdVel = 0;
        _birdRot = 0;
    }

    void ResetToLeaderboard()
    {
        ResetToMenu();
        _state = GameState.Leaderboard;
        _stateTimer = 0;
        _gameOverHoldTimer = 0;
    }

    void RecordSessionScore()
    {
        if (_totalScore <= 0) return;
        var name = string.IsNullOrWhiteSpace(_currentPlayerName) ? "PLAYER" : _currentPlayerName;
        _sessionScores.Add((name, _totalScore));
        _sessionScores.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (_sessionScores.Count > 20) _sessionScores.RemoveRange(20, _sessionScores.Count - 20);
    }

    void StartSection(int section, bool isRetry)
    {
        _currentSection = section;
        _currentPipeIndex = 0;
        _sectionTimer = 0;
        _spawnTimer = 0;
        _sectionScore = 0;
        _pipes.Clear();
        _gore.Clear();
        _bloodStains.Clear();
        _birdX = 200;
        _birdY = LogH / 2f;
        _birdVel = 0;
        _birdRot = 0;
        if (isRetry) _sectionAttempts++;
        else _sectionAttempts = 1;
        _state = GameState.Playing;
        _stateTimer = 0;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // ===== BCI Gravity Slowdown =====
        if (!_slowGravityMode)
        {
            // Poll BCI for mental commands
            if (_cortexClient != null)
            {
                while (_cortexClient.FlapEvents.TryRead(out var ev))
                {
                    if (ev.Action == "push" || ev.Action == "lift")
                    {
                        _gravitySlowTimer = GRAVITY_SLOW_DURATION;
                        Console.WriteLine($"[BCI] Mental command '{ev.Action}' (power={ev.Power:F2}) → gravity slowdown for {GRAVITY_SLOW_DURATION}s");
                    }
                }
            }
            // Tick gravity timer
            if (_gravitySlowTimer > 0f)
            {
                _gravitySlowTimer -= dt;
                _gravityScale = _gravitySlowTimer > 0f ? GRAVITY_SLOW_SCALE : 1.0f;
            }
        }
        var kb = Keyboard.GetState();

        // ESC from LaunchMenu = exit app; ESC from anywhere else = return to launch menu
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            if (_state == GameState.LaunchMenu)
                Exit();
            else
                ReturnToLaunchMenu();
        }

        // Drain HTTP command queue on the game thread
        while (_commandQueue.TryDequeue(out var c))
        {
            ProcessCommand(c.Cmd, c.Player);
        }

        if (_trainingRequested)
        {
            _trainingRequested = false;
            StartTraining();
        }

        bool flapPressed = (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                           (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up));
        bool restartPressed = kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R);
        bool pausePressed = kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P);
        bool retrainPressed = kb.IsKeyDown(Keys.T) && _prevKb.IsKeyUp(Keys.T);

        if (restartPressed && _state != GameState.InAppTraining && _state != GameState.LaunchMenu)
        {
            ResetToLeaderboard();
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

        if (pausePressed)
        {
            TogglePause();
        }

        _stateTimer += dt;
        _leaderboardTitlePulse += dt;

        switch (_state)
        {
            case GameState.LaunchMenu:
                UpdateLaunchMenu(kb);
                break;

            case GameState.Leaderboard:
                _leaderboardScroll += dt * 30f; // slow continuous parallax scroll
                if (flapPressed)
                {
                    BeginGameRun();
                }
                else if (retrainPressed && _bciEnabled && _trainingScene == null)
                {
                    StartInAppRetrain();
                }
                break;

            case GameState.Menu:
                if (_aiMode) flapPressed = true; // auto-start
                if (flapPressed)
                {
                    BeginGameRun();
                }
                else if (retrainPressed && _bciEnabled && _trainingScene == null)
                {
                    StartInAppRetrain();
                }
                break;

            case GameState.Playing:
                if (_aiMode) flapPressed = AiShouldFlap();
                UpdatePlaying(dt, flapPressed);
                break;

            case GameState.Paused:
                // freeze update loop; nothing advances
                break;

            case GameState.GoreAnimation:
                UpdateGore(dt);
                if (_stateTimer >= 2.5f)
                {
                    _state = GameState.SectionRetry;
                    _stateTimer = 0;
                }
                break;

            case GameState.SectionRetry:
                UpdateGore(dt); // gore continues to settle
                _gameOverHoldTimer += dt;
                if (_gameOverHoldTimer >= 5.0f)
                {
                    RecordSessionScore();
                    ResetToLeaderboard();
                }
                break;

            case GameState.SectionTransition:
                if (_aiMode) flapPressed = AiShouldFlap();
                UpdateBird(dt, flapPressed, allowFlap: true, allowDeath: false);
                // pipes drift off
                foreach (var p in _pipes) p.X -= SectionSpeed(_currentSection) * dt;
                _pipes.RemoveAll(p => p.X + PIPE_W < -10);
                if (_stateTimer >= TRANSITION_DURATION)
                {
                    int next = _currentSection + 1;
                    if (next >= TOTAL_SECTIONS)
                    {
                        _state = GameState.Victory;
                        _stateTimer = 0;
                        SpawnConfetti();
                    }
                    else
                    {
                        StartSection(next, false);
                    }
                }
                break;

            case GameState.Victory:
                UpdateConfetti(dt);
                if (flapPressed || _stateTimer >= 8.0f)
                {
                    RecordSessionScore();
                    ResetToLeaderboard();
                }
                break;

            case GameState.Training:
                _leaderboardScroll += dt * 15f;
                if (_stateTimer >= 30f)
                {
                    ResetToLeaderboard();
                }
                break;

            case GameState.InAppTraining:
                if (_trainingScene != null)
                {
                    _trainingScene.Update(dt, kb);
                    if (_trainingScene.IsComplete)
                    {
                        if (_trainingScene.Result == TrainingScene.Outcome.Skipped)
                        {
                            // User opted to play with keyboard only — disable BCI for this session.
                            _bciEnabled = false;
                        }
                        _trainingScene.Detach();
                        _trainingScene = null;
                        ResetToLeaderboard();
                    }
                }
                break;
        }

        if (_totalScore > _bestScore) _bestScore = _totalScore;

        _prevKb = kb;
        base.Update(gameTime);
    }

    void BeginGameRun()
    {
        if (!string.IsNullOrWhiteSpace(_pendingPlayerName))
        {
            _currentPlayerName = _pendingPlayerName!.Trim();
            _pendingPlayerName = null;
        }
        _totalScore = 0;
        _gameOverHoldTimer = 0;
        StartSection(0, false);
    }

    void TogglePause()
    {
        if (_state == GameState.Playing)
        {
            _state = GameState.Paused;
        }
        else if (_state == GameState.Paused)
        {
            _state = GameState.Playing;
        }
    }

    void ProcessCommand(GameCommand cmd, string? player)
    {
        switch (cmd)
        {
            case GameCommand.Start:
                if (!string.IsNullOrWhiteSpace(player)) _pendingPlayerName = player;
                if (_state == GameState.Paused) { _state = GameState.Playing; break; }
                if (_state == GameState.Leaderboard || _state == GameState.Menu || _state == GameState.Victory || _state == GameState.SectionRetry || _state == GameState.Training)
                {
                    BeginGameRun();
                }
                break;

            case GameCommand.Pause:
                TogglePause();
                break;

            case GameCommand.Stop:
                if (_state == GameState.Playing || _state == GameState.Paused || _state == GameState.SectionTransition || _state == GameState.GoreAnimation || _state == GameState.SectionRetry)
                {
                    RecordSessionScore();
                }
                ResetToLeaderboard();
                break;

            case GameCommand.Train:
                _trainingRequested = true;
                break;
        }
    }

    void StartTraining()
    {
        Console.WriteLine("[train] training requested — entering training mode (BCI calibration stub)");
        _state = GameState.Training;
        _stateTimer = 0;
    }

    void UpdateLaunchMenu(KeyboardState kb)
    {
        bool key1 = (kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1)) ||
                    (kb.IsKeyDown(Keys.NumPad1) && _prevKb.IsKeyUp(Keys.NumPad1));
        bool key2 = (kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2)) ||
                    (kb.IsKeyDown(Keys.NumPad2) && _prevKb.IsKeyUp(Keys.NumPad2));
        bool key3 = (kb.IsKeyDown(Keys.D3) && _prevKb.IsKeyUp(Keys.D3)) ||
                    (kb.IsKeyDown(Keys.NumPad3) && _prevKb.IsKeyUp(Keys.NumPad3));
        bool key4 = (kb.IsKeyDown(Keys.D4) && _prevKb.IsKeyUp(Keys.D4)) ||
                    (kb.IsKeyDown(Keys.NumPad4) && _prevKb.IsKeyUp(Keys.NumPad4));

        if (key1)
        {
            _launchMenuSelection = 0;
            _launchMode = LaunchMode.TrainAndPlay;
            _bciEnabled = true;
            _skipTraining = false;
            StartBciOrGame();
        }
        else if (key2)
        {
            _launchMenuSelection = 1;
            _launchMode = LaunchMode.ExistingTraining;
            _bciEnabled = true;
            _skipTraining = true;
            StartBciOrGame();
        }
        else if (key3)
        {
            _launchMenuSelection = 2;
            _launchMode = LaunchMode.Keyboard;
            _bciEnabled = false;
            _skipTraining = true;
            ResetToLeaderboard();
        }
        else if (key4)
        {
            _launchMenuSelection = 3;
            _launchMode = LaunchMode.Keyboard;
            _bciEnabled = false;
            _skipTraining = true;
            _outbackTheme = true;
            _themeName = "outback";
            _themePalette = Themes.Get("outback");
            TryLoadOutbackAssets();
            ResetToLeaderboard();
        }
    }

    void TryLoadOutbackAssets()
    {
        const string koalaPath = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
        const string bgPath    = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";
        if (_koalaTexture == null && System.IO.File.Exists(koalaPath))
            try { _koalaTexture = Texture2D.FromFile(GraphicsDevice, koalaPath); } catch { }
        if (_bgTexture == null && System.IO.File.Exists(bgPath))
            try { _bgTexture = Texture2D.FromFile(GraphicsDevice, bgPath); } catch { }
    }

    void ReturnToLaunchMenu()
    {
        if (_cortexClient != null)
        {
            try { _ = _cortexClient.DisposeAsync(); } catch { }
            _cortexClient = null;
        }
        if (_trainingScene != null)
        {
            try { _trainingScene.Detach(); } catch { }
            _trainingScene = null;
        }
        _bciEnabled = false;
        _skipTraining = true;
        _launchMenuSelection = 0;
        _launchMode = LaunchMode.TrainAndPlay;
        ResetToMenu();
        _state = GameState.LaunchMenu;
    }

    /// <summary>
    /// Apply the launch menu selection: optionally bring up the Cortex BCI client,
    /// and route to either the in-app training scene or directly to the leaderboard/menu.
    /// </summary>
    void StartBciOrGame()
    {
        if (_bciEnabled && _cortexClient == null)
        {
            var config = new CortexConfig { ProfileName = "flappybrain", ActionMap = "push", PowerThreshold = 0.5 };
            _cortexClient = new CortexClient(config);
            _ = _cortexClient.StartAsync();
            Console.WriteLine("[BCI] Cortex client started — connecting to Epoc X on wss://localhost:6868...");
        }

        if (_bciEnabled && !_skipTraining)
        {
            _trainingScene = new TrainingScene(
                _cortexClient,
                (text, x, y, sz, c, centered, outline) => DrawBigText(text, x, y, sz, c, centered, outline),
                (x, y, w, h, c) => DrawRect(x, y, w, h, c),
                (cx, cy, r, c) => DrawCircle(cx, cy, r, c));
            _state = GameState.InAppTraining;
            _stateTimer = 0;
        }
        else
        {
            ResetToLeaderboard();
        }
    }

    /// <summary>
    /// Operator pressed T on the attract/leaderboard screen — restart the in-app
    /// Cortex training flow so the next attendee can calibrate without restarting the kiosk.
    /// </summary>
    void StartInAppRetrain()
    {
        Console.WriteLine("[train] T pressed on leaderboard — restarting in-app BCI training");
        _trainingScene = new TrainingScene(
            _cortexClient,
            (text, x, y, sz, c, centered, outline) => DrawBigText(text, x, y, sz, c, centered, outline),
            (x, y, w, h, c) => DrawRect(x, y, w, h, c),
            (cx, cy, r, c) => DrawCircle(cx, cy, r, c));
        _trainingScene.Reset();
        _state = GameState.InAppTraining;
        _stateTimer = 0;
    }

    void UpdatePlaying(float dt, bool flapPressed)
    {
        if (_outbackTheme) _bgScroll += 2.4f;

        // Demo: every 15 seconds of play, 1-in-3 chance of forced collision (only when pipes are on screen)
        if (_aiMode)
        {
            _demoCollisionTimer += dt;
            if (_demoCollisionTimer >= 15f)
            {
                _demoCollisionTimer = 0f;
                if (_pipes.Count > 0 && _demoRng.Next(3) == 0)  // 1-in-3, only during active pipe sections
                    TriggerGore();
            }
        }

        _sectionTimer += dt;
        _spawnTimer += dt;

        UpdateBird(dt, flapPressed, allowFlap: true, allowDeath: true);

        // Spawn pipes
        if (_sectionTimer < SECTION_DURATION)
        {
            float spawnInt = SectionSpawnInt(_currentSection);
            if (_spawnTimer >= spawnInt && _currentPipeIndex < _sectionPipes[_currentSection].Length)
            {
                _spawnTimer = 0;
                float gapY = _sectionPipes[_currentSection][_currentPipeIndex];
                _currentPipeIndex++;
                float gapH = SectionGap(_currentSection) + (_outbackTheme ? OUTBACK_GAP_BONUS : 0f);
                _pipes.Add(new Pipe { X = LogW + 20, GapY = gapY, GapH = gapH });
            }
        }

        // Move pipes
        float speed = SectionSpeed(_currentSection);
        foreach (var p in _pipes) p.X -= speed * dt;

        // Score & remove
        for (int i = _pipes.Count - 1; i >= 0; i--)
        {
            var p = _pipes[i];
            if (!p.Scored && p.X + PIPE_W < _birdX)
            {
                p.Scored = true;
                _totalScore++;
                _sectionScore++;
            }
            if (p.X + PIPE_W < -10) _pipes.RemoveAt(i);
        }

        // Collision
        if (_state == GameState.Playing)
        {
            CheckCollision();
        }

        // Section complete?
        if (_state == GameState.Playing && _sectionTimer >= SECTION_DURATION && _pipes.Count == 0)
        {
            _totalScore += (int)SECTION_BONUS;
            _state = GameState.SectionTransition;
            _stateTimer = 0;
        }
    }

    void UpdateBird(float dt, bool flapPressed, bool allowFlap, bool allowDeath)
    {
        if (allowFlap && flapPressed) _birdVel = FLAP;

        _birdVel += GRAVITY * _gravityScale;
        if (_birdVel > TERMINAL * _gravityScale) _birdVel = TERMINAL * _gravityScale;
        _birdY += _birdVel;

        _birdRot = MathHelper.Clamp(_birdVel * 0.07f, -0.5f, 1.2f);

        if (allowDeath && (_birdY < 10 || _birdY > LogH - 60))
        {
            TriggerGore();
        }

        // Keep bird in bounds during transition
        if (!allowDeath)
        {
            if (_birdY < 20) { _birdY = 20; _birdVel = 0; }
            if (_birdY > LogH - 20) { _birdY = LogH - 20; _birdVel = 0; }
        }
    }

    void CheckCollision()
    {
        float bW = _outbackTheme ? OUTBACK_BIRD_W : BIRD_WIDTH;
        float bH = _outbackTheme ? OUTBACK_BIRD_H : BIRD_HEIGHT;
        var birdBox = Collision.BirdHitbox(_birdX, _birdY, bW, bH);
        foreach (var p in _pipes)
        {
            var top = Collision.PipeTopRect(p.X, PIPE_W, p.GapY, p.GapH);
            var bot = Collision.PipeBottomRect(p.X, PIPE_W, p.GapY, p.GapH, LogH);
            if (birdBox.Intersects(top) || birdBox.Intersects(bot))
            {
                TriggerGore();
                return;
            }
        }
    }

    void TriggerGore()
    {
        _state = GameState.GoreAnimation;
        _stateTimer = 0;
        _gameOverHoldTimer = 0;
        _deathX = _birdX;
        _deathY = MathHelper.Clamp(_birdY, 0, LogH);
        SpawnGoreParticles(_deathX, _deathY);
    }

    void SpawnGoreParticles(float cx, float cy)
    {
        var rng = _goreRng;

        // BloodBlob
        int blobs = rng.Next(30, 41);
        for (int i = 0; i < blobs; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 150f + (float)rng.NextDouble() * 250f;
            byte r = (byte)rng.Next(0x88, 0xCD);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.BloodBlob,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 280f,
                MaxLife = 1.8f + (float)rng.NextDouble() * 0.7f,
                Size = 6f + (float)rng.NextDouble() * 12f,
                Color = new Color(r, (byte)(0x08 + rng.Next(8)), (byte)(0x08 + rng.Next(8))),
            });
        }

        // FurChunk
        int furs = rng.Next(15, 21);
        for (int i = 0; i < furs; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 100f + (float)rng.NextDouble() * 180f;
            byte r = (byte)rng.Next(0x8B, 0xC5);
            byte g = (byte)rng.Next(0x60, 0x90);
            byte b = (byte)rng.Next(0x40, 0x60);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.FurChunk,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 220f,
                MaxLife = 2.0f + (float)rng.NextDouble() * 0.8f,
                Size = 8f + (float)rng.NextDouble() * 12f,
                Color = new Color(r, g, b),
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 8f,
            });
        }

        // Feathers
        int feathers = rng.Next(10, 16);
        for (int i = 0; i < feathers; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 60f + (float)rng.NextDouble() * 100f;
            bool grey = rng.NextDouble() > 0.5;
            Color c = grey ? new Color(0x80, 0x80, 0x80) : new Color(0xD4, 0xC0, 0x90);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.Feather,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 100f,
                MaxLife = 2.5f + (float)rng.NextDouble() * 1.0f,
                Size = 5f,
                Color = c,
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 4f,
            });
        }

        // Bones
        int bones = rng.Next(5, 9);
        for (int i = 0; i < bones; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 200f + (float)rng.NextDouble() * 250f;
            _gore.Add(new GoreParticle
            {
                Type = GoreType.Bone,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 350f,
                MaxLife = 1.5f + (float)rng.NextDouble() * 0.5f,
                Size = 4f,
                Color = new Color(0xF0, 0xE8, 0xD0),
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 12f,
            });
        }

        // Blood spray
        int sprays = rng.Next(20, 31);
        for (int i = 0; i < sprays; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 300f + (float)rng.NextDouble() * 300f;
            _gore.Add(new GoreParticle
            {
                Type = GoreType.BloodSpray,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 200f,
                MaxLife = 0.8f + (float)rng.NextDouble() * 0.6f,
                Size = 2f + (float)rng.NextDouble() * 4f,
                Color = new Color(255, 16, 32) * 0.7f,
            });
        }
    }

    void UpdateGore(float dt)
    {
        for (int i = _gore.Count - 1; i >= 0; i--)
        {
            var g = _gore[i];
            g.Life += dt;
            if (g.Life >= g.MaxLife)
            {
                _gore.RemoveAt(i);
                continue;
            }
            if (!g.Landed)
            {
                g.X += g.VX * dt;
                g.Y += g.VY * dt;
                g.VY += g.Gravity * dt;
                g.Rotation += g.RotSpeed * dt;

                // Trails for blood spray
                if (g.Type == GoreType.BloodSpray)
                {
                    g.Trail.Add((g.X, g.Y, 0));
                    if (g.Trail.Count > 3) g.Trail.RemoveAt(0);
                }

                // Blood blob hits ground
                if (g.Type == GoreType.BloodBlob && g.Y >= LogH - 80)
                {
                    g.Y = LogH - 80;
                    g.Landed = true;
                    _bloodStains.Add((g.X, g.Y + g.Size * 0.4f, g.Size * 1.2f, new Color(g.Color.R, g.Color.G, g.Color.B) * 0.6f));
                }

                // Fur chunks settle on ground band
                if (g.Type == GoreType.FurChunk && g.Y >= LogH - 60)
                {
                    g.Y = LogH - 60 + (float)_goreRng.NextDouble() * 8f;
                    g.VX *= 0.4f;
                    g.VY = 0;
                    g.Gravity = 0;
                    g.RotSpeed *= 0.3f;
                }
            }
        }
    }

    void SpawnConfetti()
    {
        var rng = _goreRng;
        for (int i = 0; i < 200; i++)
        {
            Color c = new Color(rng.Next(100, 256), rng.Next(100, 256), rng.Next(100, 256));
            _confetti.Add(new Confetti
            {
                X = (float)rng.NextDouble() * LogW,
                Y = -20 - (float)rng.NextDouble() * 200,
                VX = ((float)rng.NextDouble() - 0.5f) * 80,
                VY = 60f + (float)rng.NextDouble() * 100,
                Rot = (float)rng.NextDouble() * MathF.PI * 2,
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 8,
                Size = 6f + (float)rng.NextDouble() * 6,
                Color = c,
            });
        }
    }

    void UpdateConfetti(float dt)
    {
        foreach (var c in _confetti)
        {
            c.X += c.VX * dt;
            c.Y += c.VY * dt;
            c.Rot += c.RotSpeed * dt;
            if (c.Y > LogH + 20) c.Y = -20;
        }
    }

    // ===== DRAWING =====

    protected override void Draw(GameTime gameTime)
    {
        if (_fullscreen && _renderTarget != null)
        {
            GraphicsDevice.SetRenderTarget(_renderTarget);
            DrawScene(gameTime);
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Viewport = new Viewport(0, 0, _displayW, _displayH);
            GraphicsDevice.Clear(Color.Black);
            var vp = GraphicsDevice.Viewport;
            float scale = Math.Min(vp.Width / (float)LogW, vp.Height / (float)LogH);
            int dstW = (int)(LogW * scale), dstH = (int)(LogH * scale);
            int offX = (vp.Width - dstW) / 2, offY = (vp.Height - dstH) / 2;
            _spriteBatch.Begin(samplerState: Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp);
            _spriteBatch.Draw(_renderTarget, new Rectangle(offX, offY, dstW, dstH), Color.White);
            _spriteBatch.End();
            base.Draw(gameTime);
            return;
        }

        DrawScene(gameTime);
    }

    void DrawScene(GameTime gameTime)
    {
        GraphicsDevice.Clear(_outbackTheme ? new Color(0x1A, 0x0F, 0x08) : new Color(0x6E, 0xC8, 0xE6));

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_state == GameState.LaunchMenu)
        {
            DrawLaunchMenu();
            _spriteBatch.End();
            if (!_fullscreen) base.Draw(gameTime);
            return;
        }

        if (_state == GameState.Leaderboard || _state == GameState.Training)
        {
            DrawLeaderboardScene();
            if (_state == GameState.Training) DrawTrainingOverlay();
            _spriteBatch.End();
            if (!_fullscreen) base.Draw(gameTime);
            return;
        }

        if (_state == GameState.InAppTraining && _trainingScene != null)
        {
            _trainingScene.Draw();
            _spriteBatch.End();
            if (!_fullscreen) base.Draw(gameTime);
            return;
        }

        // Compute screen shake offset
        Vector2 shake = Vector2.Zero;
        if (_state == GameState.GoreAnimation)
        {
            float amt = MathHelper.Clamp(12f * (1f - _stateTimer / 2.5f), 0, 12);
            shake = new Vector2(MathF.Sin(_stateTimer * 80f) * amt, MathF.Cos(_stateTimer * 70f) * amt);
        }

        DrawSky(shake);
        DrawGround(shake);

        // Pipes
        if (_state == GameState.Playing || _state == GameState.Paused || _state == GameState.GoreAnimation ||
            _state == GameState.SectionRetry || _state == GameState.SectionTransition)
        {
            foreach (var p in _pipes) DrawPipe(p, shake);
        }

        // Blood stains (persist on ground)
        foreach (var s in _bloodStains)
        {
            DrawCircle(s.X + shake.X, s.Y + shake.Y, s.R, s.C);
        }

        // Gore particles
        foreach (var g in _gore) DrawGoreParticle(g, shake);

        // Bird (not during gore)
        if (_state != GameState.GoreAnimation && _state != GameState.SectionRetry && _state != GameState.Victory)
        {
            DrawBird(_birdX + shake.X, _birdY + shake.Y, _birdRot);
        }

        // Red flash on gore
        if (_state == GameState.GoreAnimation && _stateTimer < 0.15f)
        {
            float a = (1f - _stateTimer / 0.15f) * 0.6f;
            DrawRect(0, 0, LogW, LogH, new Color(255, 0, 0) * a);
        }

        // State-specific overlays
        switch (_state)
        {
            case GameState.Menu: DrawMenu(); break;
            case GameState.Playing: DrawHud(); break;
            case GameState.Paused: DrawHud(); DrawPauseOverlay(); break;
            case GameState.GoreAnimation: DrawGoreOverlay(); DrawHud(); break;
            case GameState.SectionRetry: DrawRetryOverlay(); break;
            case GameState.SectionTransition: DrawTransitionOverlay(); DrawHud(); break;
            case GameState.Victory: DrawVictoryOverlay(); break;
        }

        _spriteBatch.End();
        if (!_fullscreen) base.Draw(gameTime);
    }

    void DrawPauseOverlay()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.55f);
        DrawBigText("* PAUSED *", LogW / 2f, LogH / 2f - 30, 64, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        DrawBigText("PRESS P OR RESUME FROM CONSOLE", LogW / 2f, LogH / 2f + 40, 20, Color.White, centered: true, outline: true);
    }

    void DrawTrainingOverlay()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.65f);
        DrawBigText("TRAINING MODE", LogW / 2f, LogH / 2f - 60, 56, new Color(0xC0, 0x80, 0xFF), centered: true, outline: true);
        DrawBigText("FOLLOW HEADSET INSTRUCTIONS", LogW / 2f, LogH / 2f + 10, 22, Color.White, centered: true, outline: true);
        float secLeft = MathF.Max(0, 30f - _stateTimer);
        DrawBigText($"{secLeft:F0}S REMAINING", LogW / 2f, LogH / 2f + 50, 18, new Color(0xFF, 0xD0, 0x40), centered: true);
    }

    void DrawLeaderboardScene()
    {
        // Scrolling parallax background using outback palette regardless of theme — looks more cinematic for idle.
        DrawLeaderboardSky();
        DrawLeaderboardSkyline();
        DrawLeaderboardGround();

        // Translucent vignette panel behind text for legibility
        DrawRect(0, 0, LogW, LogH, new Color(0x05, 0x05, 0x08) * 0.55f);
        DrawRect(LogW * 0.08f, 70, LogW * 0.84f, LogH - 130, new Color(0x10, 0x06, 0x02) * 0.55f);

        // Title pulse 0.7..1.0 over 2s
        float t = (MathF.Sin(_leaderboardTitlePulse * MathF.PI) + 1f) * 0.5f;
        float titleA = 0.7f + t * 0.3f;
        var amber = new Color(0xFF, 0xD0, 0x40);
        var cyan = new Color(0x00, 0xFF, 0xE5);

        DrawBigText("FLAPPY BRAIN", LogW / 2f, 100, 64, amber * titleA, centered: true, outline: true);
        DrawBigText("FOCUS TO FLY  -  BCI ARCADE", LogW / 2f, 170, 18, Color.White * 0.85f, centered: true, outline: true);

        // Separator
        float sepY = 210;
        DrawRect(LogW * 0.18f, sepY, LogW * 0.64f, 2, amber * 0.7f);
        DrawRect(LogW * 0.18f, sepY + 4, LogW * 0.64f, 1, amber * 0.3f);

        DrawBigText("TOP SCORES", LogW / 2f, 235, 28, cyan, centered: true, outline: true);

        // Score rows
        if (_sessionScores.Count == 0)
        {
            DrawBigText("BE THE FIRST TO PLAY!", LogW / 2f, 350, 32, Color.White * (0.7f + t * 0.3f), centered: true, outline: true);
        }
        else
        {
            int max = Math.Min(10, _sessionScores.Count);
            float rowY = 290;
            float rowH = 26;
            for (int i = 0; i < max; i++)
            {
                var entry = _sessionScores[i];
                float y = rowY + i * rowH;
                bool top = (i == 0);
                var rankCol = amber;
                var nameCol = top ? new Color(0xFF, 0xD7, 0x00) : Color.White;
                var dotCol = new Color(0x80, 0x60, 0x30);
                var scoreCol = top ? new Color(0xFF, 0xD7, 0x00) : cyan;

                string rank = $"{i + 1,2}.";
                string name = SanitizeName(entry.Name).ToUpperInvariant();
                if (name.Length > 18) name = name.Substring(0, 18);
                string scoreText = $"{entry.Score} PTS";

                // Rank
                float xLeft = LogW * 0.18f;
                DrawBigText(rank, xLeft, y, 18, rankCol, centered: false, outline: true);
                // Name
                DrawBigText(name, xLeft + 50, y, 18, nameCol, centered: false, outline: true);
                // Score (right-aligned approximation)
                float xRight = LogW * 0.82f;
                DrawBigText(scoreText, xRight - scoreText.Length * 19, y, 18, scoreCol, centered: false, outline: true);
                // Dots filling between name end and score start
                float dotsStart = xLeft + 50 + name.Length * 19 + 8;
                float dotsEnd = xRight - scoreText.Length * 19 - 8;
                for (float dx = dotsStart; dx < dotsEnd; dx += 12)
                {
                    DrawRect(dx, y + 16, 3, 3, dotCol);
                }
            }
        }

        // Bottom prompt
        string prompt = "PRESS SPACE TO START";
        float promptA = 0.65f + t * 0.35f;
        DrawBigText(prompt, LogW / 2f, LogH - 70, 26, cyan * promptA, centered: true, outline: true);
        DrawBigText("OR USE OPERATOR CONSOLE", LogW / 2f, LogH - 38, 14, Color.White * 0.5f, centered: true);
        if (_bciEnabled)
        {
            DrawBigText("T = RETRAIN HEADSET", LogW / 2f, LogH - 20, 13,
                new Color(0x40, 0xE0, 0xFF) * 0.65f, centered: true);
        }

        // ESC hint bottom-left
        DrawBigText("ESC = MAIN MENU", 16, LogH - 22, 13,
            Color.White * 0.5f, centered: false);
    }

    static string SanitizeName(string n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "PLAYER";
        var sb = new StringBuilder();
        foreach (var ch in n)
        {
            char up = char.ToUpperInvariant(ch);
            if ((up >= 'A' && up <= 'Z') || (up >= '0' && up <= '9') || up == ' ' || up == '.' || up == '-')
                sb.Append(up);
        }
        var s = sb.ToString().Trim();
        return s.Length == 0 ? "PLAYER" : s;
    }

    void DrawLeaderboardSky()
    {
        // Far gradient
        var c1 = _themePalette.SkyTop;
        var c2 = _themePalette.SkyMid;
        var c3 = _themePalette.SkyBot;
        int skyH = LogH - 70;
        for (int y = 0; y < skyH; y++)
        {
            float t = y / (float)skyH;
            Color c = t < 0.55f
                ? Color.Lerp(c1, c2, t / 0.55f)
                : Color.Lerp(c2, c3, (t - 0.55f) / 0.45f);
            DrawRect(0, y, LogW, 1, c);
        }
        // Drifting dust particles (slow parallax layer 1)
        var rng = new Random(101);
        for (int i = 0; i < 60; i++)
        {
            float baseX = rng.NextSingle() * LogW * 2f;
            float px = (baseX - _leaderboardScroll * (0.2f + rng.NextSingle() * 0.5f)) % (LogW * 2f);
            if (px < 0) px += LogW * 2f;
            if (px > LogW) continue;
            float py = 30 + rng.NextSingle() * 460;
            float sz = 1.5f + rng.NextSingle() * 2.5f;
            DrawRect(px, py, sz, sz, new Color(0xD4, 0x95, 0x6A) * 0.4f);
        }
    }

    void DrawLeaderboardSkyline()
    {
        // Mid-layer parallax: ruined city silhouette scrolling faster
        float offset = (_leaderboardScroll * 0.6f) % (LogW * 2f);
        var col = new Color(0x2A, 0x15, 0x08) * 0.92f;
        int[] widths  = { 40, 25, 55, 30, 70, 20, 45, 35, 50, 28 };
        int[] heights = { 180, 120, 220, 140, 160, 90, 200, 130, 175, 110 };
        int x = -200;
        for (int i = 0; i < widths.Length * 5; i++)
        {
            int idx = i % widths.Length;
            int bx = (int)(x - offset + LogW);
            if (bx > -80 && bx < LogW + 20)
            {
                int by = LogH - 70 - heights[idx];
                DrawRect(bx, by, widths[idx], heights[idx], col);
                // Broken antenna
                DrawRect(bx + widths[idx] - 8, by - 18, 4, 18, col);
            }
            x += widths[idx] + 12;
        }

        // Front layer: smaller ruins scrolling fastest
        float offset2 = (_leaderboardScroll * 1.4f) % (LogW * 2f);
        var col2 = new Color(0x1A, 0x09, 0x04);
        int[] w2 = { 28, 18, 36, 22, 48, 16, 32 };
        int[] h2 = { 80, 50, 100, 60, 70, 40, 90 };
        int x2 = -120;
        for (int i = 0; i < w2.Length * 7; i++)
        {
            int idx = i % w2.Length;
            int bx = (int)(x2 - offset2 + LogW);
            if (bx > -60 && bx < LogW + 20)
            {
                int by = LogH - 70 - h2[idx];
                DrawRect(bx, by, w2[idx], h2[idx], col2);
            }
            x2 += w2[idx] + 6;
        }
    }

    void DrawLeaderboardGround()
    {
        int groundY = LogH - 70;
        for (int y = 0; y < 70; y++)
        {
            float t = y / 70f;
            Color c = Color.Lerp(new Color(0x3A, 0x20, 0x10), new Color(0x12, 0x07, 0x03), t);
            DrawRect(0, groundY + y, LogW, 1, c);
        }
        // Front rubble scrolls fastest
        var rng = new Random(303);
        for (int i = 0; i < 36; i++)
        {
            float baseX = rng.NextSingle() * LogW * 2f;
            float rx = (baseX - _leaderboardScroll * 2.2f) % (LogW * 2f);
            if (rx < 0) rx += LogW * 2f;
            if (rx > LogW) continue;
            int rw = 4 + (int)(rng.NextSingle() * 14);
            int rh = 3 + (int)(rng.NextSingle() * 8);
            DrawRect(rx, groundY + 3, rw, rh, new Color(0x2A, 0x15, 0x08));
        }
    }

    void DrawSky(Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackSky(shake); return; }
        // Gradient bands
        for (int y = 0; y < 4; y++)
        {
            byte b = (byte)(0xE6 - y * 8);
            byte g = (byte)(0xC8 - y * 6);
            DrawRect(shake.X, shake.Y + y * (LogH / 4f), LogW, LogH / 4f + 2, new Color(0x6E + y * 4, g, b));
        }
        // Clouds
        DrawCloud(120 + shake.X, 80 + shake.Y);
        DrawCloud(450 + shake.X, 130 + shake.Y);
        DrawCloud(680 + shake.X, 60 + shake.Y);
    }

    void DrawCloud(float x, float y)
    {
        var c = Color.White * 0.85f;
        DrawCircle(x, y, 22, c);
        DrawCircle(x + 24, y - 8, 26, c);
        DrawCircle(x + 50, y, 22, c);
        DrawCircle(x + 30, y + 10, 24, c);
    }

    void DrawGround(Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackGround(shake); return; }
        DrawRect(shake.X, shake.Y + LogH - 60, LogW, 60, new Color(0x8B, 0x6F, 0x4E));
        DrawRect(shake.X, shake.Y + LogH - 60, LogW, 6, new Color(0x6E, 0xB7, 0x4E));
        // Grass tufts
        for (int i = 0; i < LogW; i += 16)
        {
            DrawRect(shake.X + i, shake.Y + LogH - 62, 3, 4, new Color(0x4E, 0x8B, 0x3A));
        }
    }

    void DrawPipe(Pipe p, Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackPipe(p, shake); return; }
        float topH = MathF.Max(0, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;
        float botH = MathF.Max(0, LogH - 60 - botY);

        Color body = new Color(0x4E, 0xA8, 0x4E);
        Color shade = new Color(0x36, 0x80, 0x36);
        Color light = new Color(0x80, 0xD8, 0x70);
        Color rim = new Color(0x28, 0x60, 0x28);

        // Top pipe body
        DrawRect(p.X + shake.X, shake.Y, PIPE_W, topH, body);
        // Corrugated stripes
        for (int i = 0; i < topH; i += 12)
        {
            DrawRect(p.X + shake.X + 6, shake.Y + i, 4, 8, light);
            DrawRect(p.X + shake.X + PIPE_W - 10, shake.Y + i, 4, 8, shade);
        }
        // Top cap
        if (topH > 0)
        {
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 18, PIPE_W + 8, 18, body);
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 18, PIPE_W + 8, 4, light);
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 4, PIPE_W + 8, 4, rim);
        }

        // Bottom pipe body
        DrawRect(p.X + shake.X, shake.Y + botY, PIPE_W, botH, body);
        for (int i = 0; i < botH; i += 12)
        {
            DrawRect(p.X + shake.X + 6, shake.Y + botY + i, 4, 8, light);
            DrawRect(p.X + shake.X + PIPE_W - 10, shake.Y + botY + i, 4, 8, shade);
        }
        // Bottom cap
        if (botH > 0)
        {
            DrawRect(p.X + shake.X - 4, shake.Y + botY, PIPE_W + 8, 18, body);
            DrawRect(p.X + shake.X - 4, shake.Y + botY, PIPE_W + 8, 4, light);
            DrawRect(p.X + shake.X - 4, shake.Y + botY + 14, PIPE_W + 8, 4, rim);
        }
    }

    void DrawBird(float x, float y, float rot)
    {
        if (_outbackTheme && _koalaTexture != null)
        {
            // Draw koala asset sprite
            float sprW = 160f, sprH = 130f;
            float r = MathHelper.Clamp(rot, -0.5f, 1.2f);
            float scaleX = sprW / _koalaTexture.Width;
            float scaleY = sprH / _koalaTexture.Height;
            // Draw centered at (x, y) — origin = texture center, position = bird center
            _spriteBatch.Draw(_koalaTexture,
                new Vector2(x, y),
                null,
                Color.White,
                r,
                new Vector2(_koalaTexture.Width / 2f, _koalaTexture.Height / 2f),
                new Vector2(scaleX, scaleY),
                SpriteEffects.None,
                0f);
            // Draw hitbox outline for debugging (thin red border)
            float hbW = OUTBACK_BIRD_W - 8f, hbH = OUTBACK_BIRD_H - 8f;
            var hitCol = new Color(255, 0, 0) * 0.6f;
            DrawRect(x - hbW/2, y - hbH/2, hbW, 2, hitCol);  // top
            DrawRect(x - hbW/2, y + hbH/2, hbW, 2, hitCol);  // bottom
            DrawRect(x - hbW/2, y - hbH/2, 2, hbH, hitCol);  // left
            DrawRect(x + hbW/2, y - hbH/2, 2, hbH, hitCol);  // right
            return;
        }
        // Body — golden
        Color body = new Color(0xFF, 0xD0, 0x40);
        Color belly = new Color(0xFF, 0xE8, 0x80);
        Color wing = new Color(0xE0, 0xA0, 0x20);
        Color beak = new Color(0xFF, 0x80, 0x20);
        Color eye = Color.Black;

        // approximate rotation by tilting via offsets (cheap)
        float tilt = rot * 6f;

        DrawRect(x - 16, y - 14 + tilt * 0.2f, 32, 28, body);
        DrawRect(x - 14, y - 4 + tilt * 0.2f, 28, 14, belly);
        // wing
        DrawRect(x - 10, y - 2, 14, 10, wing);
        // eye
        DrawRect(x + 4, y - 8, 6, 6, Color.White);
        DrawRect(x + 6, y - 7, 3, 3, eye);
        // beak
        DrawRect(x + 14, y - 2 + tilt * 0.3f, 10, 6, beak);
    }

    void DrawGoreParticle(GoreParticle g, Vector2 shake)
    {
        float alpha = 1f;
        float fadeStart = g.MaxLife * 0.7f;
        if (g.Life > fadeStart) alpha = 1f - (g.Life - fadeStart) / (g.MaxLife - fadeStart);
        Color c = g.Color * alpha;

        float x = g.X + shake.X;
        float y = g.Y + shake.Y;

        switch (g.Type)
        {
            case GoreType.BloodBlob:
                DrawCircle(x, y, g.Size, c);
                DrawCircle(x - g.Size * 0.3f, y - g.Size * 0.2f, g.Size * 0.7f, c);
                break;
            case GoreType.FurChunk:
                DrawCircle(x, y, g.Size * 0.6f, c);
                DrawCircle(x + g.Size * 0.4f, y - g.Size * 0.2f, g.Size * 0.5f, c);
                DrawCircle(x - g.Size * 0.3f, y + g.Size * 0.3f, g.Size * 0.5f, c);
                break;
            case GoreType.Feather:
                DrawRotRect(x, y, 5, 18, g.Rotation, c);
                break;
            case GoreType.Bone:
                DrawRotRect(x, y, 4, 12, g.Rotation, c);
                break;
            case GoreType.BloodSpray:
                // Trail
                for (int i = 0; i < g.Trail.Count; i++)
                {
                    float ta = alpha * (i + 1) / (float)(g.Trail.Count + 1) * 0.5f;
                    DrawCircle(g.Trail[i].X + shake.X, g.Trail[i].Y + shake.Y, g.Size * 0.7f, c * ta);
                }
                DrawCircle(x, y, g.Size, c);
                break;
        }
    }

    void DrawGoreOverlay()
    {
        if (_stateTimer >= 0.3f)
        {
            float fadeIn = MathHelper.Clamp((_stateTimer - 0.3f) / 0.4f, 0, 1);
            string txt = "💀 SPLAT!";
            DrawBigText(txt, LogW / 2f, LogH / 2f, 72, new Color(255, 32, 32) * fadeIn, centered: true, outline: true);
            DrawBigText($"SECTION {_currentSection + 1} FAILED", LogW / 2f, LogH / 2f + 60, 28, Color.White * fadeIn, centered: true);
        }
    }

    void DrawRetryOverlay()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.7f);
        DrawBigText("GAME OVER", LogW / 2f, LogH / 2f - 50, 56, new Color(0xFF, 0x40, 0x40), centered: true, outline: true);
        DrawBigText($"FINAL SCORE: {_totalScore}", LogW / 2f, LogH / 2f + 10, 28, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        float wait = MathF.Max(0, 5f - _gameOverHoldTimer);
        DrawBigText($"RETURN TO LEADERBOARD IN {wait:F0}S", LogW / 2f, LogH / 2f + 60, 18, Color.White * 0.8f, centered: true);
    }

    void DrawTransitionOverlay()
    {
        // Top countdown bar
        float prog = 1f - (_stateTimer / TRANSITION_DURATION);
        DrawRect(0, 0, LogW * prog, 6, new Color(0xFF, 0xD0, 0x40));

        DrawBigText($"✓ SECTION {_currentSection + 1} COMPLETE", LogW / 2f, LogH / 2f - 60, 52, new Color(0x40, 0xFF, 0x80), centered: true, outline: true);
        DrawBigText("+5 POINTS", LogW / 2f, LogH / 2f, 36, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        if (_currentSection + 1 >= TOTAL_SECTIONS)
        {
            DrawBigText("FINAL SECTION!", LogW / 2f, LogH / 2f + 50, 28, Color.White, centered: true);
        }
        else
        {
            DrawBigText($"Next: Section {_currentSection + 2}", LogW / 2f, LogH / 2f + 50, 24, Color.White, centered: true);
        }
    }

    void DrawVictoryOverlay()
    {
        DrawRect(0, 0, LogW, LogH, new Color(0x10, 0x10, 0x40) * 0.85f);

        // Confetti
        foreach (var c in _confetti)
        {
            DrawRotRect(c.X, c.Y, c.Size, c.Size * 0.6f, c.Rot, c.Color);
        }

        DrawBigText("\U0001F389 COMPLETE!", LogW / 2f, 140, 72, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        DrawBigText($"FINAL SCORE: {_totalScore}", LogW / 2f, 250, 48, Color.White, centered: true, outline: true);
        DrawBigText($"All {TOTAL_SECTIONS} sections cleared!", LogW / 2f, 320, 28, new Color(0x40, 0xFF, 0x80), centered: true);
        DrawBigText($"Best: {_bestScore}", LogW / 2f, 370, 24, new Color(200, 200, 200), centered: true);
        DrawBigText("Press SPACE to play again", LogW / 2f, 470, 24, Color.White, centered: true);
    }

    void DrawLaunchMenu()
    {
        // Theme-coloured sky gradient background
        var c1 = _themePalette.SkyTop;
        var c2 = _themePalette.SkyMid;
        var c3 = _themePalette.SkyBot;
        for (int y = 0; y < LogH; y++)
        {
            float t = y / (float)LogH;
            Color c = t < 0.55f
                ? Color.Lerp(c1, c2, t / 0.55f)
                : Color.Lerp(c2, c3, (t - 0.55f) / 0.45f);
            DrawRect(0, y, LogW, 1, c);
        }
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.45f);

        var amber = new Color(0xFF, 0xD0, 0x40);
        DrawBigText("FLAPPY BRAIN", LogW / 2f, 90, 64, amber, centered: true, outline: true);
        DrawBigText("CHOOSE LAUNCH MODE", LogW / 2f, 165, 22, Color.White * 0.85f, centered: true, outline: true);

        string[] labels =
        {
            "[1]  TRAIN BRAIN + PLAY",
            "[2]  PLAY WITH EXISTING TRAINING",
            "[3]  PLAY WITH SPACE BAR",
            "[4]  OUTBACK FLYING KOALA",
        };

        float baseY = 280;
        float rowH = 60;
        for (int i = 0; i < labels.Length; i++)
        {
            bool selected = (i == _launchMenuSelection);
            float y = baseY + i * rowH;
            Color col = selected ? Color.White : new Color(200, 200, 200);
            int size = selected ? 28 : 26;
            DrawBigText(labels[i], LogW / 2f, y, size, col, centered: true, outline: true);
        }

        DrawBigText("PRESS 1, 2, 3, OR 4 TO SELECT", LogW / 2f, LogH - 60, 16,
            Color.White * 0.6f, centered: true);
        DrawBigText("ESC = QUIT", LogW / 2f, LogH - 38, 13,
            Color.White * 0.45f, centered: true);
    }

    void DrawMenu()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.45f);
        DrawBigText("FLAPPY BRAIN V2", LogW / 2f, 140, 64, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        DrawBigText("20 SECTIONS OF PAIN", LogW / 2f, 210, 32, new Color(0xFF, 0x40, 0x40), centered: true, outline: true);
        DrawBigText("Survive each section. Die = retry.", LogW / 2f, 290, 22, Color.White, centered: true);
        DrawBigText("+1 per pipe  •  +5 per section", LogW / 2f, 325, 22, new Color(0x40, 0xFF, 0x80), centered: true);
        DrawBigText("Press SPACE / UP to start", LogW / 2f, 410, 28, Color.White, centered: true, outline: true);
        DrawBigText("R = restart  •  ESC = Main Menu", LogW / 2f, 460, 18, new Color(180, 180, 180), centered: true);
        if (_bestScore > 0)
        {
            DrawBigText($"Best: {_bestScore}", LogW / 2f, 510, 22, new Color(0xFF, 0xD0, 0x40), centered: true);
        }
    }

    void DrawHud()
    {
        // Left: section + score
        DrawBigText($"SECTION: {_currentSection + 1}/{TOTAL_SECTIONS}", 16, 14, 22, Color.White, centered: false, outline: true);
        DrawBigText($"SCORE: {_totalScore}", 16, 42, 28, new Color(0xFF, 0xD0, 0x40), centered: false, outline: true);

        // Section progress bar
        float secProg = MathHelper.Clamp(_sectionTimer / SECTION_DURATION, 0, 1);
        DrawRect(16, 80, 220, 8, new Color(40, 40, 40));
        DrawRect(16, 80, 220 * secProg, 8, new Color(0x40, 0xFF, 0x80));

        // Right: best
        DrawBigText($"BEST: {_bestScore}", LogW - 140, 14, 22, Color.White, centered: false, outline: true);

        // Section tick marks at top
        int tickW = 28;
        int gap = 4;
        int totalW = TOTAL_SECTIONS * tickW + (TOTAL_SECTIONS - 1) * gap;
        float startX = (LogW - totalW) / 2f;
        for (int s = 0; s < TOTAL_SECTIONS; s++)
        {
            Color c = s < _currentSection ? new Color(0xFF, 0xD0, 0x40) :
                      s == _currentSection ? Color.White :
                      new Color(60, 60, 60);
            DrawRect(startX + s * (tickW + gap), 6, tickW, 6, c);
        }

        if (_sectionAttempts > 1)
        {
            DrawBigText($"ATTEMPT {_sectionAttempts}", 16, 100, 18, new Color(255, 80, 80), centered: false, outline: true);
        }

        // ESC hint bottom-left
        DrawBigText("ESC = Main Menu", 16, LogH - 22, 13,
            Color.White * 0.5f, centered: false);
    }

    // ===== Primitive draw helpers =====

    void DrawRect(float x, float y, float w, float h, Color c)
    {
        if (w <= 0 || h <= 0) return;
        _spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)MathF.Ceiling(w), (int)MathF.Ceiling(h)), c);
    }

    void DrawCircle(float cx, float cy, float r, Color c)
    {
        if (r <= 0) return;
        int ir = (int)MathF.Ceiling(r);
        for (int dy = -ir; dy <= ir; dy++)
        {
            int dx = (int)MathF.Sqrt(r * r - dy * dy);
            DrawRect(cx - dx, cy + dy, dx * 2, 1, c);
        }
    }

    void DrawRotRect(float cx, float cy, float w, float h, float rot, Color c)
    {
        _spriteBatch.Draw(_pixel,
            position: new Vector2(cx, cy),
            sourceRectangle: null,
            color: c,
            rotation: rot,
            origin: new Vector2(0.5f, 0.5f),
            scale: new Vector2(w, h),
            effects: SpriteEffects.None,
            layerDepth: 0);
    }

    // Bitmap pixel font (5x7) — uppercase + digits + a few symbols
    static readonly Dictionary<char, string[]> Font = BuildFont();

    static Dictionary<char, string[]> BuildFont()
    {
        var f = new Dictionary<char, string[]>();
        void Add(char ch, params string[] rows) => f[ch] = rows;

        Add('A', "01110", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('B', "11110", "10001", "10001", "11110", "10001", "10001", "11110");
        Add('C', "01110", "10001", "10000", "10000", "10000", "10001", "01110");
        Add('D', "11110", "10001", "10001", "10001", "10001", "10001", "11110");
        Add('E', "11111", "10000", "10000", "11110", "10000", "10000", "11111");
        Add('F', "11111", "10000", "10000", "11110", "10000", "10000", "10000");
        Add('G', "01110", "10001", "10000", "10111", "10001", "10001", "01110");
        Add('H', "10001", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('I', "01110", "00100", "00100", "00100", "00100", "00100", "01110");
        Add('J', "00111", "00010", "00010", "00010", "00010", "10010", "01100");
        Add('K', "10001", "10010", "10100", "11000", "10100", "10010", "10001");
        Add('L', "10000", "10000", "10000", "10000", "10000", "10000", "11111");
        Add('M', "10001", "11011", "10101", "10001", "10001", "10001", "10001");
        Add('N', "10001", "11001", "10101", "10011", "10001", "10001", "10001");
        Add('O', "01110", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('P', "11110", "10001", "10001", "11110", "10000", "10000", "10000");
        Add('Q', "01110", "10001", "10001", "10001", "10101", "10010", "01101");
        Add('R', "11110", "10001", "10001", "11110", "10100", "10010", "10001");
        Add('S', "01111", "10000", "10000", "01110", "00001", "00001", "11110");
        Add('T', "11111", "00100", "00100", "00100", "00100", "00100", "00100");
        Add('U', "10001", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('V', "10001", "10001", "10001", "10001", "10001", "01010", "00100");
        Add('W', "10001", "10001", "10001", "10001", "10101", "11011", "10001");
        Add('X', "10001", "10001", "01010", "00100", "01010", "10001", "10001");
        Add('Y', "10001", "10001", "01010", "00100", "00100", "00100", "00100");
        Add('Z', "11111", "00001", "00010", "00100", "01000", "10000", "11111");
        Add('0', "01110", "10001", "10011", "10101", "11001", "10001", "01110");
        Add('1', "00100", "01100", "00100", "00100", "00100", "00100", "01110");
        Add('2', "01110", "10001", "00001", "00010", "00100", "01000", "11111");
        Add('3', "01110", "10001", "00001", "00110", "00001", "10001", "01110");
        Add('4', "00010", "00110", "01010", "10010", "11111", "00010", "00010");
        Add('5', "11111", "10000", "11110", "00001", "00001", "10001", "01110");
        Add('6', "00110", "01000", "10000", "11110", "10001", "10001", "01110");
        Add('7', "11111", "00001", "00010", "00100", "01000", "01000", "01000");
        Add('8', "01110", "10001", "10001", "01110", "10001", "10001", "01110");
        Add('9', "01110", "10001", "10001", "01111", "00001", "00010", "01100");
        Add(' ', "00000", "00000", "00000", "00000", "00000", "00000", "00000");
        Add(':', "00000", "00100", "00100", "00000", "00100", "00100", "00000");
        Add('!', "00100", "00100", "00100", "00100", "00100", "00000", "00100");
        Add('.', "00000", "00000", "00000", "00000", "00000", "00100", "00100");
        Add('/', "00001", "00010", "00010", "00100", "01000", "01000", "10000");
        Add('-', "00000", "00000", "00000", "11111", "00000", "00000", "00000");
        Add('+', "00000", "00100", "00100", "11111", "00100", "00100", "00000");
        Add('•', "00000", "00000", "01110", "01110", "01110", "00000", "00000");
        return f;
    }

    void DrawBigText(string text, float x, float y, int sizePx, Color c, bool centered = false, bool outline = false)
    {
        // map all chars to uppercase, unknown -> space
        text = text.ToUpperInvariant();
        // strip emoji-likes the font doesn't have, replace with placeholders
        var clean = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (Font.ContainsKey(ch))
            {
                clean.Append(ch);
            }
            else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                // skip emoji surrogate pair, replace with star
                clean.Append('*');
                i++;
            }
            else if (ch == '✓' || ch == '✔') // check marks
            {
                clean.Append('+');
            }
            else
            {
                clean.Append(' ');
            }
        }
        text = clean.ToString();

        // pixel cell size
        int pixSize = MathF.Max(1, sizePx / 8) > 0 ? (int)MathF.Max(1, sizePx / 8f) : 1;
        if (pixSize < 1) pixSize = 1;
        int charW = 5 * pixSize;
        int charH = 7 * pixSize;
        int charSpacing = pixSize;

        int totalW = text.Length * (charW + charSpacing) - charSpacing;
        float startX = centered ? x - totalW / 2f : x;
        float startY = centered ? y - charH / 2f : y;

        if (outline)
        {
            int o = MathF.Max(1, pixSize / 2) > 0 ? (int)MathF.Max(1, pixSize / 2f) : 1;
            for (int dx = -o; dx <= o; dx += o)
                for (int dy = -o; dy <= o; dy += o)
                    if (dx != 0 || dy != 0)
                        DrawTextRaw(text, startX + dx, startY + dy, pixSize, Color.Black);
        }
        DrawTextRaw(text, startX, startY, pixSize, c);
    }

    void DrawTextRaw(string text, float x, float y, int pixSize, Color c)
    {
        int charW = 5 * pixSize;
        int charH = 7 * pixSize;
        int charSpacing = pixSize;
        float cx = x;
        foreach (char ch in text)
        {
            if (!Font.TryGetValue(ch, out var rows))
            {
                cx += charW + charSpacing;
                continue;
            }
            for (int row = 0; row < 7; row++)
            {
                string r = rows[row];
                for (int col = 0; col < 5; col++)
                {
                    if (r[col] == '1')
                    {
                        DrawRect(cx + col * pixSize, y + row * pixSize, pixSize, pixSize, c);
                    }
                }
            }
            cx += charW + charSpacing;
        }
    }

    // ===== AI Autopilot =====
    int _aiDebugFrame = 0;


    // ===== Q-table state binning (must match FlappyBrain.Learning) =====
    const int QL_BIRD_Y_BINS   = 24;
    const int QL_BIRD_VEL_BINS = 18;
    const int QL_GAP_CTR_BINS  = 18;
    const int QL_DIST_BINS     = 12;
    const float QL_HITBOX_INSET = 4f;

    static int QlBinBirdY(float y)     => Math.Clamp((int)(y / 25f), 0, QL_BIRD_Y_BINS - 1);
    static int QlBinBirdVel(float v)   => Math.Clamp((int)((v + 7f) / 1.0f), 0, QL_BIRD_VEL_BINS - 1);
    static int QlBinGapCenter(float g) => Math.Clamp((int)(g / 33f), 0, QL_GAP_CTR_BINS - 1);
    static int QlBinDist(float d)      => Math.Clamp((int)(d / 67f), 0, QL_DIST_BINS - 1);

    static int QlStateIndex(float y, float vel, float gapC, float dist) =>
        QlBinBirdY(y) * (QL_BIRD_VEL_BINS * QL_GAP_CTR_BINS * QL_DIST_BINS) +
        QlBinBirdVel(vel) * (QL_GAP_CTR_BINS * QL_DIST_BINS) +
        QlBinGapCenter(gapC) * QL_DIST_BINS +
        QlBinDist(dist);

    bool AiShouldFlapLearned()
    {
        // Find pipe overlapping bird (preferred), or next ahead
        float bW2 = _outbackTheme ? OUTBACK_BIRD_W : BIRD_WIDTH;
        float birdLeft = _birdX - bW2 / 2f + QL_HITBOX_INSET;
        float birdRight = _birdX + bW2 / 2f - QL_HITBOX_INSET;

        Pipe? inside = null;
        foreach (var p in _pipes)
        {
            float pL = p.X, pR = p.X + PIPE_W;
            if (pR > birdLeft && pL < birdRight)
            {
                if (inside == null || p.X < inside.X) inside = p;
            }
        }

        Pipe? next = null;
        if (inside == null)
        {
            float bestX = float.MaxValue;
            foreach (var p in _pipes)
            {
                if (p.X >= birdRight && p.X < bestX)
                {
                    next = p;
                    bestX = p.X;
                }
            }
        }

        var primary = inside ?? next;
        float gapC = primary?.GapY ?? (LogH / 2f);
        float dist = primary != null ? MathF.Max(0f, primary.X - _birdX) : LogW;

        int state = QlStateIndex(_birdY, _birdVel, gapC, dist);
        return _qTable![state, 1] > _qTable[state, 0];
    }

    bool AiShouldFlap()
    {
        if (_learnedMode && _qTable != null) return AiShouldFlapLearned();

        _aiDebugFrame++;

        // Find nearest upcoming pipe
        Pipe? next = null;
        float minDist = float.MaxValue;
        foreach (var p in _pipes)
        {
            if (p.X + PIPE_W > _birdX)
            {
                float d = p.X - _birdX;
                if (d < minDist) { minDist = d; next = p; }
            }
        }

        float gapCenter = next != null ? next.GapY : LogH / 2f;
        float gapH      = next != null ? next.GapH : 160f;
        float gapTop    = gapCenter - gapH / 2f;
        float gapBottom = gapCenter + gapH / 2f;

        // Safe flap zone: bird must be in lower gapH*0.28 portion so peak stays in gap
        // With flap=-8.5 and gravity=0.4: peak rise ≈ 8.5*8.5/(2*0.4) ≈ 90px
        // So flap when birdY >= gapCenter + (90 - gapH/2 + margin)
        float minFlapY  = gapCenter + Math.Max(20f, 90f - gapH / 2f + 15f);

        // Debug every 60 frames
        if (_aiDebugFrame % 60 == 0)
            System.Console.WriteLine(
                $"AI #{_aiDebugFrame} birdY={_birdY:F0} vel={_birdVel:F1} " +
                $"gapC={gapCenter:F0} gapH={gapH:F0} minFlapY={minFlapY:F0} " +
                $"pipes={_pipes.Count} sec={_currentSection} score={_totalScore}");

        // RULE 1: Never flap if above the minimum flap Y (stay above center naturally)
        if (_birdY < minFlapY) return false;

        // RULE 2: Flap when in the safe zone and not already rising fast
        if (_birdVel > -4f) return true;

        // RULE 3: Emergency — about to hit floor or ground
        if (_birdY > gapBottom - 10f) return true;
        if (_birdY > LogH - 60f) return true;

        return false;
    }


    // ===== OUTBACK THEME HELPERS =====

    void DrawOutbackSky(Vector2 shake)
    {
        // Post-apocalyptic: draw BG asset first, then burnt-orange sky gradient overlay
        if (_bgTexture != null)
        {
            _spriteBatch.Draw(_bgTexture,
                new Rectangle(0, 0, LogW, LogH),
                Color.White);
        }

        // Burnt-orange gradient overlay at 65% opacity: #1A0F08 → #6B3020 → #C4622D
        var c1 = _themePalette.SkyTop;
        var c2 = _themePalette.SkyMid;
        var c3 = _themePalette.SkyBot;
        int skyH = LogH - 70;
        for (int y = 0; y < skyH; y++)
        {
            float t = y / (float)skyH;
            Color c = t < 0.55f
                ? Color.Lerp(c1, c2, t / 0.55f)
                : Color.Lerp(c2, c3, (t - 0.55f) / 0.45f);
            DrawRect(0, y + shake.Y, LogW, 1, c * 0.65f);
        }

        // Speed lines: 15 horizontal streaks from left edge
        var rngSL = new Random((int)(_bgScroll / 30f));
        for (int i = 0; i < 15; i++)
        {
            float py = rngSL.NextSingle() * LogH;
            float len = 60 + rngSL.NextSingle() * 120;
            float alpha = 0.20f + rngSL.NextSingle() * 0.25f;
            DrawRect(shake.X, py + shake.Y, len, 1, _themePalette.SpeedLine * alpha);
        }

        // Dust particles drifting left
        var rng2 = new Random(42);
        for (int i = 0; i < 80; i++)
        {
            float px = ((rng2.NextSingle() * LogW * 2 - _bgScroll * (0.5f + rng2.NextSingle())) % (LogW * 2));
            if (px < 0) px += LogW * 2;
            if (px > LogW) continue;
            float py = 30 + rng2.NextSingle() * 500;
            float sz = 2 + rng2.NextSingle() * 2;
            DrawRect(px + shake.X, py + shake.Y, (int)sz, (int)sz, _themePalette.DustColor * 0.35f);
        }

        // Ruined city silhouette (far background parallax)
        DrawRuinedSkyline(shake);
    }

    void DrawRuinedSkyline(Vector2 shake)
    {
        float offset = (_bgScroll * 0.08f) % (LogW * 2);
        var col = _themePalette.RuinCol * 0.9f;
        int[] widths  = { 40, 25, 55, 30, 70, 20, 45, 35 };
        int[] heights = { 180, 120, 220, 140, 160, 90, 200, 130 };
        int x = 0;
        for (int i = 0; i < widths.Length * 2; i++)
        {
            int idx = i % widths.Length;
            int bx = (int)(x - offset + LogW);
            if (bx > -80 && bx < LogW + 20)
            {
                int by = LogH - 70 - heights[idx];
                DrawRect(bx + shake.X, by + shake.Y, widths[idx], heights[idx], col);
                // Broken top edge
                DrawRect(bx + shake.X + widths[idx] - 8, by + shake.Y - 20, 8, 20, col);
            }
            x += widths[idx] + 10;
        }
    }

    void DrawGumTrees(float parallax, Color col, int baseY)
    {
        float offset = (_bgScroll * parallax) % (LogW * 2);
        int[] xs = { 0, 220, 450, 670, 900, 1120 };
        foreach (int tx in xs)
        {
            int x = (int)(tx - offset);
            if (x > -120 && x < LogW + 60)
                DrawSingleGumTree(x, baseY, col);
        }
    }

    void DrawSingleGumTree(int x, int baseY, Color col)
    {
        // Trunk
        DrawRect(x, baseY - 80, 8, 80, col);
        // Leaf clusters
        DrawCircle(x + 4, baseY - 95, 28, col);
        DrawCircle(x - 20, baseY - 80, 22, col);
        DrawCircle(x + 25, baseY - 78, 25, col);
    }

    void DrawOutbackGround(Vector2 shake)
    {
        // Post-apoc: dark ash and rubble
        int groundY = LogH - 70;
        for (int y = 0; y < 70; y++)
        {
            float t = y / 70f;
            Color c = Color.Lerp(_themePalette.GroundTop, _themePalette.GroundBot, t);
            DrawRect(0, groundY + y + shake.Y, LogW, 1, c);
        }
        // Rubble chunks
        var rng3 = new Random(77);
        for (int i = 0; i < 20; i++)
        {
            int rx = (int)((rng3.NextSingle() * LogW * 2 - _bgScroll * 0.3f) % (LogW * 2));
            if (rx > LogW || rx < -30) continue;
            int rw = 4 + (int)(rng3.NextSingle() * 14);
            int rh = 3 + (int)(rng3.NextSingle() * 8);
            DrawRect(rx + shake.X, groundY + shake.Y + 3, rw, rh, _themePalette.RuinCol);
        }
    }

    void DrawOutbackPipe(Pipe p, Vector2 shake)
    {
        float topH = MathF.Max(0, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;
        float botH = MathF.Max(0, LogH - 70 - botY);

        // Themed corrugated pipe
        var body   = _themePalette.PipeBody;
        var stripe = _themePalette.PipeStripe;
        var cap    = _themePalette.PipeCap;

        // Top pipe
        DrawRect(p.X + shake.X, shake.Y, PIPE_W, topH, body);
        for (float i = 0; i < topH; i += 12)
            DrawRect(p.X + shake.X, shake.Y + i, PIPE_W, 2, stripe);
        if (topH > 0)
            DrawRect(p.X + shake.X - 5, shake.Y + topH - 20, PIPE_W + 10, 20, cap);

        // Bottom pipe
        DrawRect(p.X + shake.X, shake.Y + botY, PIPE_W, botH, body);
        for (float i = 0; i < botH; i += 12)
            DrawRect(p.X + shake.X, shake.Y + botY + i, PIPE_W, 2, stripe);
        if (botH > 0)
            DrawRect(p.X + shake.X - 5, shake.Y + botY, PIPE_W + 10, 20, cap);
    }


}