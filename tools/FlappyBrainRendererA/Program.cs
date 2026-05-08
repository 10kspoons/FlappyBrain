using SkiaSharp;

namespace FlappyBrainRendererA;

internal static class Program
{
    private const int W = 800;
    private const int H = 600;
    private const int Fps = 30;
    private const int TotalFrames = 1080;

    private const string SpritePath = "/tmp/flappybrain-assets/image-1---5a35ceca-1725-41f2-ba19-a412b80e94b0.png";
    private const string BackgroundPath = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";
    private const string OutDir = "/tmp/flappybrain_frames_a";

    // Physics tuned for visible play
    private const float Gravity = 0.42f;
    private const float FlapImpulse = -6.0f;
    private const float MaxFallSpeed = 8f;

    // Visual ground (where pipes/bird die)
    private const float GroundY = H * 0.73f; // 438

    private const float PipeWidth = 90f;
    private const float PipeGap = 200f;
    private const float PipeSpeed = 3.0f;
    private const float PipeSpacing = 240f;

    public static SKBitmap SpriteBitmap = null!;
    public static SKBitmap BgBitmap = null!;

    private static readonly Random Rng = new(1337);

    public static void Main()
    {
        Directory.CreateDirectory(OutDir);
        SpriteBitmap = SKBitmap.Decode(SpritePath) ?? throw new InvalidOperationException("Failed to load sprite");
        BgBitmap = SKBitmap.Decode(BackgroundPath) ?? throw new InvalidOperationException("Failed to load background");

        Console.WriteLine($"Loaded sprite {SpriteBitmap.Width}x{SpriteBitmap.Height}, bg {BgBitmap.Width}x{BgBitmap.Height}");

        var sim = new GameSim();

        // Sequence:
        // 0..60 (2s)  : menu (frame 0..59)
        // 60..  : auto-start, play
        for (int frame = 0; frame < TotalFrames; frame++)
        {
            sim.Step(frame);
            using var surface = SKSurface.Create(new SKImageInfo(W, H));
            var canvas = surface.Canvas;
            sim.Render(canvas, frame);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            var path = Path.Combine(OutDir, $"frame_{frame:D5}.png");
            using var fs = File.OpenWrite(path);
            data.SaveTo(fs);
            if (frame % 60 == 0)
                Console.WriteLine($"Rendered frame {frame}/{TotalFrames} state={sim.State} score={sim.Score} y={sim.BirdY:F1}");
        }

        Console.WriteLine("Done.");
    }
}

internal enum GameState
{
    Menu,
    Playing,
    Dying,
    GameOver,
}

internal struct Pipe
{
    public float X;
    public float GapY;
    public bool Scored;
}

internal struct DustParticle
{
    public float X, Y, Speed, Size;
    public byte Alpha;
}

internal sealed class GameSim
{
    private const int W = 800;
    private const int H = 600;
    private const float Gravity = 0.42f;
    private const float FlapImpulse = -6.0f;
    private const float MaxFallSpeed = 8f;
    private const float GroundY = H * 0.73f;
    private const float PipeWidth = 90f;
    private const float PipeGap = 200f;
    private const float PipeSpeed = 3.0f;
    private const float PipeSpacing = 240f;

    public GameState State { get; private set; } = GameState.Menu;
    public float BirdX = 220f;
    public float BirdY = H * 0.45f;
    private float _vy;
    private float _bgScroll;
    private int _pipeSpawnTimer;
    public int Score { get; private set; }
    private int _stateTimer;
    private float _tilt;
    private int _flapAnim;
    private int _dbgFrame;
    private int _runIndex;

    private readonly List<Pipe> _pipes = new();
    private readonly List<DustParticle> _dust = new();
    private readonly Random _rng = new(42);

    public GameSim()
    {
        for (int i = 0; i < 70; i++)
            _dust.Add(NewDust(true));
    }

    private DustParticle NewDust(bool initial)
    {
        return new DustParticle
        {
            X = initial ? _rng.Next(0, W) : W + 10,
            Y = _rng.Next(60, (int)GroundY),
            Speed = 0.5f + (float)_rng.NextDouble() * 1.8f,
            Size = 1 + (float)_rng.NextDouble() * 2.5f,
            Alpha = (byte)_rng.Next(60, 180),
        };
    }

