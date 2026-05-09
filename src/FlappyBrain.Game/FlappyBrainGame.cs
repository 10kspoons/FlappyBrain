using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace FlappyBrain;

// ─── Game State ───────────────────────────────────────────────────────────────

public enum GameState { Menu, Playing, Dead }

// ─── Bird ─────────────────────────────────────────────────────────────────────

public class Bird
{
    public const float Gravity       = 0.40f;
    public const float FlapImpulse   = -8.5f;
    public const float TerminalVel   = 14f;
    public const float Width         = 40f;
    public const float Height        = 36f;

    public Vector2 Position;
    public float   Velocity;
    public float   Rotation;

    public RectangleF Hitbox => new(Position.X - Width / 2 + 4,
                                    Position.Y - Height / 2 + 4,
                                    Width - 8, Height - 8);

    public void Reset(float x, float y) { Position = new(x, y); Velocity = 0; Rotation = 0; }

    public void Flap()  { Velocity = FlapImpulse; }

    public void Update(float dt)
    {
        Velocity = Math.Min(Velocity + Gravity, TerminalVel);
        Position.Y += Velocity;
        Rotation = MathHelper.Clamp(Velocity * 0.07f, -0.5f, 1.2f);
    }
}

// ─── Pipe Pair ────────────────────────────────────────────────────────────────

public class PipePair
{
    public const float Width    = 90f;
    public const float CapHeight= 24f;

    public float X;
    public float GapCenterY;
    public float GapHeight;
    public bool  Scored;

    public RectangleF TopRect(float screenH)
    {
        float h = GapCenterY - GapHeight / 2f;
        return new(X, 0, Width, MathF.Max(0, h));
    }
    public RectangleF BottomRect(float screenH)
    {
        float botY = GapCenterY + GapHeight / 2f;
        float h = screenH - botY;
        return new(X, botY, Width, MathF.Max(0, h));
    }

    public void Update(float speed, float dt) { X -= speed * dt; }
}

// ─── Particle ─────────────────────────────────────────────────────────────────

public class Particle
{
    public Vector2 Pos, Vel;
    public float   Life, MaxLife, Alpha, Size;
}

// ─── Parallax Layer ───────────────────────────────────────────────────────────

public class ParallaxLayer
{
    public Color   Color;
    public float   ScrollMult;
    public float   Height;   // fraction of screen height
    public float   YOffset;  // fraction of screen height from top
}

// ─── Main Game ────────────────────────────────────────────────────────────────

public class FlappyBrainGame : Game
{
    // --- Graphics
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch?      _sb;
    private Texture2D?        _pixel;
    // private SpriteFont?    _font; // reserved for Phase 3

    // Logical resolution
    private const int LogW = 800, LogH = 600;
    private RenderTarget2D?   _rt;

    // --- Game objects
    private Bird              _bird = new();
    private List<PipePair>    _pipes = new();
    private List<Particle>    _particles = new();
    private GameState         _state = GameState.Menu;
    private int               _score;
    private int               _bestScore;
    private float             _pipeTimer;
    private float             _flashAlpha;
    private float             _shakeTime;
    private Random            _rng = new();

    // --- Input
    private KeyboardState _prevKeys;

    // --- Timing
    private float _baseSpeed = 180f;
    private float PipeSpeed  => _baseSpeed + _score * 4f;
    private float PipeGap    => Math.Max(150f, 210f - _score * 2.5f);
    private float SpawnInt   => Math.Max(1.5f, 2.3f - _score * 0.015f);

    // --- Palette (post-apoc Australian outback)
    private static readonly Color SkyTop    = new(0x4A, 0x2A, 0x1A);   // deep ochre night
    private static readonly Color SkyBot    = new(0xC4, 0x62, 0x2D);   // burnt ochre
    private static readonly Color MidColor  = new(0x7A, 0x4A, 0x2E);   // distant rust
    private static readonly Color GroundTop = new(0xB5, 0x45, 0x1B);   // red dust
    private static readonly Color GroundBot = new(0x7A, 0x35, 0x20);   // dark earth
    private static readonly Color PipeBody  = new(0x8B, 0x5E, 0x3C);   // rusty metal
    private static readonly Color PipeCap   = new(0x6A, 0x42, 0x25);   // darker rust
    private static readonly Color BirdColor = new(0xE8, 0xC4, 0x40);   // dust gold
    private static readonly Color BirdDark  = new(0xB8, 0x85, 0x10);   // shadow gold
    private static readonly Color DustColor = new(0xD4, 0x95, 0x6A, 0x60); // dust haze

