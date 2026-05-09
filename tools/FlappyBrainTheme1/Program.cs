using SkiaSharp;

const int W = 800;
const int H = 600;
const int FPS = 30;
const int TOTAL_FRAMES = 900;
const string OUT_DIR = "/tmp/fb-t1-frames";
const string BIRD_PATH = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";

Directory.CreateDirectory(OUT_DIR);

// Load bird
SKBitmap birdBitmap = SKBitmap.Decode(BIRD_PATH) ?? throw new Exception("Bird image not loaded");

var rand = new Random(42);

// Particles
var particles = new List<(float x, float y, float speed, float size, float opacity)>();
for (int i = 0; i < 80; i++)
{
    particles.Add((
        (float)(rand.NextDouble() * W),
        (float)(rand.NextDouble() * H),
        (float)(0.5 + rand.NextDouble() * 2.0),
        (float)(2 + rand.NextDouble() * 2),
        (float)(0.2 + rand.NextDouble() * 0.2)
    ));
}

// Far gum trees (parallax 0.1)
var farTrees = new List<(float x, float height)>();
for (int i = 0; i < 5; i++)
    farTrees.Add(((float)(i * 200 + rand.NextDouble() * 80), (float)(150 + rand.NextDouble() * 150)));

// Mid gum trees (parallax 0.2)
var midTrees = new List<(float x, float height)>();
for (int i = 0; i < 3; i++)
    midTrees.Add(((float)(i * 280 + 100 + rand.NextDouble() * 60), (float)(200 + rand.NextDouble() * 100)));

// Emus
var emus = new List<(float x, float y)>();
for (int i = 0; i < 2; i++)
    emus.Add(((float)(i * 400 + 150), 460f + (float)(rand.NextDouble() * 20)));

// Pipe definitions for gameplay
var pipes = new List<(float x, float gapY, float gapH, bool scored)>();
float pipeSpawnX = W + 100;
const float PIPE_WIDTH = 80f;
const float PIPE_GAP = 180f;
const float PIPE_SPACING = 280f;
for (int i = 0; i < 10; i++)
{
    float gapY = 180f + (float)(rand.NextDouble() * 180);
    pipes.Add((pipeSpawnX + i * PIPE_SPACING, gapY, PIPE_GAP, false));
}

// Bird state
float birdX = 200f;
float birdY = 300f;
float birdVy = 0f;
const float GRAVITY = 0.45f;
const float FLAP_VY = -7.5f;
int score = 0;

var flapFrames = new HashSet<int> { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };
var glowFrames = new Dictionary<int, int>(); // frame -> opacity countdown

int? gameOverFrame = null;