    public void Step(int frame)
    {
        _stateTimer++;
        _bgScroll += 0.5f * 0.6f; // background parallax

        // Update dust
        for (int i = 0; i < _dust.Count; i++)
        {
            var d = _dust[i];
            d.X -= d.Speed;
            if (d.X < -5) d = NewDust(false);
            _dust[i] = d;
        }

        switch (State)
        {
            case GameState.Menu:
                BirdY = H * 0.45f + MathF.Sin(frame * 0.12f) * 8f;
                _tilt = MathF.Sin(frame * 0.12f) * 5f;
                if (_stateTimer >= 60)
                {
                    State = GameState.Playing;
                    _stateTimer = 0;
                    BirdY = H * 0.45f;
                    _vy = -2f;
                    // Pre-position so first pipe arrives sooner
                    _pipeSpawnTimer = (int)(PipeSpacing / PipeSpeed) - 40;
                    Score = 0;
                    _pipes.Clear();
                }
                break;

            case GameState.Playing:
                StepPlay();
                break;

            case GameState.Dying:
                _vy += Gravity * 1.4f;
                if (_vy > MaxFallSpeed) _vy = MaxFallSpeed;
                BirdY += _vy;
                _tilt = Math.Min(90f, _tilt + 5f);
                if (BirdY >= GroundY - 35)
                {
                    BirdY = GroundY - 35;
                    State = GameState.GameOver;
                    _stateTimer = 0;
                }
                break;

            case GameState.GameOver:
                if (_stateTimer >= 75)
                {
                    // Restart
                    State = GameState.Menu;
                    _stateTimer = 0;
                    BirdY = H * 0.45f;
                    _vy = 0;
                    Score = 0;
                    _pipes.Clear();
                    _tilt = 0;
                    _runIndex++;
                }
                break;
        }
    }

    private void StepPlay()
    {
        // Auto-pilot: predict 15 ticks ahead
        Pipe? nextPipe = null;
        foreach (var p in _pipes)
        {
            if (p.X + PipeWidth > BirdX - 5)
            {
                if (nextPipe == null || p.X < nextPipe.Value.X)
                    nextPipe = p;
            }
        }
        // When no pipe: aim mid-screen. When pipe present: aim slightly above gap center.
        float targetY = nextPipe?.GapY - 15 ?? GroundY * 0.55f;

        // Predict ahead until apex (vy = 0) or 25 ticks
        float predY = BirdY;
        float predV = _vy;
        for (int t = 0; t < 25; t++)
        {
            predV += Gravity;
            if (predV > MaxFallSpeed) predV = MaxFallSpeed;
            predY += predV;
        }

        bool wantFlap = false;
        // Only flap when bird is currently below target AND will overshoot below
        if (BirdY > targetY && _vy >= 0 && predY > targetY + 30) wantFlap = true;
        // Or if bird is far below target regardless
        if (BirdY > targetY + 50 && _vy >= -2f) wantFlap = true;
        // Hard floor protection
        if (BirdY > GroundY - 80) wantFlap = true;
        // Hard ceiling
        if (BirdY < 70) wantFlap = false;
        // Don't flap if already moving up fast
        if (_vy < -3f) wantFlap = false;

        // After a good run, force a death so we get game-over + restart
        if (_runIndex == 0 && Score >= 7)
            wantFlap = false;

        if (wantFlap)
        {
            _vy = FlapImpulse;
            _flapAnim = 6;
        }

        _dbgFrame++;

        if (_flapAnim > 0) _flapAnim--;

        _vy += Gravity;
        if (_vy > MaxFallSpeed) _vy = MaxFallSpeed;
        BirdY += _vy;

        // Tilt
        float targetTilt = _vy < 0 ? -25f : Math.Min(70f, _vy * 6f);
        _tilt += (targetTilt - _tilt) * 0.18f;

        // Spawn pipes
        _pipeSpawnTimer++;
        if (_pipeSpawnTimer * PipeSpeed >= PipeSpacing)
        {
            _pipeSpawnTimer = 0;
            float gapY = _rng.Next(150, (int)(GroundY - 150));
            _pipes.Add(new Pipe { X = W + 50, GapY = gapY, Scored = false });
        }

        // Move pipes, score
        for (int i = 0; i < _pipes.Count; i++)
        {
            var p = _pipes[i];
            p.X -= PipeSpeed;
            if (!p.Scored && p.X + PipeWidth < BirdX)
            {
                p.Scored = true;
                Score++;
            }
            _pipes[i] = p;
        }
        _pipes.RemoveAll(p => p.X < -PipeWidth - 50);

        // Collisions
        const float birdR = 22f;
        if (BirdY > GroundY - birdR)
        {
            BirdY = GroundY - birdR;
            Die();
            return;
        }
        if (BirdY < birdR)
        {
            BirdY = birdR;
            _vy = 0;
        }

        foreach (var p in _pipes)
        {
            if (BirdX + birdR * 0.5f > p.X && BirdX - birdR * 0.5f < p.X + PipeWidth)
            {
                if (BirdY - birdR * 0.5f < p.GapY - PipeGap / 2 || BirdY + birdR * 0.5f > p.GapY + PipeGap / 2)
                {
                    Die();
                    return;
                }
            }
        }
    }

