using SkiaSharp;
using System;
using System.IO;

namespace FlappyBrainTheme4R;

internal static class Program
{
    private const int Width = 800;
    private const int Height = 600;
    private const int Fps = 30;
    private const int TotalFrames = 900;
    private const int TitleEnd = 60;
    private const int GameOverStart = 840;
    private const string OutDir = "/tmp/fb-t4r-frames";
    private const string BirdAsset = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    private const string BgAsset = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";

    private static readonly int[] FlapFrames = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    private static SKBitmap _birdBmp = null!;
    private static SKBitmap _bgBmp = null!;
    private static readonly Random Rng = new(73);

    private static void Main()
    {
        Directory.CreateDirectory(OutDir);

        _birdBmp = SKBitmap.Decode(BirdAsset) ?? throw new Exception("Failed to load bird asset: " + BirdAsset);
        _bgBmp = SKBitmap.Decode(BgAsset) ?? throw new Exception("Failed to load bg asset: " + BgAsset);

        // Pre-generate stable random fields
        var rubble = new (float x, float y, float w, float h, byte shade)[180];
        for (int i = 0; i < rubble.Length; i++)
        {
            rubble[i] = (
                (float)Rng.NextDouble() * Width,
                Height - 70 + (float)Rng.NextDouble() * 70,
                3 + (float)Rng.NextDouble() * 8,
                2 + (float)Rng.NextDouble() * 4,
                (byte)Rng.Next(20, 60)
            );
        }

        var farBuildings = new (float x, float w, float h, int jagSeed)[14];
        for (int i = 0; i < farBuildings.Length; i++)
        {
            farBuildings[i] = (
                i * 70f - 30f + (float)Rng.NextDouble() * 20f,
                40 + (float)Rng.NextDouble() * 50,
                120 + (float)Rng.NextDouble() * 200,
                Rng.Next()
            );
        }

        var midHulks = new (float x, float w, float h)[10];
        for (int i = 0; i < midHulks.Length; i++)
        {
            midHulks[i] = (
                i * 100f + (float)Rng.NextDouble() * 40f,
                50 + (float)Rng.NextDouble() * 60,
                70 + (float)Rng.NextDouble() * 100
            );
        }

        var dust = new (float x, float y, float r, byte a, float vx)[80];
        for (int i = 0; i < dust.Length; i++)
        {
            dust[i] = (
                (float)Rng.NextDouble() * Width,
                (float)Rng.NextDouble() * Height,
                2 + (float)Rng.NextDouble() * 2,
                (byte)Rng.Next(50, 105),
                0.5f + (float)Rng.NextDouble() * 1.8f
            );
        }

        var speedLines = new (float y, float len, byte a)[15];
        for (int i = 0; i < speedLines.Length; i++)
        {
            speedLines[i] = (
                (float)Rng.NextDouble() * Height,
                60 + (float)Rng.NextDouble() * 120,
                (byte)Rng.Next(50, 115)
            );
        }

        // Pipes — every 3rd has zombies
        var pipeSpacing = 280f;
        var pipeCount = 30;
        var pipes = new (float xBase, float gapY, int idx)[pipeCount];
        for (int i = 0; i < pipeCount; i++)
        {
            pipes[i] = (
                i * pipeSpacing + 900f,
                180 + (float)Rng.NextDouble() * 200f,
                i
            );
        }

        // Bird kinematics
        float birdX = 220f;
        float birdY = 300f;
        float birdVy = 0f;
        const float gravity = 0.42f;
        const float flapImpulse = -7.8f;

        int score = 0;
        int lastFlapFrame = -100;

        for (int f = 0; f < TotalFrames; f++)
        {
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            // BG asset stretched
            canvas.DrawBitmap(_bgBmp, new SKRect(0, 0, Width, Height));

            // Gradient overlay 65%
            using (var paint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(0, Height),
                    new[] { new SKColor(0x1A, 0x0F, 0x08), new SKColor(0x6B, 0x30, 0x20), new SKColor(0xC4, 0x62, 0x2D) },
                    new float[] { 0f, 0.55f, 1f },
                    SKShaderTileMode.Clamp)
            })
            {
                paint.Color = paint.Color.WithAlpha(166);
                canvas.DrawRect(0, 0, Width, Height, paint);
            }

