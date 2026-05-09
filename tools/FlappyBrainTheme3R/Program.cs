using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

const int W = 800, H = 600;
const int TOTAL_FRAMES = 900;
const string OUTPUT_DIR = "/tmp/fb-t3r-frames";
const string ASSET_PATH = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";

Directory.CreateDirectory(OUTPUT_DIR);

// Load koala asset
SKBitmap? koalaBmp = null;
if (File.Exists(ASSET_PATH))
{
    using var stream = File.OpenRead(ASSET_PATH);
    koalaBmp = SKBitmap.Decode(stream);
}

var rand = new Random(1337);

// Flap frames
var flapFrames = new HashSet<int> { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

// Pipe positions for parallax movement
var pipes = new List<Pipe>();
for (int i = 0; i < 8; i++)
{
    pipes.Add(new Pipe(900 + i * 220, 150 + rand.Next(200), 130));
}

// Steam particles (persistent)
var steamParticles = new List<Particle>();
var sparkParticles = new List<Particle>();

// Chimney smoke puffs
var smokePuffs = new List<SmokePuff>();
// Pre-seed
for (int i = 0; i < 12; i++)
{
    smokePuffs.Add(new SmokePuff(
        100 + rand.Next(600),
        420 - rand.Next(150),
        rand.Next(60)
    ));
}

// Bird state
double birdY = 300;
double birdVel = 0;
double birdX = 200;
const double GRAVITY = 0.45;
const double FLAP_VEL = -8.5;

for (int frame = 0; frame < TOTAL_FRAMES; frame++)
{
    using var surface = SKSurface.Create(new SKImageInfo(W, H));
    var canvas = surface.Canvas;

    DrawSky(canvas);
    DrawBackgroundCogs(canvas, frame);
    DrawCitySkyline(canvas, frame);
    DrawClockTower(canvas, frame);
    DrawChimneyStacks(canvas, frame);
    UpdateAndDrawSmoke(canvas, frame);
    DrawGround(canvas, frame);

    // Update bird
    bool isTitle = frame < 60;
    bool isGameOver = frame >= 840;
    bool isGameplay = !isTitle && !isGameOver;

    if (isGameplay)
    {
        if (flapFrames.Contains(frame)) birdVel = FLAP_VEL;
        birdVel += GRAVITY;
        birdY += birdVel;
        if (birdY < 50) { birdY = 50; birdVel = 0; }
        if (birdY > 520) { birdY = 520; birdVel = 0; }

        // Pipes scroll
        foreach (var p in pipes)
        {
            p.X -= 2.5;
            if (p.X < -100)
            {
                p.X += 8 * 220;
                p.GapY = 150 + rand.Next(200);
            }
        }
    }

    // Draw pipes
    if (!isTitle)
    {
        foreach (var p in pipes)
        {
            DrawPipe(canvas, p, frame);
        }
    }

    // Spark particles spawn near pipes occasionally
    if (isGameplay && frame % 6 == 0)
    {
        foreach (var p in pipes)
        {
            if (p.X > 0 && p.X < W && rand.NextDouble() < 0.3)
            {
                sparkParticles.Add(new Particle(
                    p.X + rand.Next(-10, 30),
                    p.GapY + rand.Next(-5, 5),
                    rand.NextDouble() * 4 - 2,
                    -rand.NextDouble() * 3 - 1,
                    20
                ));
            }
        }
    }

    // Steam particles spawn around bird and pipes
    if (frame % 3 == 0)
    {
        steamParticles.Add(new Particle(
            (float)birdX + rand.Next(-30, 30),
            (float)birdY + rand.Next(20, 50),
            rand.NextDouble() * 0.6 - 0.3,
            -rand.NextDouble() * 0.8 - 0.3,
            60
        ));
    }

    UpdateAndDrawSparks(canvas);
    UpdateAndDrawSteam(canvas);

    // Draw bird
    bool flapGlow = false;
    foreach (var ff in flapFrames)
    {
        if (Math.Abs(frame - ff) <= 3) { flapGlow = true; break; }
    }
    DrawBird(canvas, birdX, birdY, birdVel, flapGlow, frame);

    DrawForegroundCogs(canvas, frame);
    DrawVignette(canvas);

    // HUD
    if (isGameplay)
    {
        DrawScoreHUD(canvas, frame);
    }

    if (isTitle) DrawTitle(canvas, frame);
    if (isGameOver) DrawGameOver(canvas, frame);

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    using var fs = File.OpenWrite(Path.Combine(OUTPUT_DIR, $"frame_{frame:0000}.png"));
    data.SaveTo(fs);

    if (frame % 100 == 0) Console.WriteLine($"Frame {frame}/{TOTAL_FRAMES}");
}

Console.WriteLine("All frames rendered.");

// ============== DRAW FUNCTIONS ==============

void DrawSky(SKCanvas canvas)
{
    using var paint = new SKPaint();
    using var shader = SKShader.CreateLinearGradient(
        new SKPoint(0, 0), new SKPoint(0, H),
        new[] {
            new SKColor(0x2A, 0x1A, 0x05),
            new SKColor(0x7A, 0x4A, 0x0A),
            new SKColor(0xC4, 0x9A, 0x20),
        },
        new float[] { 0f, 0.5f, 1f },
        SKShaderTileMode.Clamp);
    paint.Shader = shader;
    canvas.DrawRect(0, 0, W, H, paint);

    // Cloud streaks
    using var cloudPaint = new SKPaint
    {
        Color = new SKColor(0xD4, 0xB0, 0x60, 60),
        IsAntialias = true
    };
    var cloudYs = new[] { 80, 140, 200, 260, 320 };
    var cloudOffsets = new[] { 50, 350, 120, 480, 250 };
    var cloudWidths = new[] { 320, 220, 280, 180, 360 };
    for (int i = 0; i < cloudYs.Length; i++)
    {
        canvas.DrawRect(cloudOffsets[i], cloudYs[i], cloudWidths[i], 2, cloudPaint);
    }
    using var cloudPaint2 = new SKPaint
    {
        Color = new SKColor(0xD4, 0xB0, 0x60, 40),
        IsAntialias = true
    };
    var cloudYs2 = new[] { 100, 170, 230, 300 };
    var cloudOffsets2 = new[] { 600, 30, 400, 150 };
    for (int i = 0; i < cloudYs2.Length; i++)
    {
        canvas.DrawRect(cloudOffsets2[i], cloudYs2[i], 200, 1, cloudPaint2);
    }
}

void DrawBackgroundCogs(SKCanvas canvas, int frame)
{
    var cogs = new[] {
        (cx: 150f, cy: 180f, r: 100f, speed: 0.004f, teeth: 14),
        (cx: 650f, cy: 220f, r: 120f, speed: -0.003f, teeth: 16),
        (cx: 400f, cy: 100f, r: 80f, speed: 0.005f, teeth: 12),
    };
    foreach (var c in cogs)
    {
        DrawCog(canvas, c.cx, c.cy, c.r, c.teeth, frame * c.speed,
            new SKColor(0x8B, 0x69, 0x14, 38));
    }
}

void DrawCog(SKCanvas canvas, float cx, float cy, float r, int teeth, double angle, SKColor color)
{
    using var paint = new SKPaint
    {
        Color = color,
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    using var path = new SKPath();
    float toothDepth = r * 0.15f;
    float innerR = r - toothDepth;
    for (int i = 0; i < teeth * 2; i++)
    {
        double a = angle + i * Math.PI / teeth;
        float rr = (i % 2 == 0) ? r : innerR;
        float x = cx + (float)(Math.Cos(a) * rr);
        float y = cy + (float)(Math.Sin(a) * rr);
        if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
    }
    path.Close();
    canvas.DrawPath(path, paint);

    // Inner hole
    using var holePaint = new SKPaint
    {
        Color = new SKColor(0, 0, 0, 0),
        BlendMode = SKBlendMode.Clear
    };
    // Just draw a darker inner circle for visual depth
    using var innerPaint = new SKPaint
    {
        Color = new SKColor(color.Red, color.Green, color.Blue, (byte)(color.Alpha / 2)),
        IsAntialias = true
    };
    canvas.DrawCircle(cx, cy, r * 0.25f, innerPaint);
}

void DrawCitySkyline(SKCanvas canvas, int frame)
{
    float scrollX = (float)((frame * 0.1) % 200);
    using var paint = new SKPaint
    {
        Color = new SKColor(0x2A, 0x20, 0x10),
        IsAntialias = true
    };
    // Buildings far back
    float baseY = 460;
    for (int i = -1; i < 12; i++)
    {
        float bx = i * 90 - scrollX;
        float bw = 60 + (i * 17) % 30;
        float bh = 80 + (i * 23) % 70;
        canvas.DrawRect(bx, baseY - bh, bw, bh, paint);
        // Window dots
        using var win = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20, 90),
            IsAntialias = true
        };
        for (int wy = 0; wy < bh / 14; wy++)
        {
            for (int wx = 0; wx < bw / 12; wx++)
            {
                if (((wx + wy + i) * 7) % 5 == 0)
                {
                    canvas.DrawRect(bx + 4 + wx * 12, baseY - bh + 6 + wy * 14, 3, 4, win);
                }
            }
        }
    }
}