for (int frame = 0; frame < TOTAL_FRAMES; frame++)
{
    using var surface = SKSurface.Create(new SKImageInfo(W, H));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Black);

    // === SKY GRADIENT ===
    using (var paint = new SKPaint())
    {
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, H - 80),
            new[] { new SKColor(0x1C, 0x3A, 0x6E), new SKColor(0x5B, 0x9B, 0xD5), new SKColor(0xF0, 0xA8, 0x32) },
            new float[] { 0f, 0.55f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, W, H - 80, paint);
    }

    // === FAR HILLS ===
    using (var paint = new SKPaint { Color = new SKColor(0x8B, 0x30, 0x10), IsAntialias = true })
    {
        var hillPath = new SKPath();
        hillPath.MoveTo(0, H - 80);
        for (int x = 0; x <= W; x += 20)
        {
            float scrollX = x + frame * 0.1f;
            float y = H - 80 - (float)(40 + 25 * Math.Sin(scrollX * 0.005) + 15 * Math.Sin(scrollX * 0.013));
            hillPath.LineTo(x, y);
        }
        hillPath.LineTo(W, H - 80);
        hillPath.Close();
        canvas.DrawPath(hillPath, paint);
    }

    // === FAR GUM TREES (0.1x parallax) ===
    foreach (var tree in farTrees)
    {
        float tx = ((tree.x - frame * 0.1f) % (W + 200) + (W + 200)) % (W + 200) - 100;
        DrawGumTree(canvas, tx, H - 80, tree.height, 0.7f);
    }

    // === EMUS (mid-ground) ===
    foreach (var emu in emus)
    {
        float ex = ((emu.x - frame * 0.15f) % (W + 200) + (W + 200)) % (W + 200) - 100;
        DrawEmu(canvas, ex, emu.y);
    }

    // === MID GUM TREES (0.2x parallax) ===
    foreach (var tree in midTrees)
    {
        float tx = ((tree.x - frame * 0.2f) % (W + 200) + (W + 200)) % (W + 200) - 100;
        DrawGumTree(canvas, tx, H - 80, tree.height, 1.0f);
    }

    // === RED DUST PARTICLES ===
    using (var paint = new SKPaint { IsAntialias = true })
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            float nx = p.x - p.speed;
            if (nx < -10) nx = W + 10;
            particles[i] = (nx, p.y, p.speed, p.size, p.opacity);
            paint.Color = new SKColor(0xD4, 0x95, 0x6A, (byte)(p.opacity * 255));
            canvas.DrawCircle(nx, p.y, p.size, paint);
        }
    }

    // === GROUND (red dirt) ===
    using (var paint = new SKPaint())
    {
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, H - 80),
            new SKPoint(0, H),
            new[] { new SKColor(0xC2, 0x50, 0x1F), new SKColor(0x8B, 0x30, 0x10) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(0, H - 80, W, 80, paint);
    }
    // ground texture lines
    using (var paint = new SKPaint { Color = new SKColor(0x8B, 0x30, 0x10), StrokeWidth = 1 })
    {
        for (int i = 0; i < 30; i++)
        {
            float gx = ((i * 40 - frame * 3) % (W + 40) + (W + 40)) % (W + 40);
            float gy = H - 70 + (i % 3) * 20;
            canvas.DrawLine(gx, gy, gx + 15, gy, paint);
        }
    }

    // === GAMEPLAY: title (0-59), gameplay (60-839), game over (840-899) ===
    bool inGameplay = frame >= 60 && frame < 840;
    bool inTitle = frame < 60;
    bool inGameOver = frame >= 840;

    if (inGameplay || inGameOver)
    {
        // Update bird physics during gameplay
        if (inGameplay)
        {
            if (flapFrames.Contains(frame))
            {
                birdVy = FLAP_VY;
                glowFrames[frame] = 10;
            }
            birdVy += GRAVITY;
            birdY += birdVy;
            if (birdY > H - 110) { birdY = H - 110; birdVy = 0; }
            if (birdY < 60) { birdY = 60; birdVy = 0; }

            // Update pipes
            for (int i = 0; i < pipes.Count; i++)
            {
                var p = pipes[i];
                float newX = p.x - 3.5f;
                bool scored = p.scored;
                if (!scored && newX + PIPE_WIDTH / 2 < birdX)
                {
                    scored = true;
                    score++;
                }
                pipes[i] = (newX, p.gapY, p.gapH, scored);
            }
            // Recycle pipes
            for (int i = 0; i < pipes.Count; i++)
            {
                if (pipes[i].x < -PIPE_WIDTH)
                {
                    float maxX = pipes.Max(pp => pp.x);
                    float gapY = 180f + (float)(rand.NextDouble() * 180);
                    pipes[i] = (maxX + PIPE_SPACING, gapY, PIPE_GAP, false);
                }
            }
        }

        // Draw pipes
        foreach (var p in pipes)
        {
            DrawPipe(canvas, p.x, 0, p.gapY, true); // top
            DrawPipe(canvas, p.x, p.gapY + p.gapH, H - 80 - (p.gapY + p.gapH), false); // bottom
        }

        // Glow if recently flapped
        int glowOpacity = 0;
        foreach (var kv in glowFrames.ToList())
        {
            if (frame - kv.Key < 10)
            {
                int remaining = 10 - (frame - kv.Key);
                if (remaining > glowOpacity) glowOpacity = remaining;
            }
            else glowFrames.Remove(kv.Key);
        }
        if (glowOpacity > 0)
        {
            using var paint = new SKPaint { IsAntialias = true };
            paint.Color = new SKColor(0xF0, 0xA8, 0x32, (byte)(glowOpacity * 25 * 0.3));
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 15);
            canvas.DrawCircle(birdX, birdY, 80, paint);
        }

        // Draw bird with rotation
        float rotation = Math.Clamp(birdVy * 4f, -25f, 70f);
        canvas.Save();
        canvas.Translate(birdX, birdY);
        canvas.RotateDegrees(rotation);
        var dest = new SKRect(-80, -65, 80, 65);
        canvas.DrawBitmap(birdBitmap, dest);
        canvas.Restore();

        // Score HUD
        DrawScoreHUD(canvas, score);
    }

    // === VIGNETTE ===
    DrawVignette(canvas, W, H);

    // === TITLE CARD ===
    if (inTitle)
    {
        using (var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(0.6 * 255)) })
            canvas.DrawRect(0, 0, W, H, paint);

        using var titlePaint = new SKPaint
        {
            Color = new SKColor(0xF0, 0xA8, 0x32),
            TextSize = 64,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
        };
        canvas.DrawText("OUTBACK CLASSIC", W / 2f, H / 2f - 10, titlePaint);

        using var subPaint = new SKPaint
        {
            Color = new SKColor(0xF2, 0xD5, 0xA0),
            TextSize = 32,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("FlappyBrain 🧠", W / 2f, H / 2f + 40, subPaint);
    }

    // === GAME OVER ===
    if (inGameOver)
    {
        float fade = Math.Min(1f, (frame - 840) / 20f);
        using (var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(fade * 0.7 * 255)) })
            canvas.DrawRect(0, 0, W, H, paint);

        if (fade > 0.3f)
        {
            using var goPaint = new SKPaint
            {
                Color = new SKColor(0xE0, 0x40, 0x30),
                TextSize = 80,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
            };
            canvas.DrawText("GAME OVER", W / 2f, H / 2f - 10, goPaint);

            using var scorePaint = new SKPaint
            {
                Color = new SKColor(0xF2, 0xD5, 0xA0),
                TextSize = 36,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText($"FINAL SCORE: {score}", W / 2f, H / 2f + 50, scorePaint);
        }
    }

    // === SAVE ===
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    string path = Path.Combine(OUT_DIR, $"frame_{frame:D4}.png");
    using var fs = File.OpenWrite(path);
    data.SaveTo(fs);

    if (frame % 60 == 0) Console.WriteLine($"Frame {frame}/{TOTAL_FRAMES}");
}