    // --- Parallax scroll offset
    private float _bgScroll;

    public FlappyBrainGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth  = LogW,
            PreferredBackBufferHeight = LogH,
            IsFullScreen              = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "FlappyBrain 🧠";
    }

    protected override void Initialize()
    {
        _rt = new RenderTarget2D(GraphicsDevice, LogW, LogH);
        SpawnDustBurst(80);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _sb    = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        // No font needed — score goes in window title for prototype
    }

    protected override void Update(GameTime gt)
    {
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        var keys = Keyboard.GetState();

        bool spaceJustDown = keys.IsKeyDown(Keys.Space)    && !_prevKeys.IsKeyDown(Keys.Space);
        bool upJustDown    = keys.IsKeyDown(Keys.Up)       && !_prevKeys.IsKeyDown(Keys.Up);
        bool escJustDown   = keys.IsKeyDown(Keys.Escape)   && !_prevKeys.IsKeyDown(Keys.Escape);
        bool flap          = spaceJustDown || upJustDown;

        if (escJustDown) { Exit(); return; }

        switch (_state)
        {
            case GameState.Menu:
                if (flap) StartGame();
                break;

            case GameState.Playing:
                UpdatePlaying(dt, flap);
                break;

            case GameState.Dead:
                _shakeTime = Math.Max(0, _shakeTime - dt);
                _flashAlpha = Math.Max(0, _flashAlpha - dt * 1.5f);
                if (flap) StartGame();
                break;
        }

        // Background scroll always runs
        _bgScroll += 60f * dt;

        UpdateParticles(dt);
        _prevKeys = keys;

        Window.Title = _state == GameState.Playing
            ? $"FlappyBrain 🧠 — Score: {_score}  |  Best: {_bestScore}  |  [Space/↑] Flap  [Esc] Quit"
            : $"FlappyBrain 🧠 — Best: {_bestScore}  |  [Space/↑] {(_state == GameState.Menu ? "Start" : "Restart")}  |  [Esc] Quit";

        base.Update(gt);
    }

    private void StartGame()
    {
        _bird.Reset(200, LogH / 2f);
        _pipes.Clear();
        _score     = 0;
        _pipeTimer = 0;
        _flashAlpha= 0;
        _shakeTime = 0;
        _state     = GameState.Playing;
        SpawnDustBurst(30);
    }

    private void UpdatePlaying(float dt, bool flap)
    {
        if (flap) { _bird.Flap(); SpawnDustBurst(5); }
        _bird.Update(dt);

        // Spawn pipes
        _pipeTimer += dt;
        if (_pipeTimer >= SpawnInt)
        {
            _pipeTimer = 0;
            float gapY = _rng.NextSingle() * (LogH - 200f) + 100f;
            _pipes.Add(new PipePair { X = LogW + 10, GapCenterY = gapY, GapHeight = PipeGap });
        }

        // Update pipes, check score
        foreach (var p in _pipes)
        {
            p.Update(PipeSpeed, dt);
            if (!p.Scored && p.X + PipePair.Width < _bird.Position.X)
            {
                p.Scored = true;
                _score++;
                if (_score > _bestScore) _bestScore = _score;
                SpawnDustBurst(12);
            }
        }
        _pipes.RemoveAll(p => p.X + PipePair.Width < -10);

        // Collision
        if (_bird.Position.Y < 0 || _bird.Position.Y > LogH)
        {
            KillBird(); return;
        }
        foreach (var p in _pipes)
        {
            if (Collision.HitsTopPipe(_bird.Position.X, _bird.Position.Y, Bird.Width, Bird.Height,
                                       p.X, PipePair.Width, p.GapCenterY, p.GapHeight) ||
                Collision.HitsBottomPipe(_bird.Position.X, _bird.Position.Y, Bird.Width, Bird.Height,
                                          p.X, PipePair.Width, p.GapCenterY, p.GapHeight, LogH))
            {
                KillBird(); return;
            }
        }
    }

    private void KillBird()
    {
        _state = GameState.Dead;
        _flashAlpha = 1f;
        _shakeTime  = 0.35f;
        SpawnDustBurst(50);
    }

    // ── Particles ─────────────────────────────────────────────────────────────

    private void SpawnDustBurst(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var angle = (float)(_rng.NextDouble() * Math.PI * 2);
            var speed = _rng.NextSingle() * 80 + 20;
            _particles.Add(new Particle
            {
                Pos     = _bird.Position + new Vector2(_rng.NextSingle() * 40 - 20, _rng.NextSingle() * 20 - 10),
                Vel     = new Vector2(MathF.Cos(angle) * speed - 30, MathF.Sin(angle) * speed),
                Life    = 0,
                MaxLife = _rng.NextSingle() * 1.2f + 0.4f,
                Alpha   = 1f,
                Size    = _rng.NextSingle() * 6 + 2
            });
        }
    }

    private void UpdateParticles(float dt)
    {
        // Ambient dust drift (spawn a few each frame)
        if (_rng.NextSingle() < 0.4f)
        {
            _particles.Add(new Particle
            {
                Pos     = new Vector2(LogW + 10, _rng.NextSingle() * LogH),
                Vel     = new Vector2(-(_rng.NextSingle() * 40 + 20), _rng.NextSingle() * 10 - 5),
                MaxLife = _rng.NextSingle() * 3f + 2f,
                Alpha   = _rng.NextSingle() * 0.5f + 0.1f,
                Size    = _rng.NextSingle() * 4 + 1
            });
        }

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life += dt;
            p.Pos  += p.Vel * dt;
            p.Vel.X -= 15f * dt; // slow drift
            float t = p.Life / p.MaxLife;
            p.Alpha = (1 - t) * (i < 40 ? 0.6f : 0.35f);
            if (p.Life >= p.MaxLife) _particles.RemoveAt(i);
        }

        if (_particles.Count > 300) _particles.RemoveRange(0, _particles.Count - 300);
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    protected override void Draw(GameTime gt)
    {
        // Render to RenderTarget at fixed logical res
        GraphicsDevice.SetRenderTarget(_rt);
        GraphicsDevice.Clear(SkyTop);

        _sb!.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        DrawBackground();
        DrawParticles();

        if (_state != GameState.Menu)
        {
            DrawPipes();
            DrawBird();
        }
        else
        {
            DrawMenuBird();
        }

        DrawHUD();
        DrawOverlay();

        _sb.End();

        // Blit RT to backbuffer (letterboxed)
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var (destRect, _) = GetLetterboxRect();
        _sb.Begin();
        _sb.Draw(_rt!, destRect, Color.White);
        _sb.End();

        base.Draw(gt);
    }

    private void DrawBackground()
    {
        // Sky gradient (top half)
        for (int y = 0; y < LogH * 0.55f; y++)
        {
            float t = y / (LogH * 0.55f);
            var c = Color.Lerp(SkyTop, SkyBot, t);
            DrawRect(0, y, LogW, 1, c);
        }

        // Mid distant haze band — parallax 0.2x
        float mid1 = (_bgScroll * 0.2f) % LogW;
        DrawRect((int)(-mid1), (int)(LogH * 0.30f), LogW * 2, (int)(LogH * 0.18f),
            new Color(0x8B, 0x45, 0x1B, 0x80));

        // Mid ruins band — parallax 0.35x  
        float mid2 = (_bgScroll * 0.35f) % LogW;
        DrawRect((int)(-mid2), (int)(LogH * 0.40f), LogW * 2, (int)(LogH * 0.12f),
            new Color(0x7A, 0x4A, 0x2E, 0xC0));

        // Ground — parallax 0.6x
        float gx = (_bgScroll * 0.6f) % 40;
        for (int x = -(int)gx; x < LogW + 40; x += 40)
        {
            // ground stripe
        }
        DrawRect(0, (int)(LogH * 0.50f), LogW, (int)(LogH * 0.10f),
            new Color(GroundTop.R, GroundTop.G, GroundTop.B, (byte)0xD0));
        DrawRect(0, (int)(LogH * 0.58f), LogW, LogH, GroundBot);

        // Distant rock silhouettes (parallax 0.15x)
        float rx = (_bgScroll * 0.15f) % LogW;
        DrawSilhouetteMountains((int)(-rx));
        DrawSilhouetteMountains((int)(-rx) + LogW);

        // Dead gum trees (parallax 0.25x)
        float tx = (_bgScroll * 0.25f) % LogW;
        DrawDeadTrees((int)(-tx));
        DrawDeadTrees((int)(-tx) + LogW);

        // Corrugated iron shacks (parallax 0.4x)
        float sx = (_bgScroll * 0.4f) % LogW;
        DrawRuins((int)(-sx));
        DrawRuins((int)(-sx) + LogW);
    }

    private void DrawSilhouetteMountains(int offsetX)
    {
        var c = new Color(0x5C, 0x30, 0x1A, 0x90);
        int[] heights = { 80, 55, 70, 45, 90, 60, 50, 75 };
        int x = offsetX + 30;
        foreach (var h in heights)
        {
            // Simple triangle-ish rocks as stacked rects
            for (int i = 0; i < h; i += 4)
            {
                int w = (h - i) * 3 / h + 2;
                DrawRect(x - w / 2, (int)(LogH * 0.46f) - h + i, w + 2, 4, c);
            }
            x += 90 + (h % 3) * 20;
        }
    }

    private void DrawDeadTrees(int offsetX)
    {
        var trunk = new Color(0x4A, 0x30, 0x1A, 0xB0);
        var branch= new Color(0x5A, 0x38, 0x20, 0x90);
        int[] xs = { 80, 210, 380, 550, 700 };
        foreach (var bx in xs)
        {
            int tx = offsetX + bx;
            int baseY = (int)(LogH * 0.50f);
            int trunkH = 55 + (bx % 20);
            DrawRect(tx, baseY - trunkH, 5, trunkH, trunk);
            // branches
            DrawRect(tx - 18, baseY - trunkH + 15, 18, 3, branch);
            DrawRect(tx + 5,  baseY - trunkH + 25, 20, 3, branch);
            DrawRect(tx - 12, baseY - trunkH + 5,  12, 3, branch);
        }
    }

    private void DrawRuins(int offsetX)
    {
        var iron  = new Color(0x7A, 0x55, 0x3A, 0xC0);
        var dark  = new Color(0x50, 0x35, 0x1A, 0xFF);
        int[] xs  = { 100, 350, 600 };
        foreach (var bx in xs)
        {
            int rx = offsetX + bx;
            int by = (int)(LogH * 0.47f);
            // shack outline
            DrawRect(rx, by - 35, 55, 35, iron);
            DrawRect(rx + 5, by - 30, 12, 20, dark); // window
            DrawRect(rx + 28, by - 30, 10, 20, dark); // door
            // roof lean
            DrawRect(rx - 5, by - 42, 65, 10, dark);
        }
    }

    private void DrawParticles()
    {
        foreach (var p in _particles)
        {
            var c = DustColor * p.Alpha;
            DrawRect((int)p.Pos.X, (int)p.Pos.Y, (int)p.Size, (int)p.Size, c);
        }
    }

    private void DrawPipes()
    {
        foreach (var pipe in _pipes)
        {
            float topH   = pipe.GapCenterY - pipe.GapHeight / 2;
            float botY   = pipe.GapCenterY + pipe.GapHeight / 2;
            float botH   = LogH - botY;

            // Top pipe body
            DrawRect((int)pipe.X, 0, (int)PipePair.Width, (int)topH, PipeBody);
            // Top pipe cap
            DrawRect((int)pipe.X - 5, (int)(topH - PipePair.CapHeight),
                     (int)PipePair.Width + 10, (int)PipePair.CapHeight, PipeCap);
            // Rust stripe
            DrawRect((int)pipe.X + 10, 0, 8, (int)(topH - PipePair.CapHeight),
                     new Color(0x6A, 0x35, 0x15, 0x80));

            // Bottom pipe body
            DrawRect((int)pipe.X, (int)botY, (int)PipePair.Width, (int)botH, PipeBody);
            // Bottom pipe cap
            DrawRect((int)pipe.X - 5, (int)botY,
                     (int)PipePair.Width + 10, (int)PipePair.CapHeight, PipeCap);
            DrawRect((int)pipe.X + 10, (int)(botY + PipePair.CapHeight), 8, (int)botH,
                     new Color(0x6A, 0x35, 0x15, 0x80));
        }
    }

    private void DrawBird()
    {
        var pos = _bird.Position;
        if (_shakeTime > 0)
            pos += new Vector2(_rng.NextSingle() * 6 - 3, _rng.NextSingle() * 6 - 3);

        int bx = (int)(pos.X - Bird.Width / 2);
        int by = (int)(pos.Y - Bird.Height / 2);
        int bw = (int)Bird.Width;
        int bh = (int)Bird.Height;

        // Body
        DrawRect(bx, by + 6, bw, bh - 12, BirdColor);
        // Top/bottom taper
        DrawRect(bx + 4, by + 2, bw - 8, 6, BirdColor);
        DrawRect(bx + 4, by + bh - 8, bw - 8, 6, BirdColor);
        // Shadow underside
        DrawRect(bx + 2, by + bh / 2, bw - 4, bh / 2 - 6, BirdDark);
        // Eye
        DrawRect(bx + bw - 14, by + 8, 8, 8, Color.White);
        DrawRect(bx + bw - 11, by + 10, 5, 5, Color.Black);
        // Beak
        DrawRect(bx + bw, by + bh / 2 - 3, 10, 6, new Color(0xE8, 0x80, 0x20));
        // Wing flap hint
        float wingY = _bird.Velocity < 0 ? -4 : 4;
        DrawRect(bx + 6, by + bh / 2 - 2 + (int)wingY, bw - 16, 8,
                 new Color(BirdDark.R, BirdDark.G, BirdDark.B, (byte)0xC0));

        // Goggle (BCI headset hint)
        DrawRect(bx + bw - 20, by + 4, 14, 10, new Color(0x30, 0x30, 0x30, 0xD0));
        DrawRect(bx + bw - 18, by + 6, 10, 6, new Color(0x50, 0x90, 0xC0, 0x90));
    }

    private void DrawMenuBird()
    {
        // Bobbing idle
        float bobY = MathF.Sin((float)Environment.TickCount64 / 300f) * 8f;
        _bird.Position = new Vector2(LogW / 2f - 40, LogH / 2f - 30 + bobY);
        DrawBird();
    }

    private void DrawHUD()
    {
        // Score display — large pixel-art style rectangles for digits
        if (_state == GameState.Playing || _state == GameState.Dead)
        {
            DrawBigScore(_score);
        }

        // BCI dot (top-right) — grey (keyboard only mode)
        DrawRect(LogW - 28, 12, 16, 16, new Color(0x70, 0x70, 0x70, 0xFF));
        DrawRect(LogW - 25, 15, 10, 10, new Color(0x55, 0x55, 0x55, 0xFF));
    }

    private void DrawOverlay()
    {
        switch (_state)
        {
            case GameState.Menu:
                // Title banner
                DrawRect(LogW / 2 - 180, 60, 360, 90, new Color(0, 0, 0, 0xB0));
                DrawRect(LogW / 2 - 176, 64, 352, 82, new Color(0x8B, 0x45, 0x1B, 0xFF));
                // "FLAPPYBRAIN" — just a coloured bar placeholder (no font)
                DrawRect(LogW / 2 - 150, 80, 300, 24, new Color(0xE8, 0xC4, 0x40));
                DrawRect(LogW / 2 - 90,  110, 180, 14, new Color(0xD4, 0x95, 0x6A));

                // Prompt
                DrawRect(LogW / 2 - 130, LogH / 2 + 60, 260, 40, new Color(0, 0, 0, 0x90));
                DrawRect(LogW / 2 - 126, LogH / 2 + 64, 252, 32, new Color(0xB5, 0x45, 0x1B, 0xE0));
                break;

            case GameState.Dead:
                // Flash
                if (_flashAlpha > 0)
                    DrawRect(0, 0, LogW, LogH, Color.White * _flashAlpha * 0.4f);
                // Game over panel
                DrawRect(LogW / 2 - 160, LogH / 2 - 80, 320, 160, new Color(0, 0, 0, 0xB0));
                DrawRect(LogW / 2 - 156, LogH / 2 - 76, 312, 152, new Color(0x8B, 0x45, 0x1B, 0xFF));
                DrawRect(LogW / 2 - 140, LogH / 2 - 60, 280, 24, new Color(0xE8, 0x50, 0x20));
                DrawBigScore(_score, LogW / 2, LogH / 2 - 20);
                // Restart prompt
                DrawRect(LogW / 2 - 100, LogH / 2 + 50, 200, 24, new Color(0xE8, 0xC4, 0x40, 0xD0));
                break;
        }
    }

    // ── Pixel digit rendering ─────────────────────────────────────────────────

    // Each digit is 5×7 pixels, scaled up
    private static readonly bool[,,] Digits = BuildDigits();
    private static bool[,,] BuildDigits()
    {
        // 5-wide × 7-tall pixel patterns for 0-9
        string[] patterns = {
            "01110 10001 10001 10001 10001 10001 01110",
            "00100 01100 00100 00100 00100 00100 01110",
            "01110 10001 00001 00110 01000 10000 11111",
            "01110 10001 00001 00110 00001 10001 01110",
            "00010 00110 01010 10010 11111 00010 00010",
            "11111 10000 10000 11110 00001 10001 01110",
            "01110 10000 10000 11110 10001 10001 01110",
            "11111 00001 00010 00100 01000 01000 01000",
            "01110 10001 10001 01110 10001 10001 01110",
            "01110 10001 10001 01111 00001 00001 01110",
        };
        var d = new bool[10, 7, 5];
        for (int n = 0; n < 10; n++)
        {
            var rows = patterns[n].Split(' ');
            for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                    d[n, r, c] = rows[r][c] == '1';
        }
        return d;
    }

    private void DrawDigit(int digit, int x, int y, int scale, Color color)
    {
        for (int r = 0; r < 7; r++)
            for (int c = 0; c < 5; c++)
                if (Digits[digit, r, c])
                    DrawRect(x + c * scale, y + r * scale, scale, scale, color);
    }

    private void DrawBigScore(int score, int? cx = null, int? cy = null)
    {
        string s = score.ToString();
        int scale = 5;
        int digitW = 5 * scale + 2;
        int totalW = s.Length * digitW;
        int startX = cx.HasValue ? cx.Value - totalW / 2 : LogW / 2 - totalW / 2;
        int startY = cy ?? 16;

        // Shadow
        for (int i = 0; i < s.Length; i++)
            DrawDigit(s[i] - '0', startX + i * digitW + 2, startY + 2, scale, Color.Black * 0.5f);
        // Main
        for (int i = 0; i < s.Length; i++)
            DrawDigit(s[i] - '0', startX + i * digitW, startY, scale, new Color(0xF2, 0xD5, 0xA0));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void DrawRect(int x, int y, int w, int h, Color c)
    {
        if (w <= 0 || h <= 0) return;
        _sb!.Draw(_pixel!, new Rectangle(x, y, w, h), c);
    }

    private (Rectangle dest, float scale) GetLetterboxRect()
    {
        var vp = GraphicsDevice.Viewport;
        float scaleX = (float)vp.Width  / LogW;
        float scaleY = (float)vp.Height / LogH;
        float scale  = Math.Min(scaleX, scaleY);
        int dw = (int)(LogW * scale);
        int dh = (int)(LogH * scale);
        return (new Rectangle((vp.Width - dw) / 2, (vp.Height - dh) / 2, dw, dh), scale);
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _rt?.Dispose();
        _sb?.Dispose();
    }
}

// RectangleF moved to RectangleF.cs