void DrawClockTower(SKCanvas canvas, int frame)
{
    float scrollX = (float)((frame * 0.1) % 800);
    float towerX = 580 - scrollX;
    if (towerX < -100) towerX += 800;
    float towerY = 280;
    float tw = 50;
    float th = 180;

    using var paint = new SKPaint { Color = new SKColor(0x3A, 0x2A, 0x14), IsAntialias = true };
    canvas.DrawRect(towerX, towerY, tw, th, paint);

    // Roof point
    using var roof = new SKPaint { Color = new SKColor(0x4A, 0x32, 0x1A), IsAntialias = true };
    using var path = new SKPath();
    path.MoveTo(towerX - 6, towerY);
    path.LineTo(towerX + tw / 2, towerY - 30);
    path.LineTo(towerX + tw + 6, towerY);
    path.Close();
    canvas.DrawPath(path, roof);

    // Clock face
    float cx = towerX + tw / 2;
    float cy = towerY + 30;
    using var face = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20, 200), IsAntialias = true };
    canvas.DrawCircle(cx, cy, 18, face);
    using var faceBorder = new SKPaint
    {
        Color = new SKColor(0x2A, 0x1A, 0x05),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        IsAntialias = true
    };
    canvas.DrawCircle(cx, cy, 18, faceBorder);

    // Tick marks
    using var tickP = new SKPaint
    {
        Color = new SKColor(0x2A, 0x1A, 0x05),
        StrokeWidth = 1.5f,
        IsAntialias = true
    };
    for (int i = 0; i < 12; i++)
    {
        double a = i * Math.PI / 6;
        float x1 = cx + (float)(Math.Cos(a) * 14);
        float y1 = cy + (float)(Math.Sin(a) * 14);
        float x2 = cx + (float)(Math.Cos(a) * 17);
        float y2 = cy + (float)(Math.Sin(a) * 17);
        canvas.DrawLine(x1, y1, x2, y2, tickP);
    }

    // Hands (slow rotation)
    double hourAngle = -Math.PI / 2 + frame * 0.001;
    double minAngle = -Math.PI / 2 + frame * 0.012;
    using var handP = new SKPaint
    {
        Color = new SKColor(0x2A, 0x1A, 0x05),
        StrokeWidth = 2,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round
    };
    canvas.DrawLine(cx, cy,
        cx + (float)(Math.Cos(hourAngle) * 9),
        cy + (float)(Math.Sin(hourAngle) * 9), handP);
    canvas.DrawLine(cx, cy,
        cx + (float)(Math.Cos(minAngle) * 14),
        cy + (float)(Math.Sin(minAngle) * 14), handP);
}