Console.WriteLine($"Rendered {TOTAL_FRAMES} frames to {OUT_DIR}");

// === HELPERS ===

static void DrawGumTree(SKCanvas canvas, float baseX, float baseY, float height, float scale)
{
    using var trunkPaint = new SKPaint { Color = new SKColor(0x4A, 0x30, 0x10), IsAntialias = true, StrokeWidth = 8 * scale, StrokeCap = SKStrokeCap.Round };
    // S-curve trunk via bezier path stroke
    using var trunk = new SKPath();
    trunk.MoveTo(baseX, baseY);
    trunk.CubicTo(
        baseX - 4 * scale, baseY - height * 0.35f,
        baseX + 6 * scale, baseY - height * 0.7f,
        baseX, baseY - height);
    canvas.DrawPath(trunk, trunkPaint);

    // Leaf clusters
    using var leafPaint = new SKPaint { Color = new SKColor(0x1A, 0x2A, 0x0A), IsAntialias = true };
    var rng = new Random((int)(baseX * 7 + height));
    int clusters = 6;
    for (int i = 0; i < clusters; i++)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = (float)(rng.NextDouble() * 35 * scale);
        float cx = baseX + (float)Math.Cos(angle) * dist;
        float cy = baseY - height + (float)Math.Sin(angle) * dist * 0.6f - 10;
        float r = (float)(15 + rng.NextDouble() * 20) * scale;
        canvas.DrawOval(cx, cy, r, r * 0.85f, leafPaint);
    }
}