            // Distant ruined skyscrapers (parallax 0.15)
            float farOffset = -(f * 0.15f * 2f) % 1000f;
            using (var paint = new SKPaint { Color = new SKColor(0x2A, 0x15, 0x08), IsAntialias = true })
            {
                foreach (var b in farBuildings)
                {
                    float bx = b.x + farOffset;
                    while (bx < -100) bx += 1000f;
                    float topY = Height - 70 - b.h;
                    canvas.DrawRect(bx, topY, b.w, b.h, paint);
                    // jagged broken top
                    var rng = new Random(b.jagSeed);
                    using var path = new SKPath();
                    path.MoveTo(bx, topY);
                    int teeth = 5;
                    for (int t = 0; t <= teeth; t++)
                    {
                        float tx = bx + (b.w * t / teeth);
                        float ty = topY - (float)rng.NextDouble() * 18f;
                        path.LineTo(tx, ty);
                    }
                    path.LineTo(bx + b.w, topY);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
            }

            // Mid-ground crumbling walls / hulks (parallax 0.25)
            float midOffset = -(f * 0.25f * 2f) % 1000f;
            using (var paint = new SKPaint { Color = new SKColor(0x4A, 0x28, 0x18), IsAntialias = true })
            {
                foreach (var h in midHulks)
                {
                    float hx = h.x + midOffset;
                    while (hx < -120) hx += 1000f;
                    canvas.DrawRect(hx, Height - 70 - h.h, h.w, h.h, paint);
                    // rust streaks
                    using var rust = new SKPaint { Color = new SKColor(0x6B, 0x30, 0x10), StrokeWidth = 2 };
                    canvas.DrawLine(hx + 5, Height - 70 - h.h + 10, hx + 5, Height - 70, rust);
                    canvas.DrawLine(hx + h.w - 8, Height - 70 - h.h + 20, hx + h.w - 8, Height - 70, rust);
                }
            }

            // Billboards (mid 0.5)
            DrawBillboards(canvas, f);

            // Pipes
            DrawPipes(canvas, pipes, f);

            // Ash rubble ground
            using (var paint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, Height - 70),
                    new SKPoint(0, Height),
                    new[] { new SKColor(0x3A, 0x20, 0x10), new SKColor(0x1A, 0x0A, 0x05) },
                    SKShaderTileMode.Clamp)
            })
            {
                canvas.DrawRect(0, Height - 70, Width, 70, paint);
            }
            // rubble bits
            foreach (var r in rubble)
            {
                using var paint = new SKPaint { Color = new SKColor(r.shade, (byte)(r.shade - 8), (byte)(r.shade - 12)) };
                canvas.DrawRect(r.x, r.y, r.w, r.h, paint);
            }

            // Speed lines (only during gameplay)
            if (f >= TitleEnd && f < GameOverStart)
            {
                using var paint = new SKPaint { Color = new SKColor(0xC4, 0x62, 0x2D), StrokeWidth = 2, IsAntialias = true };
                for (int i = 0; i < speedLines.Length; i++)
                {
                    var s = speedLines[i];
                    float baseX = (f * 12f + i * 53f) % (Width + 200) - 100;
                    paint.Color = new SKColor(0xC4, 0x62, 0x2D, s.a);
                    canvas.DrawLine(baseX, s.y, baseX + s.len, s.y, paint);
                }
            }

            // Red dust particles
            using (var paint = new SKPaint { IsAntialias = true })
            {
                for (int i = 0; i < dust.Length; i++)
                {
                    var d = dust[i];
                    float x = (d.x - f * d.vx) % (Width + 20);
                    if (x < -10) x += Width + 20;
                    paint.Color = new SKColor(0xD4, 0x95, 0x6A, d.a);
                    canvas.DrawCircle(x, d.y, d.r, paint);
                }
            }

            // Update bird physics during gameplay
            if (f >= TitleEnd && f < GameOverStart)
            {
                bool flapped = false;
                foreach (var ff in FlapFrames)
                {
                    if (ff == f) { birdVy = flapImpulse; lastFlapFrame = f; flapped = true; break; }
                }
                _ = flapped;
                birdVy += gravity;
                birdY += birdVy;
                if (birdY < 90) { birdY = 90; birdVy = 0; }
                if (birdY > Height - 110) { birdY = Height - 110; birdVy = 0; }

                // Score increments approx every 60 frames after first pipe pass
                if (f > 100 && (f - 100) % 60 == 0) score++;
            }

            // Flap glow
            int sinceFlap = f - lastFlapFrame;
            if (sinceFlap >= 0 && sinceFlap < 12 && f >= TitleEnd && f < GameOverStart)
            {
                float t = sinceFlap / 12f;
                byte alpha = (byte)(64 * (1f - t));
                using var paint = new SKPaint { Color = new SKColor(0xC4, 0x62, 0x2D, alpha), IsAntialias = true };
                canvas.DrawCircle(birdX, birdY, 85, paint);
            }

            // Bird with rotation
            if (f >= TitleEnd)
            {
                float rotation = Math.Clamp(birdVy * 3.2f, -22f, 35f);
                if (f >= GameOverStart) rotation = 60f;
                canvas.Save();
                canvas.Translate(birdX, birdY);
                canvas.RotateDegrees(rotation);
                var dest = new SKRect(-80, -65, 80, 65);
                canvas.DrawBitmap(_birdBmp, dest);
                canvas.Restore();
            }

            // Vignette
            DrawVignette(canvas);

            // HUD score (gameplay only)
            if (f >= TitleEnd && f < GameOverStart)
            {
                DrawScore(canvas, score);
            }

            // Title card
            if (f < TitleEnd)
            {
                DrawTitleCard(canvas, f);
            }

            // Game over
            if (f >= GameOverStart)
            {
                DrawGameOver(canvas, score);
            }

            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.Create(Path.Combine(OutDir, $"frame_{f:D4}.png"));
            data.SaveTo(fs);

            if (f % 60 == 0) Console.WriteLine($"frame {f}/{TotalFrames}");
        }
        Console.WriteLine("All frames rendered.");
    }

    private static void DrawPipes(SKCanvas canvas, (float xBase, float gapY, int idx)[] pipes, int frame)
    {
        const float scrollSpeed = 2.6f;
        const float gapSize = 175f;
        const int pipeWidth = 80;
        const int capHeight = 28;
        const int groundY = Height - 70;

        foreach (var p in pipes)
        {
            float x = p.xBase - frame * scrollSpeed;
            if (x < -pipeWidth - 20 || x > Width + 20) continue;

            float topPipeBottom = p.gapY - gapSize / 2f;
            float bottomPipeTop = p.gapY + gapSize / 2f;

            // Top pipe
            DrawPipeBody(canvas, x, 0, pipeWidth, topPipeBottom);
            // Top cap
            DrawPipeCap(canvas, x, topPipeBottom - capHeight, pipeWidth, capHeight);
            // Bottom pipe
            DrawPipeBody(canvas, x, bottomPipeTop, pipeWidth, groundY - bottomPipeTop);
            // Bottom cap
            DrawPipeCap(canvas, x, bottomPipeTop, pipeWidth, capHeight);

            // Zombies every 3rd pipe pair
            if (p.idx % 3 == 2)
            {
                DrawZombie(canvas, x + 8, topPipeBottom - 60);
                DrawZombie(canvas, x + pipeWidth - 32, bottomPipeTop + 30);
            }
        }
    }

    private static void DrawPipeBody(SKCanvas canvas, float x, float y, float w, float h)
    {
        if (h <= 0) return;
        using var body = new SKPaint { Color = new SKColor(0x5A, 0x30, 0x10) };
        canvas.DrawRect(x, y, w, h, body);
        using var stripe = new SKPaint { Color = new SKColor(0x6B, 0x40, 0x20) };
        for (float yy = y + 6; yy < y + h; yy += 12)
        {
            canvas.DrawRect(x, yy, w, 2, stripe);
        }
        // edge shadow
        using var shadow = new SKPaint { Color = new SKColor(0x2A, 0x14, 0x08, 180) };
        canvas.DrawRect(x, y, 4, h, shadow);
        canvas.DrawRect(x + w - 4, y, 4, h, shadow);
    }

    private static void DrawPipeCap(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var cap = new SKPaint { Color = new SKColor(0x4A, 0x28, 0x08) };
        canvas.DrawRect(x - 8, y, w + 16, h, cap);
        using var hl = new SKPaint { Color = new SKColor(0x6B, 0x40, 0x20) };
        canvas.DrawRect(x - 8, y + 2, w + 16, 2, hl);
    }

    private static void DrawZombie(SKCanvas canvas, float x, float y)
    {
        using var paint = new SKPaint { Color = new SKColor(0x2A, 0x15, 0x08), IsAntialias = true };
        // head oval 20px
        canvas.DrawOval(x + 10, y, 8, 10, paint);
        // torso 15x30
        canvas.DrawRect(x + 5, y + 10, 15, 30, paint);
        // arms outstretched 30x8
        canvas.DrawRect(x - 12, y + 14, 30, 6, paint);
        // stub legs
        canvas.DrawRect(x + 6, y + 40, 4, 10, paint);
        canvas.DrawRect(x + 14, y + 40, 4, 10, paint);
    }

    private static void DrawBillboards(SKCanvas canvas, int frame)
    {
        // Two billboards alternating
        var boards = new (float baseX, string text, SKColor textColor, int size, float tilt)[]
        {
            (300f, "PATIENT ZERO", new SKColor(0xCC, 0x20, 0x10), 38, -4f),
            (1100f, "10,000 SPOONS", new SKColor(0xE8, 0xC4, 0x40), 34, 4f),
            (1900f, "PATIENT ZERO", new SKColor(0xCC, 0x20, 0x10), 38, -4f),
            (2700f, "10,000 SPOONS", new SKColor(0xE8, 0xC4, 0x40), 34, 4f),
        };

        const float parallax = 0.5f;
        float cycle = 3500f;
        foreach (var b in boards)
        {
            float x = (b.baseX - frame * parallax * 2f) % cycle;
            while (x < -300) x += cycle;
            DrawBillboard(canvas, x, 130, b.text, b.textColor, b.size, b.tilt);
        }
    }

    private static void DrawBillboard(SKCanvas canvas, float cx, float cy, string text, SKColor textColor, int textSize, float tiltDeg)
    {
        if (cx < -200 || cx > Width + 200) return;
        const float panelW = 260, panelH = 90;
        // Pole
        using (var pole = new SKPaint { Color = new SKColor(0x5A, 0x35, 0x18) })
        {
            canvas.DrawRect(cx - 4, cy + panelH / 2f, 8, Height - cy - panelH / 2f, pole);
        }

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(tiltDeg);

        // Panel bg
        using (var bg = new SKPaint { Color = new SKColor(0x12, 0x12, 0x12, 217), IsAntialias = true })
        {
            canvas.DrawRect(-panelW / 2f, -panelH / 2f, panelW, panelH, bg);
        }
        // Border
        using (var border = new SKPaint { Color = new SKColor(0x8B, 0x45, 0x13), StrokeWidth = 3, Style = SKPaintStyle.Stroke, IsAntialias = true })
        {
            canvas.DrawRect(-panelW / 2f, -panelH / 2f, panelW, panelH, border);
        }
        // Text shadow
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 89), TextSize = textSize, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) })
        {
            canvas.DrawText(text, 2, 12 + 2, shadow);
        }
        // Text main
        using (var t = new SKPaint { Color = textColor, TextSize = textSize, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) })
        {
            canvas.DrawText(text, 0, 12, t);
        }
        // Cracks 2 diagonal
        using (var crack = new SKPaint { Color = new SKColor(255, 255, 255, 51), StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawLine(-panelW / 2f + 30, -panelH / 2f + 5, panelW / 2f - 80, panelH / 2f - 10, crack);
        }
        using (var crack2 = new SKPaint { Color = new SKColor(255, 255, 255, 51), StrokeWidth = 1f, IsAntialias = true })
        {
            canvas.DrawLine(panelW / 2f - 40, -panelH / 2f + 8, -panelW / 2f + 60, panelH / 2f - 5, crack2);
        }
        // Grime strips horizontal
        using (var grime = new SKPaint { Color = new SKColor(0, 0, 0, 38) })
        {
            canvas.DrawRect(-panelW / 2f, -panelH / 2f + 20, panelW, 6, grime);
            canvas.DrawRect(-panelW / 2f, panelH / 2f - 25, panelW, 5, grime);
        }
        canvas.Restore();
    }

    private static void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(Width / 2f, Height / 2f),
                Width * 0.7f,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 180) },
                new float[] { 0.55f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, Width, Height, paint);
    }

    private static void DrawScore(SKCanvas canvas, int score)
    {
        using var shadow = new SKPaint { Color = SKColors.Black, TextSize = 28, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var t = new SKPaint { Color = new SKColor(0xF2, 0xD5, 0xA0), TextSize = 28, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText($"SCORE: {score}", 22, 42, shadow);
        canvas.DrawText($"SCORE: {score}", 20, 40, t);
    }

    private static void DrawTitleCard(SKCanvas canvas, int frame)
    {
        using (var overlay = new SKPaint { Color = new SKColor(0, 0, 0, 178) })
        {
            canvas.DrawRect(0, 0, Width, Height, overlay);
        }
        using var titleShadow = new SKPaint { Color = SKColors.Black, TextSize = 56, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var title = new SKPaint { Color = new SKColor(0xC4, 0x62, 0x2D), TextSize = 56, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText("POST-APOCALYPTIC", Width / 2f + 3, 270 + 3, titleShadow);
        canvas.DrawText("POST-APOCALYPTIC", Width / 2f, 270, title);

        using var sub = new SKPaint { Color = new SKColor(0xF2, 0xD5, 0xA0), TextSize = 30, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText("FlappyBrain 🧠", Width / 2f, 320, sub);
    }

    private static void DrawGameOver(SKCanvas canvas, int score)
    {
        using (var overlay = new SKPaint { Color = new SKColor(0, 0, 0, 178) })
        {
            canvas.DrawRect(0, 0, Width, Height, overlay);
        }
        using var titleShadow = new SKPaint { Color = SKColors.Black, TextSize = 80, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        using var title = new SKPaint { Color = new SKColor(0xCC, 0x20, 0x10), TextSize = 80, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText("GAME OVER", Width / 2f + 4, 280 + 4, titleShadow);
        canvas.DrawText("GAME OVER", Width / 2f, 280, title);

        using var sub = new SKPaint { Color = new SKColor(0xF2, 0xD5, 0xA0), TextSize = 36, IsAntialias = true, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
        canvas.DrawText($"FINAL SCORE: {score}", Width / 2f, 340, sub);
    }
}