void DrawChimneyStacks(SKCanvas canvas, int frame)
{
    float scrollX = (float)((frame * 0.2) % 200);
    using var paint = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x05), IsAntialias = true };
    var stacks = new[] {
        (x: 80f, h: 120f),
        (x: 280f, h: 90f),
        (x: 460f, h: 140f),
        (x: 660f, h: 100f),
        (x: 880f, h: 110f),
    };
    foreach (var s in stacks)
    {
        float x = s.x - scrollX;
        if (x < -50) x += 1000;
        canvas.DrawRect(x, 470 - s.h, 28, s.h, paint);
        // Cap
        using var capP = new SKPaint { Color = new SKColor(0x4A, 0x32, 0x1A), IsAntialias = true };
        canvas.DrawRect(x - 4, 470 - s.h - 6, 36, 8, capP);
    }
}

void UpdateAndDrawSmoke(SKCanvas canvas, int frame)
{
    // Spawn new puffs from chimneys
    if (frame % 8 == 0)
    {
        var stackTops = new[] { (80f, 350f), (280f, 380f), (460f, 330f), (660f, 370f) };
        foreach (var (x, y) in stackTops)
        {
            if (rand.NextDouble() < 0.7)
            {
                smokePuffs.Add(new SmokePuff(x + 14, y, 0));
            }
        }
    }

    for (int i = smokePuffs.Count - 1; i >= 0; i--)
    {
        var p = smokePuffs[i];
        p.Age++;
        p.X -= 0.4f; // drift up-left
        p.Y -= 0.6f;
        if (p.Age > 180) { smokePuffs.RemoveAt(i); continue; }
        float t = p.Age / 180f;
        float radius = 10 + t * 40;
        byte alpha = (byte)(180 * (1 - t));
        using var sp = new SKPaint
        {
            Color = new SKColor(0x80, 0x80, 0x70, alpha),
            IsAntialias = true
        };
        canvas.DrawCircle(p.X, p.Y, radius, sp);
    }
}