    private void Die()
    {
        State = GameState.Dying;
        _stateTimer = 0;
        _vy = -3f;
    }

    public void Render(SKCanvas canvas, int frame)
    {
        // Sky gradient
        using (var skyPaint = new SKPaint())
        {
            skyPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, H),
                new[] { new SKColor(0x4A, 0x2A, 0x1A), new SKColor(0xC4, 0x62, 0x2D) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, W, H, skyPaint);
        }

        // Background image (post-apoc outback) — lower half, scrolling at 0.5x
        DrawBackground(canvas);

        // Pipes
        DrawPipes(canvas);

        // Ground line
        using (var groundPaint = new SKPaint { Color = new SKColor(0x3A, 0x1F, 0x12) })
            canvas.DrawRect(0, GroundY, W, H - GroundY, groundPaint);
        using (var groundLine = new SKPaint { Color = new SKColor(0x6A, 0x3A, 0x22), Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
            canvas.DrawLine(0, GroundY, W, GroundY, groundLine);

        // Dust particles
        DrawDust(canvas);

        // Bird
        DrawBird(canvas);

        // Score
        if (State == GameState.Playing || State == GameState.Dying || State == GameState.GameOver)
            DrawScore(canvas, Score);

        // Menu / Game over overlays
        if (State == GameState.Menu)
            DrawMenu(canvas, frame);
        else if (State == GameState.GameOver)
            DrawGameOver(canvas);
    }

    private void DrawBackground(SKCanvas canvas)
    {
        // Use only lower portion of background (skip white sky in source image).
        // Source image is 1536x1024 - take y from 280 down (where the buildings start).
        const int bgY = 250;
        const int bgH = 250;
        int srcW = Program.BgBitmap.Width;
        int srcH = Program.BgBitmap.Height;
        var src = new SKRect(0, srcH * 0.27f, srcW, srcH);
        float scroll = _bgScroll % W;
        var dest1 = new SKRect(-scroll, bgY, -scroll + W, bgY + bgH);
        var dest2 = new SKRect(-scroll + W, bgY, -scroll + W * 2, bgY + bgH);

        // Multiply blend to drop the white pixels onto the sky color
        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.Medium,
            BlendMode = SKBlendMode.Multiply,
        };
        canvas.DrawBitmap(Program.BgBitmap, src, dest1, paint);
        canvas.DrawBitmap(Program.BgBitmap, src, dest2, paint);
    }

    private void DrawPipes(SKCanvas canvas)
    {
        var bodyColor = new SKColor(0x8B, 0x5E, 0x3C);
        var capColor = new SKColor(0x6A, 0x42, 0x25);
        var darkColor = new SKColor(0x4A, 0x2C, 0x18);
        const float capH = 24f;

        using var bodyP = new SKPaint { Color = bodyColor };
        using var capP = new SKPaint { Color = capColor };
        using var darkP = new SKPaint { Color = darkColor };
        using var rivetP = new SKPaint { Color = new SKColor(0x33, 0x1F, 0x10) };

        foreach (var p in _pipes)
        {
            float topH = p.GapY - PipeGap / 2;
            float botY = p.GapY + PipeGap / 2;

            // Top pipe
            canvas.DrawRect(p.X, 0, PipeWidth, topH, bodyP);
            canvas.DrawRect(p.X + PipeWidth - 8, 0, 8, topH, darkP);
            canvas.DrawRect(p.X - 4, topH - capH, PipeWidth + 8, capH, capP);
            canvas.DrawRect(p.X - 4, topH - 4, PipeWidth + 8, 4, darkP);

            // Bot pipe
            canvas.DrawRect(p.X, botY, PipeWidth, GroundY - botY, bodyP);
            canvas.DrawRect(p.X + PipeWidth - 8, botY, 8, GroundY - botY, darkP);
            canvas.DrawRect(p.X - 4, botY, PipeWidth + 8, capH, capP);
            canvas.DrawRect(p.X - 4, botY + capH, PipeWidth + 8, 4, darkP);

            // Rivets
            for (int r = 0; r < 4; r++)
            {
                canvas.DrawCircle(p.X + 12, 30 + r * 40, 2.5f, rivetP);
                canvas.DrawCircle(p.X + PipeWidth - 12, 30 + r * 40, 2.5f, rivetP);
            }
        }
    }