static void DrawEmu(SKCanvas canvas, float x, float y)
{
    using var paint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x0A), IsAntialias = true };
    // body
    canvas.DrawOval(x, y, 30, 20, paint);
    // neck
    using (var neckPaint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x0A), StrokeWidth = 4, IsAntialias = true })
        canvas.DrawLine(x + 18, y - 8, x + 26, y - 35, neckPaint);
    // head
    canvas.DrawCircle(x + 27, y - 38, 6, paint);
    // legs
    using (var legPaint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x0A), StrokeWidth = 2, IsAntialias = true })
    {
        canvas.DrawLine(x - 5, y + 15, x - 5, y + 35, legPaint);
        canvas.DrawLine(x + 8, y + 15, x + 8, y + 35, legPaint);
    }
}

static void DrawPipe(SKCanvas canvas, float x, float y, float height, bool isTop)
{
    if (height <= 0) return;
    float pipeW = 80f;

    // body
    using (var paint = new SKPaint { Color = new SKColor(0x8B, 0x5E, 0x3C), IsAntialias = true })
        canvas.DrawRect(x, y, pipeW, height, paint);

    // corrugation stripes
    using (var paint = new SKPaint { Color = new SKColor(0xA0, 0x70, 0x50), IsAntialias = false })
    {
        for (float sy = y + 3; sy < y + height; sy += 15)
            canvas.DrawRect(x, sy, pipeW, 2, paint);
    }

    // edge shadow
    using (var paint = new SKPaint { Color = new SKColor(0x5A, 0x3A, 0x20, 180) })
    {
        canvas.DrawRect(x + pipeW - 8, y, 8, height, paint);
        canvas.DrawRect(x, y, 4, height, new SKPaint { Color = new SKColor(0xB0, 0x80, 0x55, 120) });
    }

    // cap (at the gap-facing end)
    using (var paint = new SKPaint { Color = new SKColor(0x6A, 0x40, 0x20), IsAntialias = true })
    {
        float capY = isTop ? y + height - 20 : y;
        canvas.DrawRect(x - 10, capY, pipeW + 20, 20, paint);
    }
    using (var paint = new SKPaint { Color = new SKColor(0x4A, 0x28, 0x10) })
    {
        float capY = isTop ? y + height - 20 : y;
        canvas.DrawRect(x - 10, capY, pipeW + 20, 3, paint);
        canvas.DrawRect(x - 10, capY + 17, pipeW + 20, 3, paint);
    }
}

static void DrawScoreHUD(SKCanvas canvas, int score)
{
    string text = $"SCORE: {score}";
    using var shadow = new SKPaint
    {
        Color = SKColors.Black,
        TextSize = 28,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText(text, 22, 42, shadow);

    using var text2 = new SKPaint
    {
        Color = new SKColor(0xF2, 0xD5, 0xA0),
        TextSize = 28,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText(text, 20, 40, text2);
}

static void DrawVignette(SKCanvas canvas, int w, int h)
{
    using var paint = new SKPaint { IsAntialias = true };
    paint.Shader = SKShader.CreateRadialGradient(
        new SKPoint(w / 2f, h / 2f),
        Math.Max(w, h) * 0.7f,
        new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 130) },
        new float[] { 0.55f, 1f },
        SKShaderTileMode.Clamp);
    canvas.DrawRect(0, 0, w, h, paint);
}