void DrawGround(SKCanvas canvas, int frame)
{
    using var grad = new SKPaint();
    using var sh = SKShader.CreateLinearGradient(
        new SKPoint(0, 520), new SKPoint(0, H),
        new[] {
            new SKColor(0x1A, 0x1A, 0x1A),
            new SKColor(0x2A, 0x2A, 0x1A),
        },
        SKShaderTileMode.Clamp);
    grad.Shader = sh;
    canvas.DrawRect(0, 520, W, 80, grad);

    // Factory silhouettes on ground
    using var fp = new SKPaint { Color = new SKColor(0x0F, 0x0F, 0x0F), IsAntialias = true };
    float scrollX = (float)((frame * 1.0) % 150);
    for (int i = -1; i < 8; i++)
    {
        float fx = i * 110 - scrollX;
        canvas.DrawRect(fx, 535, 70, 25, fp);
        canvas.DrawRect(fx + 20, 525, 12, 15, fp); // small chimney
    }
}

void DrawPipe(SKCanvas canvas, Pipe p, int frame)
{
    float pipeW = 70;
    float gapH = p.GapHeight;
    float topH = (float)p.GapY - gapH / 2;
    float bottomY = (float)p.GapY + gapH / 2;
    float bottomH = 520 - bottomY;
    if (topH <= 0 || bottomH <= 0) return;

    using var bodyP = new SKPaint
    {
        Color = new SKColor(0x8B, 0x69, 0x14),
        IsAntialias = true
    };
    using var darkP = new SKPaint
    {
        Color = new SKColor(0x6A, 0x4A, 0x0A),
        IsAntialias = true
    };
    using var rivetP = new SKPaint
    {
        Color = new SKColor(0xC4, 0x9A, 0x20),
        IsAntialias = true
    };
    using var boltP = new SKPaint
    {
        Color = new SKColor(0x1A, 0x10, 0x05),
        IsAntialias = true
    };

    float px = (float)p.X;

    // Top pipe
    canvas.DrawRect(px, 0, pipeW, topH, bodyP);
    // Top cap (flange)
    canvas.DrawRect(px - 6, topH - 18, pipeW + 12, 18, darkP);
    // Bolt holes on cap
    for (int b = 0; b < 5; b++)
    {
        canvas.DrawCircle(px - 2 + b * (pipeW + 8) / 4f, topH - 9, 2, boltP);
    }
    // Rivets along top pipe edges
    for (int y = 20; y < topH - 20; y += 40)
    {
        canvas.DrawCircle(px + 5, y, 4, rivetP);
        canvas.DrawCircle(px + pipeW - 5, y, 4, rivetP);
    }

    // Bottom pipe
    canvas.DrawRect(px, bottomY, pipeW, bottomH, bodyP);
    canvas.DrawRect(px - 6, bottomY, pipeW + 12, 18, darkP);
    for (int b = 0; b < 5; b++)
    {
        canvas.DrawCircle(px - 2 + b * (pipeW + 8) / 4f, bottomY + 9, 2, boltP);
    }
    for (int y = (int)bottomY + 30; y < 510; y += 40)
    {
        canvas.DrawCircle(px + 5, y, 4, rivetP);
        canvas.DrawCircle(px + pipeW - 5, y, 4, rivetP);
    }

    // Steam vent at pipe mouths
    int phase = (frame / 4) % 8;
    for (int s = 0; s < 8; s++)
    {
        float sx = px - 15 + (s * (pipeW + 30) / 8f) + ((s + phase) % 3 - 1) * 4;
        float sy = topH + 5 + ((s + phase) % 4) * 3;
        float sw = 14 + ((s + phase) % 3) * 6;
        float sh = 10 + ((s + phase) % 3) * 4;
        byte alpha = (byte)(50 + (s % 3) * 15);
        using var sp = new SKPaint { Color = new SKColor(255, 255, 255, alpha), IsAntialias = true };
        canvas.DrawOval(sx, sy, sw, sh, sp);

        float sy2 = bottomY - 15 - ((s + phase) % 4) * 3;
        canvas.DrawOval(sx, sy2, sw, sh, sp);
    }
}