    private void DrawDust(SKCanvas canvas)
    {
        using var paint = new SKPaint { Color = new SKColor(0xE8, 0xC8, 0x9A) };
        foreach (var d in _dust)
        {
            paint.Color = new SKColor(0xE8, 0xC8, 0x9A, d.Alpha);
            canvas.DrawCircle(d.X, d.Y, d.Size, paint);
        }
    }

    private void DrawBird(SKCanvas canvas)
    {
        const float drawW = 90f;
        const float drawH = 70f;
        canvas.Save();
        canvas.Translate(BirdX, BirdY);
        canvas.RotateDegrees(_tilt);
        var dest = new SKRect(-drawW / 2, -drawH / 2, drawW / 2, drawH / 2);
        using var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawBitmap(Program.SpriteBitmap, dest, p);

        // Subtle flap glow
        if (_flapAnim > 0)
        {
            using var glow = new SKPaint
            {
                Color = new SKColor(0xFF, 0xE0, 0x80, (byte)(_flapAnim * 25)),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true,
            };
            canvas.DrawOval(0, 0, drawW / 2 + 4, drawH / 2 + 4, glow);
        }

        canvas.Restore();
    }

    // 5x7 pixel font for digits 0-9 and some letters
    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
        ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
        ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01110" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
        ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
        ['Y'] = new[] { "10001", "10001", "10001", "01010", "00100", "00100", "00100" },
        [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
        ['!'] = new[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" },
    };

    private static void DrawPixelText(SKCanvas canvas, string text, float x, float y, int pixSize, SKColor color, SKColor? shadow = null)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = false };
        text = text.ToUpperInvariant();
        float cx = x;
        if (shadow.HasValue)
        {
            using var sp = new SKPaint { Color = shadow.Value, IsAntialias = false };
            float sx = x + pixSize;
            foreach (var ch in text)
            {
                if (!Font.TryGetValue(ch, out var glyph)) { sx += pixSize * 6; continue; }
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                        if (glyph[row][col] == '1')
                            canvas.DrawRect(sx + col * pixSize, y + row * pixSize + pixSize, pixSize, pixSize, sp);
                sx += pixSize * 6;
            }
        }

        foreach (var ch in text)
        {
            if (!Font.TryGetValue(ch, out var glyph)) { cx += pixSize * 6; continue; }
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                    if (glyph[row][col] == '1')
                        canvas.DrawRect(cx + col * pixSize, y + row * pixSize, pixSize, pixSize, paint);
            cx += pixSize * 6;
        }
    }

    private static float MeasureText(string text, int pixSize) => text.Length * pixSize * 6;

    private void DrawScore(SKCanvas canvas, int score)
    {
        var s = score.ToString();
        const int pix = 5;
        float w = MeasureText(s, pix);
        float x = (W - w) / 2f;
        DrawPixelText(canvas, s, x, 30, pix, new SKColor(0xFF, 0xE6, 0x6B), new SKColor(0x33, 0x1A, 0x08));
    }

    private void DrawMenu(SKCanvas canvas, int frame)
    {
        // Title
        const string title = "FLAPPYBRAIN";
        const int titlePix = 6;
        float tw = MeasureText(title, titlePix);
        DrawPixelText(canvas, title, (W - tw) / 2f, 90, titlePix, new SKColor(0xFF, 0xD8, 0x4A), new SKColor(0x33, 0x1A, 0x08));

        const string sub = "THINK TO FLAP";
        const int subPix = 3;
        float sw = MeasureText(sub, subPix);
        if ((frame / 15) % 2 == 0)
            DrawPixelText(canvas, sub, (W - sw) / 2f, 170, subPix, new SKColor(0xFF, 0x88, 0x33), new SKColor(0x33, 0x1A, 0x08));
    }

    private void DrawGameOver(SKCanvas canvas)
    {
        // Translucent panel
        using (var dim = new SKPaint { Color = new SKColor(0, 0, 0, 130) })
            canvas.DrawRect(0, 0, W, H, dim);
        const string go = "GAME OVER";
        const int gpix = 6;
        float gw = MeasureText(go, gpix);
        DrawPixelText(canvas, go, (W - gw) / 2f, 200, gpix, new SKColor(0xFF, 0x55, 0x33), new SKColor(0x33, 0x0A, 0x05));

        var sc = "SCORE " + Score;
        const int spix = 3;
        float sw = MeasureText(sc, spix);
        DrawPixelText(canvas, sc, (W - sw) / 2f, 290, spix, new SKColor(0xFF, 0xE6, 0x6B), new SKColor(0x33, 0x1A, 0x08));
    }
}
