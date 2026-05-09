using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme6R;

internal static class Program
{
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL = 900;
    const string OUT = "/tmp/fb-t6r-frames";

    // Phase ranges
    const int TITLE_END = 60;
    const int GAMEPLAY_END = 840;

    // Auto-flap frames (during gameplay)
    static readonly int[] FLAP_FRAMES = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    // Helicopter physics
    static float heliY = 300f;
    static float heliVy = 0f;
    const float GRAVITY = 0.45f;
    const float FLAP_VY = -7.5f;

    // Pipes
    class Pipe { public float X; public float GapY; public float GapH; public bool Scored; }
    static readonly List<Pipe> pipes = new();

    // Particles
    class Particle { public float X, Y, Vx, Vy; public int Life; public int MaxLife; public SKColor Color; public float Size; }
    static readonly List<Particle> particles = new();

    // Gold coins (flap burst)
    class Coin { public float X, Y, Vx, Vy; public int Life; }
    static readonly List<Coin> coins = new();

    static int score = 0;
    static int hudFlashFrames = 0;
    static int flashFrame = -10;

    // Random for ambient
    static readonly Random rng = new(20260509);

    // Sky gradient (background) is camera-shifted; we use a single fixed background

    static void Main()
    {
        Directory.CreateDirectory(OUT);

        // Pre-spawn pipes for gameplay
        SpawnInitialPipes();

        for (int f = 0; f < TOTAL; f++)
        {
            using var bmp = new SKBitmap(W, H);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(0x04, 0x0E, 0x1E));

            if (f < TITLE_END)
            {
                DrawTitle(canvas, f);
            }
            else if (f < GAMEPLAY_END)
            {
                int gf = f - TITLE_END;
                DrawGameplay(canvas, f, gf);
            }
            else
            {
                int of = f - GAMEPLAY_END;
                DrawGameOver(canvas, of);
            }

            DrawVignette(canvas);

            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 92);
            using var fs = File.OpenWrite(Path.Combine(OUT, $"frame_{f:D4}.png"));
            data.SaveTo(fs);
        }
        Console.WriteLine($"Wrote {TOTAL} frames to {OUT}");
    }

    // ============================================================
    // TITLE
    // ============================================================
    static void DrawTitle(SKCanvas canvas, int f)
    {
        // Background gradient
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, H),
                new[] { new SKColor(0x04, 0x0E, 0x1E), new SKColor(0x0A, 0x1E, 0x3A), new SKColor(0x14, 0x28, 0x50) },
                new float[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, W, H, paint);
        }

        // Subtle data rain in background
        DrawDataRain(canvas, f, 0.06f);

        // Big chihuahua face center (3x size)
        DrawChihuahuaFace(canvas, W / 2f, H / 2f - 70, 3.0f);

        // Title text
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 60, TextAlign = SKTextAlign.Center })
        {
            // shadow
            using var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0x8B, 0x69, 0x14), Typeface = paint.Typeface, TextSize = 60, TextAlign = SKTextAlign.Center };
            canvas.DrawText("10,000 SPOONS", W / 2f + 3, H / 2f + 110 + 3, sh);
            canvas.DrawText("10,000 SPOONS", W / 2f, H / 2f + 110, paint);
        }
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xE0, 0xE0, 0xE0), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 26, TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("UNDERDOGS ARMY", W / 2f, H / 2f + 150, paint);
        }
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0x20, 0xC0, 0xA0), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Italic), TextSize = 20, TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("Small doesn't mean weak. It means FAST.", W / 2f, H / 2f + 185, paint);
        }

        // White flash 55-59
        if (f >= 55)
        {
            byte alpha = (byte)(180 - (f - 55) * 30);
            using var p = new SKPaint { Color = new SKColor(255, 255, 255, alpha) };
            canvas.DrawRect(0, 0, W, H, p);
        }
    }

    // ============================================================
    // GAMEPLAY
    // ============================================================
    static void DrawGameplay(SKCanvas canvas, int f, int gf)
    {
        // Update physics
        bool flap = Array.IndexOf(FLAP_FRAMES, f) >= 0;
        if (flap)
        {
            heliVy = FLAP_VY;
            flashFrame = f;
            // Coin burst
            for (int i = 0; i < 8; i++)
            {
                double ang = i * (Math.PI * 2 / 8);
                coins.Add(new Coin { X = 200, Y = heliY, Vx = (float)Math.Cos(ang) * 3.5f, Vy = (float)Math.Sin(ang) * 3.5f - 1.5f, Life = 12 });
            }
            // Sparks
            for (int i = 0; i < 12; i++)
            {
                particles.Add(new Particle
                {
                    X = 200 + (float)(rng.NextDouble() * 40 - 20),
                    Y = heliY - 25,
                    Vx = (float)(rng.NextDouble() * 6 - 3),
                    Vy = (float)(rng.NextDouble() * -3 - 1),
                    Life = 18,
                    MaxLife = 18,
                    Color = new SKColor(0xFF, 0xD0, 0x40),
                    Size = 2 + (float)rng.NextDouble() * 2
                });
            }
        }
        heliVy += GRAVITY;
        heliVy = Math.Clamp(heliVy, -10f, 9f);
        heliY += heliVy;
        heliY = Math.Clamp(heliY, 80f, H - 100);

        // Update pipes (scroll left)
        float pipeSpeed = 2.6f;
        foreach (var p in pipes) p.X -= pipeSpeed;
        // Score on pass
        foreach (var p in pipes)
        {
            if (!p.Scored && p.X + 95 < 200)
            {
                p.Scored = true;
                score++;
                hudFlashFrames = 8;
            }
        }
        // Recycle / add
        pipes.RemoveAll(p => p.X < -120);
        if (pipes.Count == 0 || pipes[^1].X < W - 220)
        {
            float gh = 170;
            float gy = 120 + (float)rng.NextDouble() * 250;
            pipes.Add(new Pipe { X = W + 50, GapY = gy, GapH = gh });
        }

        // ----- DRAW BACKGROUND -----
        // Sky gradient
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, H),
                new[] { new SKColor(0x04, 0x0E, 0x1E), new SKColor(0x0A, 0x1E, 0x3A), new SKColor(0x14, 0x28, 0x50) },
                new float[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, W, H, paint);
        }

        DrawDataRain(canvas, f, 0.08f);

        // Far towers (parallax 0.08)
        DrawFarTowers(canvas, f * 0.08f);
        // Mid towers with damage (parallax 0.15)
        DrawMidTowers(canvas, f * 0.15f);
        // Rooftop dobermans
        DrawRooftopDobermans(canvas, f * 0.15f);

        // Billboards (0.4 parallax)
        DrawBillboards(canvas, f * 0.4f);

        // Ground
        DrawGround(canvas, f);

        // Pipes (Goliath suits)
        foreach (var p in pipes) DrawSuitPipe(canvas, p);

        // Speed lines
        DrawSpeedLines(canvas, f, flap);

        // Update / draw particles
        UpdateAndDrawParticles(canvas);
        UpdateAndDrawCoins(canvas);

        // Helicopter w/ chihuahua at x=200
        float angle = Math.Clamp(heliVy * 2.0f, -25f, 35f);
        DrawHelicopter(canvas, 200, heliY, angle, f, flap);

        // Near-miss extra burst at frame 640
        if (f == 640 || f == 641 || f == 642)
        {
            using var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40, 100) };
            canvas.DrawCircle(200, heliY - 25, 50, p);
        }

        // HUD
        DrawHud(canvas);
        if (hudFlashFrames > 0) hudFlashFrames--;
    }

    // ============================================================
    // GAME OVER
    // ============================================================
    static void DrawGameOver(SKCanvas canvas, int of)
    {
        // Keep last gameplay-like background, dimmed
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, H),
                new[] { new SKColor(0x04, 0x08, 0x14), new SKColor(0x08, 0x14, 0x24) },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, W, H, paint);
        }

        DrawDataRain(canvas, of + GAMEPLAY_END, 0.05f);

        // Dark overlay
        using (var p = new SKPaint { Color = new SKColor(0, 0, 0, 140) })
            canvas.DrawRect(0, 0, W, H, p);

        // "DISRUPTED" headline
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xCC, 0x20, 0x20), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 80, TextAlign = SKTextAlign.Center })
        {
            using var sh = new SKPaint { IsAntialias = true, Color = new SKColor(40, 0, 0, 200), Typeface = paint.Typeface, TextSize = 80, TextAlign = SKTextAlign.Center };
            canvas.DrawText("DISRUPTED", W / 2f + 4, H / 2f - 70 + 4, sh);
            canvas.DrawText("DISRUPTED", W / 2f, H / 2f - 70, paint);
        }

        // Final count
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 36, TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText($"DISRUPTIONS: {score}", W / 2f, H / 2f, paint);
        }

        // Tagline
        using (var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0x20, 0xC0, 0xA0), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Italic), TextSize = 22, TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("The Underdogs Army never quits.", W / 2f, H / 2f + 50, paint);
        }

        // Small chihuahua corner
        DrawChihuahuaFace(canvas, 90, H - 90, 0.9f);
    }

    // ============================================================
    // CHARACTER: HELICOPTER + CHIHUAHUA
    // ============================================================
    static void DrawHelicopter(SKCanvas canvas, float cx, float cy, float angle, int f, bool flap)
    {
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(angle);

        // ----- HULL -----
        using (var hull = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x1A, 0x2A), Style = SKPaintStyle.Fill })
        {
            // Main hull (rounded)
            var hullRect = new SKRoundRect(new SKRect(-55, -22, 55, 23), 18, 22);
            canvas.DrawRoundRect(hullRect, hull);

            // Tail boom
            canvas.DrawRect(new SKRect(40, -6, 90, 6), hull);
        }
        // Hull highlight
        using (var hl = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x3A) })
        {
            canvas.DrawRect(new SKRect(-50, -20, 50, -14), hl);
        }

        // Cockpit window
        using (var win = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x2A, 0x4A) })
        {
            var r = new SKRoundRect(new SKRect(-50, -10, -15, 15), 4, 4);
            canvas.DrawRoundRect(r, win);
        }
        using (var winShine = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x6A, 0x9A, 160) })
        {
            canvas.DrawRect(new SKRect(-46, -8, -38, 0), winShine);
        }

        // SWAT marking on hull
        using (var t = new SKPaint { IsAntialias = true, Color = SKColors.White, Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 12 })
        {
            canvas.DrawText("SWAT", -5, 8, t);
        }

        // Tail rotor (small cross)
        using (var tr = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x5A), StrokeWidth = 2, Style = SKPaintStyle.Stroke })
        {
            float rotAng = (f * 25f) % 360;
            canvas.Save();
            canvas.Translate(92, 0);
            canvas.RotateDegrees(rotAng);
            canvas.DrawLine(-8, 0, 8, 0, tr);
            canvas.DrawLine(0, -8, 0, 8, tr);
            canvas.Restore();
        }

        // Skids (landing gear)
        using (var sk = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x35), StrokeWidth = 3, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(-35, 25, 35, 25, sk);
            canvas.DrawLine(-25, 23, -25, 28, sk);
            canvas.DrawLine(25, 23, 25, 28, sk);
        }

        // Top rotor (rotating)
        float topAng = (f * 30f) % 360;
        canvas.Save();
        canvas.Translate(0, -22);
        canvas.RotateDegrees(topAng);
        using (var rot = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x5A), StrokeWidth = 3, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(-30, 0, 30, 0, rot);
        }
        using (var rotBlur = new SKPaint { IsAntialias = true, Color = new SKColor(0x6A, 0x6A, 0x7A, 80), StrokeWidth = 1, Style = SKPaintStyle.Stroke })
        {
            // Two crossed blade lines for blur
            canvas.RotateDegrees(60);
            canvas.DrawLine(-30, 0, 30, 0, rotBlur);
            canvas.RotateDegrees(60);
            canvas.DrawLine(-30, 0, 30, 0, rotBlur);
        }
        // Rotor hub
        using (var hub = new SKPaint { IsAntialias = true, Color = new SKColor(0x6A, 0x6A, 0x7A) })
            canvas.DrawCircle(0, 0, 4, hub);
        canvas.Restore();

        // Gold spoon antenna on top (brand easter egg)
        using (var spoon = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40) })
        {
            // shaft
            using var stroke = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(-2, -22, 6, -38, stroke);
            // bowl
            canvas.DrawOval(new SKRect(2, -44, 12, -36), spoon);
        }

        // ----- CHIHUAHUA PILOT (from top hatch) -----
        canvas.Save();
        canvas.Translate(-5, -32);

        // Body (oval, behind head, mostly hidden under vest)
        using (var body = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0xA0, 0x60) })
        {
            canvas.DrawOval(new SKRect(-17, 8, 18, 22), body);
        }
        // SWAT vest on body
        using (var vest = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x1A, 0x1A) })
        {
            canvas.DrawRect(new SKRect(-15, 6, 15, 22), vest);
        }
        // Vest highlight
        using (var vh = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x2A) })
            canvas.DrawRect(new SKRect(-15, 6, 15, 9), vh);
        // SWAT text on vest
        using (var swt = new SKPaint { IsAntialias = true, Color = SKColors.White, Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 8, TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("SWAT", 0, 18, swt);
        }

        // Tiny defiant paw raised
        using (var paw = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0xA0, 0x60) })
        {
            canvas.Save();
            canvas.Translate(14, 14);
            canvas.RotateDegrees(-30);
            canvas.DrawRect(new SKRect(0, -3, 10, 3), paw);
            canvas.DrawCircle(10, 0, 4, paw);
            canvas.Restore();
        }

        // EARS (behind head)
        using (var ear = new SKPaint { IsAntialias = true, Color = new SKColor(0xC8, 0x90, 0x50) })
        {
            // Left ear (tall pointy triangle)
            using var path = new SKPath();
            path.MoveTo(-12, -8);
            path.LineTo(-18, -28);
            path.LineTo(-6, -14);
            path.Close();
            canvas.DrawPath(path, ear);

            // Right ear
            using var path2 = new SKPath();
            path2.MoveTo(12, -8);
            path2.LineTo(18, -28);
            path2.LineTo(6, -14);
            path2.Close();
            canvas.DrawPath(path2, ear);
        }
        // Inner ear pink
        using (var ip = new SKPaint { IsAntialias = true, Color = new SKColor(0xE8, 0xB0, 0x90) })
        {
            using var p1 = new SKPath();
            p1.MoveTo(-12, -10);
            p1.LineTo(-15, -22);
            p1.LineTo(-9, -16);
            p1.Close();
            canvas.DrawPath(p1, ip);
            using var p2 = new SKPath();
            p2.MoveTo(12, -10);
            p2.LineTo(15, -22);
            p2.LineTo(9, -16);
            p2.Close();
            canvas.DrawPath(p2, ip);
        }

        // HEAD circle
        using (var head = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0xA0, 0x60) })
        {
            canvas.DrawCircle(0, 0, 16, head);
        }
        // Head shading
        using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0xC0, 0x8C, 0x4C, 130) })
        {
            canvas.DrawOval(new SKRect(-14, 4, 14, 14), sh);
        }

        // Eyes (large dark)
        using (var eye = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x08, 0x04) })
        {
            canvas.DrawCircle(-6, -2, 3.5f, eye);
            canvas.DrawCircle(6, -2, 3.5f, eye);
        }
        // Eye highlights (determined glint)
        using (var eh = new SKPaint { IsAntialias = true, Color = SKColors.White })
        {
            canvas.DrawCircle(-5, -3, 1.2f, eh);
            canvas.DrawCircle(7, -3, 1.2f, eh);
        }
        // Brow (alert / determined V)
        using (var brow = new SKPaint { IsAntialias = true, Color = new SKColor(0x80, 0x55, 0x30), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(-9, -7, -3, -5, brow);
            canvas.DrawLine(9, -7, 3, -5, brow);
        }

        // Nose
        using (var nose = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x08, 0x04) })
        {
            canvas.DrawOval(new SKRect(-2.5f, 4, 2.5f, 8), nose);
        }
        // Mouth (determined small line)
        using (var mouth = new SKPaint { IsAntialias = true, Color = new SKColor(0x40, 0x20, 0x10), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(0, 8, 0, 10, mouth);
            canvas.DrawLine(0, 10, -2, 11, mouth);
            canvas.DrawLine(0, 10, 2, 11, mouth);
        }

        // Headset arc
        using (var hs = new SKPaint { IsAntialias = true, Color = new SKColor(0x3A, 0x3A, 0x3A), StrokeWidth = 2, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawArc(new SKRect(-14, -16, 14, 4), 200, 140, false, hs);
        }
        // Earpiece
        using (var ep = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x2A) })
        {
            canvas.DrawCircle(-13, -2, 2.8f, ep);
            canvas.DrawCircle(13, -2, 2.8f, ep);
        }
        // Mic boom
        using (var mc = new SKPaint { IsAntialias = true, Color = new SKColor(0x3A, 0x3A, 0x3A), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(13, 0, 9, 7, mc);
        }

        canvas.Restore();

        // Flap glow ring
        if (flap)
        {
            using var glow = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
            canvas.DrawCircle(0, -22, 70, glow);
        }

        canvas.Restore();
    }

    // ============================================================
    // CHIHUAHUA FACE (for title / game over)
    // ============================================================
    static void DrawChihuahuaFace(SKCanvas canvas, float cx, float cy, float scale)
    {
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Scale(scale, scale);

        // Ears
        using (var ear = new SKPaint { IsAntialias = true, Color = new SKColor(0xC8, 0x90, 0x50) })
        {
            using var p1 = new SKPath();
            p1.MoveTo(-14, -8); p1.LineTo(-22, -38); p1.LineTo(-4, -16); p1.Close();
            canvas.DrawPath(p1, ear);
            using var p2 = new SKPath();
            p2.MoveTo(14, -8); p2.LineTo(22, -38); p2.LineTo(4, -16); p2.Close();
            canvas.DrawPath(p2, ear);
        }
        using (var ip = new SKPaint { IsAntialias = true, Color = new SKColor(0xE8, 0xB0, 0x90) })
        {
            using var p1 = new SKPath();
            p1.MoveTo(-14, -11); p1.LineTo(-19, -30); p1.LineTo(-7, -18); p1.Close();
            canvas.DrawPath(p1, ip);
            using var p2 = new SKPath();
            p2.MoveTo(14, -11); p2.LineTo(19, -30); p2.LineTo(7, -18); p2.Close();
            canvas.DrawPath(p2, ip);
        }

        // Head
        using (var head = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0xA0, 0x60) })
            canvas.DrawCircle(0, 0, 22, head);
        using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0xC0, 0x8C, 0x4C, 130) })
            canvas.DrawOval(new SKRect(-19, 5, 19, 19), sh);

        // Eyes
        using (var eye = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x08, 0x04) })
        {
            canvas.DrawCircle(-8, -3, 5, eye);
            canvas.DrawCircle(8, -3, 5, eye);
        }
        using (var eh = new SKPaint { IsAntialias = true, Color = SKColors.White })
        {
            canvas.DrawCircle(-7, -4, 1.6f, eh);
            canvas.DrawCircle(9, -4, 1.6f, eh);
        }
        using (var brow = new SKPaint { IsAntialias = true, Color = new SKColor(0x80, 0x55, 0x30), StrokeWidth = 2.2f, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(-13, -10, -4, -7, brow);
            canvas.DrawLine(13, -10, 4, -7, brow);
        }

        // Nose
        using (var nose = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x08, 0x04) })
            canvas.DrawOval(new SKRect(-3.5f, 5, 3.5f, 11), nose);

        // Mouth
        using (var mouth = new SKPaint { IsAntialias = true, Color = new SKColor(0x40, 0x20, 0x10), StrokeWidth = 1.6f, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawLine(0, 11, 0, 14, mouth);
            canvas.DrawLine(0, 14, -3, 16, mouth);
            canvas.DrawLine(0, 14, 3, 16, mouth);
        }

        canvas.Restore();
    }

    // ============================================================
    // BACKGROUND ELEMENTS
    // ============================================================

    static void DrawDataRain(SKCanvas canvas, int f, float opacityMul)
    {
        using var p = new SKPaint
        {
            IsAntialias = false,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans Mono"),
            TextSize = 10,
            Color = new SKColor(0x20, 0xC0, 0xA0, (byte)(255 * opacityMul))
        };
        for (int col = 0; col < 8; col++)
        {
            float x = 50 + col * 100 + ((col * 37) % 50);
            int seed = col * 91 + 13;
            for (int row = 0; row < 18; row++)
            {
                float y = ((f * (1 + col % 3) * 4) + row * 36 + seed) % (H + 200) - 50;
                int ch = (seed + row * 7 + f) % 16;
                string s = ch < 10 ? ch.ToString() : ((char)('A' + ch - 10)).ToString();
                canvas.DrawText(s, x, y, p);
            }
        }
    }

    static void DrawFarTowers(SKCanvas canvas, float scroll)
    {
        // Repeat every 1200px
        float sx = -((scroll % 1200) + 1200) % 1200;
        var rngLocal = new Random(101);
        using var towerP = new SKPaint { IsAntialias = false, Color = new SKColor(0x1A, 0x20, 0x30) };
        using var winP = new SKPaint { IsAntialias = false, Color = new SKColor(0xFF, 0xE0, 0x80, 180) };
        using var winP2 = new SKPaint { IsAntialias = false, Color = new SKColor(0xFF, 0xFF, 0xC0, 130) };
        using var dobP = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x1A, 0x22) };

        for (int rep = -1; rep <= 2; rep++)
        {
            float baseX = sx + rep * 1200;
            float x = baseX;
            // 12 towers per repeat
            for (int t = 0; t < 12; t++)
            {
                int wd = 60 + (rngLocal.Next(0, 5) * 10);
                int ht = 180 + rngLocal.Next(0, 180);
                float top = H - 70 - ht;
                canvas.DrawRect(x, top, wd, ht, towerP);

                // Windows random
                int cols = wd / 12;
                int rows = ht / 16;
                for (int wy = 1; wy < rows - 1; wy++)
                {
                    for (int wx = 1; wx < cols; wx++)
                    {
                        int seed = rngLocal.Next(0, 100);
                        if (seed < 32)
                        {
                            float wxp = x + wx * 12 - 4;
                            float wyp = top + wy * 16 + 4;
                            canvas.DrawRect(wxp, wyp, 4, 6, seed < 12 ? winP2 : winP);

                            // Doberman silhouette in some windows
                            if (seed > 28 && seed < 32)
                            {
                                // Dog head oval
                                canvas.DrawOval(new SKRect(wxp - 1, wyp + 1, wxp + 5, wyp + 5), dobP);
                                // pointed ears
                                using var path = new SKPath();
                                path.MoveTo(wxp, wyp + 2);
                                path.LineTo(wxp - 1, wyp);
                                path.LineTo(wxp + 1, wyp + 2);
                                path.Close();
                                canvas.DrawPath(path, dobP);
                                using var path2 = new SKPath();
                                path2.MoveTo(wxp + 4, wyp + 2);
                                path2.LineTo(wxp + 5, wyp);
                                path2.LineTo(wxp + 3, wyp + 2);
                                path2.Close();
                                canvas.DrawPath(path2, dobP);
                            }
                        }
                    }
                }
                x += wd + 8;
            }
        }
    }

    static void DrawMidTowers(SKCanvas canvas, float scroll)
    {
        float sx = -((scroll % 900) + 900) % 900;
        using var towerP = new SKPaint { IsAntialias = false, Color = new SKColor(0x22, 0x28, 0x38) };
        using var dmgP = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x10, 0x18) };
        using var winLit = new SKPaint { IsAntialias = false, Color = new SKColor(0xFF, 0xC8, 0x60, 200) };
        using var winBroken = new SKPaint { IsAntialias = false, Color = new SKColor(0x10, 0x18, 0x28) };

        for (int rep = -1; rep <= 2; rep++)
        {
            float baseX = sx + rep * 900;
            // 3 mid towers
            float[] xs = { baseX + 80, baseX + 380, baseX + 660 };
            int[] widths = { 110, 90, 130 };
            int[] heights = { 280, 240, 320 };
            for (int t = 0; t < 3; t++)
            {
                float x = xs[t];
                float top = H - 70 - heights[t];
                canvas.DrawRect(x, top, widths[t], heights[t], towerP);

                // Damage cracks
                using var crack = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x14, 0x20), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
                using var path = new SKPath();
                path.MoveTo(x + 20, top + 30);
                path.LineTo(x + 30, top + 70);
                path.LineTo(x + 20, top + 100);
                path.LineTo(x + 35, top + 130);
                canvas.DrawPath(path, crack);

                // Broken windows / lit windows
                int cols = widths[t] / 14;
                int rows = heights[t] / 18;
                for (int wy = 1; wy < rows - 1; wy++)
                {
                    for (int wx = 1; wx < cols; wx++)
                    {
                        int seed = (int)((Math.Abs(wx * 17 + wy * 31 + t * 7)) % 100);
                        if (seed < 20)
                            canvas.DrawRect(x + wx * 14, top + wy * 18, 5, 7, winLit);
                        else if (seed < 28)
                            canvas.DrawRect(x + wx * 14, top + wy * 18, 5, 7, winBroken);
                    }
                }

                // GOLIATH text on second tower
                if (t == 1)
                {
                    using var glog = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xFF, 0xFF, 60), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 14, TextAlign = SKTextAlign.Center };
                    canvas.DrawText("GOLIATH CORP", x + widths[t] / 2f, top + 20, glog);
                }

                // Rubble at base
                using var rub = new SKPaint { IsAntialias = true, Color = new SKColor(0x18, 0x14, 0x18) };
                canvas.DrawOval(new SKRect(x - 10, H - 75, x + 30, H - 65), rub);
            }
        }
    }

    static void DrawRooftopDobermans(SKCanvas canvas, float scroll)
    {
        float sx = -((scroll % 900) + 900) % 900;
        using var dob = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x3A) };
        using var dobShine = new SKPaint { IsAntialias = true, Color = new SKColor(0x3A, 0x3A, 0x4A) };
        using var tieP = new SKPaint { IsAntialias = true, Color = new SKColor(0x6A, 0x10, 0x10) };

        for (int rep = -1; rep <= 2; rep++)
        {
            float baseX = sx + rep * 900;
            float[] xs = { baseX + 100, baseX + 400, baseX + 690 };
            float[] tops = { H - 70 - 280, H - 70 - 240, H - 70 - 320 };

            for (int t = 0; t < 3; t++)
            {
                float dx = xs[t] + 50;
                float dy = tops[t] - 30;

                // Suit body (angular)
                using var path = new SKPath();
                path.MoveTo(dx - 12, dy);
                path.LineTo(dx + 12, dy);
                path.LineTo(dx + 14, dy + 50);
                path.LineTo(dx - 14, dy + 50);
                path.Close();
                canvas.DrawPath(path, dob);

                // Suit lapels
                using var lapel = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x1A, 0x22) };
                using var lp1 = new SKPath();
                lp1.MoveTo(dx - 12, dy + 2);
                lp1.LineTo(dx, dy + 18);
                lp1.LineTo(dx - 10, dy + 20);
                lp1.Close();
                canvas.DrawPath(lp1, lapel);
                using var lp2 = new SKPath();
                lp2.MoveTo(dx + 12, dy + 2);
                lp2.LineTo(dx, dy + 18);
                lp2.LineTo(dx + 10, dy + 20);
                lp2.Close();
                canvas.DrawPath(lp2, lapel);

                // Tie
                canvas.DrawRect(dx - 1.5f, dy + 12, 3, 22, tieP);

                // Head (oval, dog with pointed ears)
                canvas.DrawOval(new SKRect(dx - 9, dy - 22, dx + 9, dy + 2), dob);

                // Ears
                using var pe1 = new SKPath();
                pe1.MoveTo(dx - 6, dy - 18); pe1.LineTo(dx - 8, dy - 28); pe1.LineTo(dx - 2, dy - 20); pe1.Close();
                canvas.DrawPath(pe1, dob);
                using var pe2 = new SKPath();
                pe2.MoveTo(dx + 6, dy - 18); pe2.LineTo(dx + 8, dy - 28); pe2.LineTo(dx + 2, dy - 20); pe2.Close();
                canvas.DrawPath(pe2, dob);

                // Snout
                canvas.DrawOval(new SKRect(dx - 3, dy - 10, dx + 3, dy + 2), dobShine);

                // Tiny eye glints
                using var eye = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xC0, 0x40, 220) };
                canvas.DrawCircle(dx - 4, dy - 14, 1, eye);
                canvas.DrawCircle(dx + 4, dy - 14, 1, eye);
            }
        }
    }

    static int billboardSeq = 0;
    static int billboardFrameCount = 0;
    const int BILLBOARD_INTERVAL = 220;
    static void DrawBillboards(SKCanvas canvas, float scroll)
    {
        // Cycle messages every BILLBOARD_INTERVAL pixels of scroll
        // Compute message index from absolute scroll
        // Place a billboard every 600 px world coords
        float worldSpacing = 700f;
        float panelW = 280, panelH = 85;

        // Determine starting x
        float sx = -((scroll % worldSpacing) + worldSpacing) % worldSpacing;
        for (int i = -1; i <= 2; i++)
        {
            float bx = sx + i * worldSpacing;
            if (bx < -panelW - 50 || bx > W + 50) continue;
            // Pick message by combining position
            int msgIdx = (int)Math.Abs(Math.Floor((scroll + i * worldSpacing) / worldSpacing)) % 5;
            float bxMid = bx + panelW / 2f;
            float by = 130;

            DrawBillboardPanel(canvas, bxMid, by, panelW, panelH, msgIdx);
        }
    }

    static void DrawBillboardPanel(SKCanvas canvas, float cx, float cy, float w, float h, int msgIdx)
    {
        canvas.Save();
        canvas.Translate(cx, cy);
        // Slight tilt
        canvas.RotateDegrees((msgIdx % 2 == 0) ? -1.5f : 1.5f);

        // Pole
        using (var pole = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x4A) })
        {
            canvas.DrawRect(-3, h / 2, 6, H, pole);
        }

        // Panel bg
        using (var bg = new SKPaint { IsAntialias = true, Color = new SKColor(0x08, 0x08, 0x08) })
            canvas.DrawRect(-w / 2, -h / 2, w, h, bg);
        // Border
        using (var bd = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
            canvas.DrawRect(-w / 2, -h / 2, w, h, bd);

        // Scan lines
        using (var sl = new SKPaint { Color = new SKColor(255, 255, 255, 20) })
        {
            for (int y = -((int)h / 2); y < h / 2; y += 4)
                canvas.DrawRect(-w / 2, y, w, 1, sl);
        }

        // Message
        var bold = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold);
        var reg = SKTypeface.FromFamilyName("DejaVu Sans");

        switch (msgIdx)
        {
            case 0:
                using (var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = bold, TextSize = 36, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("UNDERDOGS ARMY", 0, 12, p);
                break;
            case 1:
                using (var p = new SKPaint { IsAntialias = true, Color = SKColors.White, Typeface = reg, TextSize = 22, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("SMALL DOESN'T MEAN WEAK", 0, 8, p);
                break;
            case 2:
                using (var p = new SKPaint { IsAntialias = true, Color = new SKColor(0x20, 0xC0, 0xA0), Typeface = bold, TextSize = 30, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("SKIN IN THE GAME", 0, 11, p);
                break;
            case 3:
                using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0x8B, 0x69, 0x14), Typeface = bold, TextSize = 38, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("10,000 SPOONS", 2, 14, sh);
                using (var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = bold, TextSize = 38, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("10,000 SPOONS", 0, 12, p);
                break;
            case 4:
                using (var p = new SKPaint { IsAntialias = true, Color = SKColors.White, Typeface = reg, TextSize = 18, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("GOLIATH HAD 5 STONES", 0, -8, p);
                using (var p2 = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = bold, TextSize = 20, TextAlign = SKTextAlign.Center })
                    canvas.DrawText("YOU HAVE 10,000", 0, 18, p2);
                break;
        }

        canvas.Restore();
    }

    static void DrawGround(SKCanvas canvas, int f)
    {
        // Ground gradient
        using (var p = new SKPaint())
        {
            p.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, H - 70), new SKPoint(0, H),
                new[] { new SKColor(0x1A, 0x10, 0x20), new SKColor(0x0A, 0x0A, 0x0A) },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, H - 70, W, 70, p);
        }
        // Top edge line
        using (var ed = new SKPaint { Color = new SKColor(0x2A, 0x20, 0x30) })
            canvas.DrawRect(0, H - 70, W, 1, ed);

        // Streetlamp glows
        float scroll = (f * 1.5f) % 200;
        for (int i = -1; i < 6; i++)
        {
            float lx = i * 200 - scroll;
            // Pole
            using var pl = new SKPaint { Color = new SKColor(0x2A, 0x2A, 0x35) };
            canvas.DrawRect(lx, H - 90, 2, 20, pl);
            // Lamp
            using var bb = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40) };
            canvas.DrawCircle(lx + 1, H - 90, 3, bb);
            // Halo
            using var h2 = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40, 50) };
            canvas.DrawCircle(lx + 1, H - 88, 14, h2);
        }
    }

    static void DrawSuitPipe(SKCanvas canvas, Pipe pipe)
    {
        float x = pipe.X;
        float gapTop = pipe.GapY;
        float gapBot = pipe.GapY + pipe.GapH;
        float pw = 95;

        // Top suit
        DrawOneSuit(canvas, x, 0, pw, gapTop, true, (int)(pipe.X / 200) % 3 == 0);
        // Bottom suit
        DrawOneSuit(canvas, x, gapBot, pw, H - 70 - gapBot, false, (int)(pipe.X / 200) % 3 == 0);
    }

    static void DrawOneSuit(SKCanvas canvas, float x, float y, float w, float h, bool isTop, bool showLogo)
    {
        if (h <= 0) return;
        using (var body = new SKPaint { IsAntialias = false, Color = new SKColor(0x2A, 0x2A, 0x35) })
            canvas.DrawRect(x, y, w, h, body);
        // Side highlight
        using (var hl = new SKPaint { Color = new SKColor(0x3A, 0x3A, 0x45) })
            canvas.DrawRect(x, y, 6, h, hl);
        using (var sh = new SKPaint { Color = new SKColor(0x1A, 0x1A, 0x22) })
            canvas.DrawRect(x + w - 6, y, 6, h, sh);

        // Mouth border (cuff at gap)
        float cuffY = isTop ? y + h - 15 : y;
        using (var cuff = new SKPaint { Color = new SKColor(0xE8, 0xE8, 0xE8) })
            canvas.DrawRect(x - 4, cuffY, w + 8, 15, cuff);
        using (var cuffEd = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90) })
            canvas.DrawRect(x - 4, isTop ? cuffY + 14 : cuffY, w + 8, 1, cuffEd);

        // Lapel V (inset)
        using (var lap = new SKPaint { IsAntialias = true, Color = new SKColor(0x3A, 0x3A, 0x45) })
        {
            using var path = new SKPath();
            if (isTop)
            {
                // V opens downward at bottom of column (toward gap)
                path.MoveTo(x + 5, y + h - 50);
                path.LineTo(x + w / 2, y + h - 18);
                path.LineTo(x + w - 5, y + h - 50);
                path.LineTo(x + w - 18, y + h - 50);
                path.LineTo(x + w / 2, y + h - 28);
                path.LineTo(x + 18, y + h - 50);
                path.Close();
            }
            else
            {
                // V opens upward at top of column (toward gap)
                path.MoveTo(x + 5, y + 50);
                path.LineTo(x + w / 2, y + 18);
                path.LineTo(x + w - 5, y + 50);
                path.LineTo(x + w - 18, y + 50);
                path.LineTo(x + w / 2, y + 28);
                path.LineTo(x + 18, y + 50);
                path.Close();
            }
            canvas.DrawPath(path, lap);
        }

        // Tie down center
        using (var tie = new SKPaint { Color = new SKColor(0xC4, 0x00, 0x20) })
        {
            float tieX = x + w / 2 - 4;
            float tieY1 = isTop ? y : y + 18;
            float tieY2 = isTop ? y + h - 18 : y + h;
            canvas.DrawRect(tieX, tieY1, 8, tieY2 - tieY1, tie);
        }
        // Tie shadow
        using (var ts = new SKPaint { Color = new SKColor(0x80, 0x00, 0x10) })
        {
            float tieX = x + w / 2 + 2;
            float tieY1 = isTop ? y : y + 18;
            float tieY2 = isTop ? y + h - 18 : y + h;
            canvas.DrawRect(tieX, tieY1, 2, tieY2 - tieY1, ts);
        }

        // Buttons
        using (var bt = new SKPaint { IsAntialias = true, Color = new SKColor(0x10, 0x10, 0x14) })
        {
            if (isTop)
            {
                canvas.DrawCircle(x + w / 2, y + h - 70, 2, bt);
                if (h > 130) canvas.DrawCircle(x + w / 2, y + h - 100, 2, bt);
            }
            else
            {
                canvas.DrawCircle(x + w / 2, y + 70, 2, bt);
                if (h > 130) canvas.DrawCircle(x + w / 2, y + 100, 2, bt);
            }
        }

        // Logo for some
        if (showLogo && h > 100)
        {
            using var lg = new SKPaint { IsAntialias = true, Color = new SKColor(0x5A, 0x5A, 0x6A), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 11, TextAlign = SKTextAlign.Center };
            canvas.DrawText("GOLIATH", x + w / 2, isTop ? y + h - 90 : y + 90, lg);
        }
    }

    static void DrawSpeedLines(SKCanvas canvas, int f, bool flap)
    {
        var rngLocal = new Random(f / 4);
        int n = flap ? 28 : 20;
        for (int i = 0; i < n; i++)
        {
            float y = (float)rngLocal.NextDouble() * H;
            float len = 80 + (float)rngLocal.NextDouble() * 170;
            float x = (float)rngLocal.NextDouble() * W;
            byte alpha = (byte)(30 + rngLocal.Next(0, 60));
            using var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40, alpha), StrokeWidth = 1.4f };
            canvas.DrawLine(x, y, x + len, y, p);
        }
    }

    static void UpdateAndDrawParticles(SKCanvas canvas)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.X += p.Vx;
            p.Y += p.Vy;
            p.Vy += 0.15f;
            p.Life--;
            float t = p.Life / (float)p.MaxLife;
            byte a = (byte)(255 * t);
            using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(p.Color.Red, p.Color.Green, p.Color.Blue, a) };
            canvas.DrawCircle(p.X, p.Y, p.Size, paint);
            if (p.Life <= 0) particles.RemoveAt(i);
        }
    }

    static void UpdateAndDrawCoins(SKCanvas canvas)
    {
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            c.X += c.Vx;
            c.Y += c.Vy;
            c.Vy += 0.25f;
            c.Life--;
            byte a = (byte)(255 * (c.Life / 12f));
            using var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40, a) };
            canvas.DrawCircle(c.X, c.Y, 4, p);
            using var p2 = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xF0, 0x80, (byte)(a / 2)) };
            canvas.DrawCircle(c.X - 1, c.Y - 1, 2, p2);
            if (c.Life <= 0) coins.RemoveAt(i);
        }
    }

    static void DrawHud(SKCanvas canvas)
    {
        // Score panel
        using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 120) })
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(20, 20, 350, 65), 6, 6), sh);

        using (var lbl = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xD0, 0x40), Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), TextSize = 28 })
        {
            using var sh2 = new SKPaint { IsAntialias = true, Color = new SKColor(0x04, 0x0E, 0x1E), Typeface = lbl.Typeface, TextSize = 28 };
            canvas.DrawText($"DISRUPTIONS: {score}", 32 + 2, 55 + 2, sh2);
            canvas.DrawText($"DISRUPTIONS: {score}", 32, 55, lbl);
        }

        if (hudFlashFrames > 0)
        {
            byte a = (byte)(80 * hudFlashFrames / 8f);
            using var fl = new SKPaint { Color = new SKColor(0xFF, 0xD0, 0x40, a) };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(20, 20, 350, 65), 6, 6), fl);
        }
    }

    static void DrawVignette(SKCanvas canvas)
    {
        using var p = new SKPaint();
        p.Shader = SKShader.CreateRadialGradient(
            new SKPoint(W / 2f, H / 2f), W * 0.7f,
            new[] { new SKColor(0, 0, 0, 0), new SKColor(0x04, 0x0E, 0x1E, 160) },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, W, H, p);
        // teal edge
        using var p2 = new SKPaint { Color = new SKColor(0x10, 0x40, 0x40, 30) };
        canvas.DrawRect(0, 0, W, 4, p2);
        canvas.DrawRect(0, H - 4, W, 4, p2);
        canvas.DrawRect(0, 0, 4, H, p2);
        canvas.DrawRect(W - 4, 0, 4, H, p2);
    }

    static void SpawnInitialPipes()
    {
        for (int i = 0; i < 4; i++)
        {
            pipes.Add(new Pipe { X = 700 + i * 220, GapY = 150 + (float)rng.NextDouble() * 220, GapH = 170 });
        }
    }
}