void UpdateAndDrawSparks(SKCanvas canvas)
{
    using var paint = new SKPaint
    {
        Color = new SKColor(0xFF, 0xD7, 0x00),
        IsAntialias = true
    };
    for (int i = sparkParticles.Count - 1; i >= 0; i--)
    {
        var p = sparkParticles[i];
        p.X += (float)p.VX;
        p.Y += (float)p.VY;
        p.VY += 0.15;
        p.Life--;
        if (p.Life <= 0) { sparkParticles.RemoveAt(i); continue; }
        canvas.DrawCircle(p.X, p.Y, 2, paint);
    }
}

void UpdateAndDrawSteam(SKCanvas canvas)
{
    for (int i = steamParticles.Count - 1; i >= 0; i--)
    {
        var p = steamParticles[i];
        p.X += (float)p.VX;
        p.Y += (float)p.VY;
        p.Life--;
        if (p.Life <= 0) { steamParticles.RemoveAt(i); continue; }
        float t = 1f - p.Life / 60f;
        float r = 20 + t * 20;
        byte alpha = (byte)(30 * (1 - t));
        using var sp = new SKPaint
        {
            Color = new SKColor(255, 255, 255, alpha),
            IsAntialias = true
        };
        canvas.DrawCircle(p.X, p.Y, r, sp);
    }
}

void DrawBird(SKCanvas canvas, double x, double y, double vel, bool flapGlow, int frame)
{
    canvas.Save();
    canvas.Translate((float)x, (float)y);
    double rot = Math.Clamp(vel * 3, -25, 60);
    canvas.RotateDegrees((float)rot);

    // Glow ring on flap
    if (flapGlow)
    {
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20, 89),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12)
        };
        canvas.DrawCircle(0, 0, 80, glowPaint);
    }

    if (koalaBmp != null)
    {
        var dest = new SKRect(-80, -65, 80, 65);
        canvas.DrawBitmap(koalaBmp, dest, new SKPaint { FilterQuality = SKFilterQuality.High });
    }
    else
    {
        // Fallback shape
        using var p = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true };
        canvas.DrawOval(0, 0, 80, 50, p);
    }

    canvas.Restore();
}

void DrawForegroundCogs(SKCanvas canvas, int frame)
{
    DrawCog(canvas, -10, H - 10, 80, 14, frame * 0.02,
        new SKColor(0x8B, 0x69, 0x14, 102));
    DrawCog(canvas, W + 10, H - 10, 80, 14, -frame * 0.02,
        new SKColor(0x8B, 0x69, 0x14, 102));
}

void DrawVignette(SKCanvas canvas)
{
    using var paint = new SKPaint();
    using var sh = SKShader.CreateRadialGradient(
        new SKPoint(W / 2f, H / 2f),
        Math.Max(W, H) * 0.7f,
        new[] {
            new SKColor(0, 0, 0, 0),
            new SKColor(0x1A, 0x0A, 0x00, 140),
        },
        SKShaderTileMode.Clamp);
    paint.Shader = sh;
    canvas.DrawRect(0, 0, W, H, paint);
}

void DrawScoreHUD(SKCanvas canvas, int frame)
{
    int score = (frame - 60) / 60;
    string text = $"SCORE: {score}";
    using var shadow = new SKPaint
    {
        Color = new SKColor(0, 0, 0, 200),
        IsAntialias = true,
        TextSize = 28,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    using var p = new SKPaint
    {
        Color = new SKColor(0xC4, 0x9A, 0x20),
        IsAntialias = true,
        TextSize = 28,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText(text, 22, 42, shadow);
    canvas.DrawText(text, 20, 40, p);
}

void DrawTitle(SKCanvas canvas, int frame)
{
    float fadeIn = Math.Min(1f, frame / 15f);
    float fadeOut = (frame > 45) ? Math.Max(0f, 1f - (frame - 45) / 15f) : 1f;
    float a = fadeIn * fadeOut;

    using var overlay = new SKPaint
    {
        Color = new SKColor(0x2A, 0x1A, 0x05, (byte)(180 * a)),
    };
    canvas.DrawRect(0, 0, W, H, overlay);

    using var title = new SKPaint
    {
        Color = new SKColor(0xC4, 0x9A, 0x20, (byte)(255 * a)),
        IsAntialias = true,
        TextSize = 54,
        TextAlign = SKTextAlign.Center,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    using var titleShadow = new SKPaint
    {
        Color = new SKColor(0, 0, 0, (byte)(220 * a)),
        IsAntialias = true,
        TextSize = 54,
        TextAlign = SKTextAlign.Center,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText("STEAMPUNK DOWNUNDER", W / 2f + 3, 263, titleShadow);
    canvas.DrawText("STEAMPUNK DOWNUNDER", W / 2f, 260, title);

    using var sub = new SKPaint
    {
        Color = new SKColor(0xD4, 0xB0, 0x60, (byte)(255 * a)),
        IsAntialias = true,
        TextSize = 30,
        TextAlign = SKTextAlign.Center,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText("FlappyBrain 🧠", W / 2f, 320, sub);
}

void DrawGameOver(SKCanvas canvas, int frame)
{
    int local = frame - 840;
    float a = Math.Min(1f, local / 15f);

    using var overlay = new SKPaint
    {
        Color = new SKColor(0x2A, 0x1A, 0x05, (byte)(200 * a)),
    };
    canvas.DrawRect(0, 0, W, H, overlay);

    using var go = new SKPaint
    {
        Color = new SKColor(0xC4, 0x9A, 0x20, (byte)(255 * a)),
        IsAntialias = true,
        TextSize = 80,
        TextAlign = SKTextAlign.Center,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    using var goShadow = new SKPaint
    {
        Color = new SKColor(0, 0, 0, (byte)(220 * a)),
        IsAntialias = true,
        TextSize = 80,
        TextAlign = SKTextAlign.Center,
        Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
    };
    canvas.DrawText("GAME OVER", W / 2f + 4, 304, goShadow);
    canvas.DrawText("GAME OVER", W / 2f, 300, go);
}

// ============== TYPES ==============

class Pipe
{
    public double X;
    public double GapY;
    public float GapHeight;
    public Pipe(double x, double gy, float gh) { X = x; GapY = gy; GapHeight = gh; }
}

class Particle
{
    public float X;
    public float Y;
    public double VX;
    public double VY;
    public int Life;
    public Particle(double x, double y, double vx, double vy, int life)
    { X = (float)x; Y = (float)y; VX = vx; VY = vy; Life = life; }
}

class SmokePuff
{
    public float X;
    public float Y;
    public int Age;
    public SmokePuff(float x, float y, int age) { X = x; Y = y; Age = age; }
}
