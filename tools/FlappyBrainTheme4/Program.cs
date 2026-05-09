using SkiaSharp;
using System;
using System.IO;

namespace FlappyBrainTheme4;

public static class Program
{
    const int W = 800, H = 600, FPS = 30, FRAMES = 900;
    const int GROUND_H = 70;
    const string OUT = "/tmp/fb-t4-frames";
    const string BIRD_PATH = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    const string BG_PATH = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";

    static SKBitmap birdBmp = null!;
    static SKBitmap bgBmp = null!;
    static Random rng = new Random(7);

    // Particles
    struct Particle { public float x, y, vx, vy, size, alpha; }
    static Particle[] particles = new Particle[80];

    // Pipe definition
    struct PipePair { public int frame; public float x; public int gapY; public int gapH; public bool zombies; public string? billboardText; public float billboardTilt; }

    public static int Main()
    {
        Directory.CreateDirectory(OUT);

        birdBmp = SKBitmap.Decode(BIRD_PATH) ?? throw new Exception("Bird asset failed to load");
        bgBmp = SKBitmap.Decode(BG_PATH) ?? throw new Exception("BG asset failed to load");

        // Init particles
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i] = new Particle
            {
                x = (float)rng.NextDouble() * W,
                y = (float)rng.NextDouble() * (H - GROUND_H),
                vx = -(0.5f + (float)rng.NextDouble() * 1.5f),
                vy = -0.1f + (float)rng.NextDouble() * 0.2f,
                size = 2 + (float)rng.NextDouble() * 2,
                alpha = 0.2f + (float)rng.NextDouble() * 0.2f
            };
        }

        // Bird physics
        int[] flapFrames = new[] { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };
        float birdY = H / 2f;
        float birdVy = 0;
        const float gravity = 0.45f;
        const float flapImpulse = -7.5f;
        int lastFlapFrame = -100;

        // Pipes: scroll. Build a list of pipe pairs spawned through gameplay.
        // Pipe spawning: every 90 frames during gameplay
        var pipes = new System.Collections.Generic.List<PipePair>();
        int pipeIndex = 0;
        int score = 0;
        string[] billboardTexts = { "PATIENT ZERO", "10,000 SPOONS" };

        for (int f = 0; f < FRAMES; f++)
        {
            // Update bird (gameplay phase)
            bool gameplay = f >= 60 && f < 840;
            if (gameplay)
            {
                if (Array.IndexOf(flapFrames, f) >= 0)
                {
                    birdVy = flapImpulse;
                    lastFlapFrame = f;
                }
                birdVy += gravity;
                birdY += birdVy;
                if (birdY < 60) { birdY = 60; birdVy = 0; }
                if (birdY > H - GROUND_H - 60) { birdY = H - GROUND_H - 60; birdVy = 0; }
            }
            else if (f < 60)
            {
                // Title - bird hovers
                birdY = H / 2f + (float)Math.Sin(f * 0.15) * 8;
                birdVy = 0;
            }
            else
            {
                // Game over - bird falls
                birdVy += gravity * 1.4f;
                birdY += birdVy;
                if (birdY > H - GROUND_H - 60) { birdY = H - GROUND_H - 60; birdVy = 0; }
            }

            // Spawn pipes during gameplay
            if (gameplay && (f - 60) % 90 == 0)
            {
                int gapH = 170;
                int gapY = 140 + rng.Next(0, 200);
                pipeIndex++;
                bool zombies = pipeIndex % 3 == 0;
                string? bb = (pipeIndex % 3 == 1) ? billboardTexts[pipeIndex % billboardTexts.Length] : null;
                pipes.Add(new PipePair
                {
                    frame = f,
                    x = W + 60,
                    gapY = gapY,
                    gapH = gapH,
                    zombies = zombies,
                    billboardText = bb,
                    billboardTilt = (float)((rng.NextDouble() - 0.5) * 8)
                });
            }

            using var surface = SKSurface.Create(new SKImageInfo(W, H));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            // 1. BG asset stretched
            canvas.DrawBitmap(bgBmp, new SKRect(0, 0, W, H));

            // 2. Sky gradient overlay at 65% opacity
            using (var grad = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, H - GROUND_H),
                new[] { new SKColor(0x1A, 0x0F, 0x08), new SKColor(0x6B, 0x30, 0x20), new SKColor(0xC4, 0x62, 0x2D) },
                new[] { 0f, 0.55f, 1f }, SKShaderTileMode.Clamp))
            using (var p = new SKPaint { Shader = grad, Color = SKColors.White.WithAlpha((byte)(255 * 0.65)) })
            {
                p.BlendMode = SKBlendMode.SrcOver;
                canvas.DrawRect(0, 0, W, H, p);
            }

            // 3. Far background — ruined city silhouette (parallax 0.15x)
            DrawFarCity(canvas, f * 0.15f);

            // 4. Mid background — crumbling walls + vehicles (parallax 0.25x)
            DrawMidBg(canvas, f * 0.25f);

            // 5. Billboards (parallax 0.5x) — drawn before pipes
            DrawBillboards(canvas, pipes, f);

            // 6. Pipes
            DrawPipes(canvas, pipes, f);

            // 7. Ground
            DrawGround(canvas, f);

            // 8. Particles
            UpdateAndDrawParticles(canvas);

            // 9. Speed lines
            DrawSpeedLines(canvas, f);

            // 10. Bird
            DrawBird(canvas, birdY, birdVy, f, lastFlapFrame);

            // 11. Vignette
            DrawVignette(canvas);

            // 12. Score HUD (during gameplay)
            if (gameplay)
            {
                // Update score: each pipe past x=200 increments
                int s = 0;
                foreach (var pp in pipes)
                {
                    float speed = 3f;
                    float px = pp.x - (f - pp.frame) * speed;
                    if (px < 200) s++;
                }
                score = s;
                DrawScoreHud(canvas, score);
            }

            // 13. Title card
            if (f < 60)
            {
                DrawTitleCard(canvas, f);
            }
            else if (f >= 840)
            {
                DrawGameOver(canvas, f - 840);
            }

            // Save
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 95);
            using var fs = File.OpenWrite(Path.Combine(OUT, $"frame_{f:D4}.png"));
            data.SaveTo(fs);

            if (f % 60 == 0) Console.WriteLine($"Frame {f}/{FRAMES}");
        }

        Console.WriteLine("All frames rendered.");
        return 0;
    }

    static void DrawFarCity(SKCanvas canvas, float scroll)
    {
        using var paint = new SKPaint { Color = new SKColor(0x2A, 0x15, 0x08), IsAntialias = false };
        // Repeating skyline
        int baseY = H - GROUND_H - 100;
        var rand = new Random(42);
        for (int repeat = 0; repeat < 3; repeat++)
        {
            float ox = repeat * 800 - (scroll % 800);
            for (int i = 0; i < 18; i++)
            {
                int bw = 30 + rand.Next(40);
                int bh = 50 + rand.Next(120);
                float bx = ox + i * 48;
                int top = baseY + 80 - bh;
                // jagged top: draw two segments
                canvas.DrawRect(bx, top, bw, bh, paint);
                // missing section (subtract a notch) -- draw a sky-colored rect instead, but easier: draw a darker hole
                if (rand.Next(3) == 0)
                {
                    using var hole = new SKPaint { Color = new SKColor(0x6B, 0x30, 0x20).WithAlpha(180) };
                    canvas.DrawRect(bx + bw * 0.3f, top + bh * 0.3f, bw * 0.4f, bh * 0.3f, hole);
                }
                // jagged top peak
                using var path = new SKPath();
                path.MoveTo(bx, top);
                path.LineTo(bx + bw * 0.2f, top - 8);
                path.LineTo(bx + bw * 0.5f, top);
                path.LineTo(bx + bw * 0.7f, top - 5);
                path.LineTo(bx + bw, top);
                path.Close();
                canvas.DrawPath(path, paint);
            }
        }
    }

    static void DrawMidBg(SKCanvas canvas, float scroll)
    {
        using var wallPaint = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x10) };
        using var darkPaint = new SKPaint { Color = new SKColor(0x2A, 0x15, 0x08) };
        int baseY = H - GROUND_H - 50;
        var rand = new Random(91);
        for (int repeat = 0; repeat < 3; repeat++)
        {
            float ox = repeat * 700 - (scroll % 700);
            for (int i = 0; i < 8; i++)
            {
                float bx = ox + i * 90;
                // Crumbling wall
                int wh = 40 + rand.Next(40);
                canvas.DrawRect(bx, baseY - wh, 50, wh, wallPaint);
                // notch
                canvas.DrawRect(bx + 30, baseY - wh, 12, 15, darkPaint);

                // Abandoned vehicle every other position
                if (i % 2 == 0)
                {
                    float vx = bx + 55;
                    float vy = baseY - 18;
                    using var carPaint = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x10) };
                    canvas.DrawRect(vx, vy, 32, 14, carPaint);
                    using var wheelPaint = new SKPaint { Color = new SKColor(0x1A, 0x0A, 0x05), IsAntialias = true };
                    canvas.DrawCircle(vx + 6, vy + 14, 4, wheelPaint);
                    canvas.DrawCircle(vx + 26, vy + 14, 4, wheelPaint);
                }
            }
        }
    }

    static void DrawBillboards(SKCanvas canvas, System.Collections.Generic.List<PipePair> pipes, int frame)
    {
        const float speed = 3f;
        foreach (var p in pipes)
        {
            if (p.billboardText == null) continue;
            // billboard parallax: drawn at 0.5x of pipe speed
            float pipeX = p.x - (frame - p.frame) * speed;
            float bx = pipeX; // tied to pipe roughly but offset
            float bWidth = 260, bHeight = 90;
            float bxLeft = bx - bWidth / 2;
            float by = 50;
            if (bxLeft + bWidth < -50 || bxLeft > W + 50) continue;

            // Poles
            using var polePaint = new SKPaint { Color = new SKColor(0x5A, 0x35, 0x18) };
            canvas.DrawRect(bxLeft + 30, by + bHeight, 8, H - by - bHeight, polePaint);
            canvas.DrawRect(bxLeft + bWidth - 38, by + bHeight, 8, H - by - bHeight, polePaint);

            canvas.Save();
            canvas.RotateDegrees(p.billboardTilt, bxLeft + bWidth / 2, by + bHeight / 2);

            // Panel bg
            using var bgPaint = new SKPaint { Color = new SKColor(0x12, 0x12, 0x12).WithAlpha((byte)(255 * 0.85)) };
            canvas.DrawRect(bxLeft, by, bWidth, bHeight, bgPaint);
            // Border
            using var borderPaint = new SKPaint { Color = new SKColor(0x8B, 0x45, 0x13), Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
            canvas.DrawRect(bxLeft, by, bWidth, bHeight, borderPaint);

            // Text
            bool isPatientZero = p.billboardText == "PATIENT ZERO";
            float fontSize = isPatientZero ? 38 : 34;
            SKColor textColor = isPatientZero ? new SKColor(0xCC, 0x20, 0x10) : new SKColor(0xE8, 0xC4, 0x40);
            using var fontPaint = new SKPaint
            {
                Color = textColor,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };
            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha((byte)(255 * 0.35)),
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };
            float tx = bxLeft + bWidth / 2;
            float ty = by + bHeight / 2 + fontSize / 3;
            canvas.DrawText(p.billboardText, tx + 2, ty + 2, shadowPaint);
            canvas.DrawText(p.billboardText, tx, ty, fontPaint);

            // Crack lines
            var rand = new Random((int)p.x);
            using var crackPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            int cracks = 2 + rand.Next(2);
            for (int c = 0; c < cracks; c++)
            {
                float a = 0.18f + (float)rand.NextDouble() * 0.10f;
                crackPaint.Color = SKColors.White.WithAlpha((byte)(255 * a));
                crackPaint.StrokeWidth = 1 + rand.Next(2);
                float x1 = bxLeft + (float)rand.NextDouble() * bWidth;
                float y1 = by + (float)rand.NextDouble() * bHeight;
                float x2 = x1 + (-30 + (float)rand.NextDouble() * 60);
                float y2 = y1 + (-20 + (float)rand.NextDouble() * 40);
                canvas.DrawLine(x1, y1, x2, y2, crackPaint);
            }

            // Grime strips
            using var grimePaint = new SKPaint { Color = SKColors.Black.WithAlpha(38) };
            canvas.DrawRect(bxLeft, by + bHeight * 0.3f, bWidth, 4, grimePaint);
            canvas.DrawRect(bxLeft, by + bHeight * 0.75f, bWidth, 5, grimePaint);

            canvas.Restore();
        }
    }

    static void DrawPipes(SKCanvas canvas, System.Collections.Generic.List<PipePair> pipes, int frame)
    {
        const float speed = 3f;
        const int pipeW = 70;
        foreach (var p in pipes)
        {
            float px = p.x - (frame - p.frame) * speed;
            if (px + pipeW < -50 || px > W + 50) continue;

            // Top pipe: from 0 to gapY
            DrawPipe(canvas, px, 0, pipeW, p.gapY, true, p.zombies);
            // Bottom pipe: from gapY+gapH to H-GROUND_H
            int botY = p.gapY + p.gapH;
            int botH = H - GROUND_H - botY;
            if (botH > 0)
                DrawPipe(canvas, px, botY, pipeW, botH, false, p.zombies);
        }
    }

    static void DrawPipe(SKCanvas canvas, float x, float y, float w, float h, bool flipCap, bool zombies)
    {
        if (h <= 0) return;
        using var bodyPaint = new SKPaint { Color = new SKColor(0x5A, 0x30, 0x10) };
        canvas.DrawRect(x, y, w, h, bodyPaint);

        // Corrugation stripes
        using var stripePaint = new SKPaint { Color = new SKColor(0x6B, 0x40, 0x20) };
        for (float sy = y; sy < y + h; sy += 12)
        {
            canvas.DrawRect(x, sy, w, 2, stripePaint);
        }

        // Cap
        using var capPaint = new SKPaint { Color = new SKColor(0x4A, 0x28, 0x08) };
        float capH = 22;
        if (flipCap)
        {
            // Cap at bottom of top pipe
            canvas.DrawRect(x - 8, y + h - capH, w + 16, capH, capPaint);
        }
        else
        {
            canvas.DrawRect(x - 8, y, w + 16, capH, capPaint);
        }

        // Zombies clinging
        if (zombies)
        {
            using var zombiePaint = new SKPaint { Color = new SKColor(0x2A, 0x15, 0x08), IsAntialias = true };
            // Two zombies on body
            for (int z = 0; z < 2; z++)
            {
                float zy = y + h * (z == 0 ? 0.3f : 0.6f);
                float zx = x + (z == 0 ? -10 : w - 5);
                // Head
                canvas.DrawOval(zx + 7, zy, 10, 10, zombiePaint);
                // Torso
                canvas.DrawRect(zx + 4, zy + 18, 15, 30, zombiePaint);
                // Arms
                canvas.DrawRect(zx - 25, zy + 22, 30, 8, zombiePaint);
                canvas.DrawRect(zx + 15, zy + 22, 30, 8, zombiePaint);
                // Legs
                canvas.DrawRect(zx + 4, zy + 48, 5, 14, zombiePaint);
                canvas.DrawRect(zx + 14, zy + 48, 5, 14, zombiePaint);
            }
        }
    }

    static void DrawGround(SKCanvas canvas, int frame)
    {
        using var grad = SKShader.CreateLinearGradient(
            new SKPoint(0, H - GROUND_H), new SKPoint(0, H),
            new[] { new SKColor(0x3A, 0x20, 0x10), new SKColor(0x1A, 0x0A, 0x05) },
            SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = grad };
        canvas.DrawRect(0, H - GROUND_H, W, GROUND_H, p);

        // Rubble
        var rand = new Random(13);
        using var rubble = new SKPaint { Color = new SKColor(0x1A, 0x0A, 0x05) };
        for (int i = 0; i < 60; i++)
        {
            float rx = ((rand.Next(1200) - frame * 3) % 1200 + 1200) % 1200 - 50;
            float ry = H - GROUND_H + 5 + rand.Next(GROUND_H - 10);
            float rw = 4 + rand.Next(10);
            float rh = 2 + rand.Next(5);
            canvas.DrawRect(rx, ry, rw, rh, rubble);
        }
    }

    static void UpdateAndDrawParticles(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < particles.Length; i++)
        {
            ref var pt = ref particles[i];
            pt.x += pt.vx;
            pt.y += pt.vy;
            if (pt.x < -10) { pt.x = W + 10; pt.y = (float)rng.NextDouble() * (H - GROUND_H); }
            if (pt.y < 0) pt.y = H - GROUND_H;
            if (pt.y > H - GROUND_H) pt.y = 0;
            paint.Color = new SKColor(0xD4, 0x95, 0x6A).WithAlpha((byte)(255 * pt.alpha));
            canvas.DrawCircle(pt.x, pt.y, pt.size, paint);
        }
    }

    static void DrawSpeedLines(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = false };
        var rand = new Random(frame / 3);
        for (int i = 0; i < 15; i++)
        {
            float y = (float)rand.NextDouble() * (H - GROUND_H);
            float len = 60 + (float)rand.NextDouble() * 120;
            float a = 0.20f + (float)rand.NextDouble() * 0.25f;
            paint.Color = new SKColor(0xC4, 0x62, 0x2D).WithAlpha((byte)(255 * a));
            paint.StrokeWidth = 1.5f;
            canvas.DrawLine(0, y, len, y, paint);
        }
    }

    static void DrawBird(SKCanvas canvas, float birdY, float vy, int frame, int lastFlap)
    {
        const int bx = 200;
        // Flap glow
        int sinceFlap = frame - lastFlap;
        if (sinceFlap >= 0 && sinceFlap < 12)
        {
            float fade = 1f - sinceFlap / 12f;
            using var glow = new SKPaint
            {
                Color = new SKColor(0xC4, 0x62, 0x2D).WithAlpha((byte)(255 * 0.25f * fade)),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
            };
            canvas.DrawCircle(bx, birdY, 85, glow);
        }

        // Rotation based on velocity
        float angle = Math.Clamp(vy * 4f, -25, 65);
        canvas.Save();
        canvas.Translate(bx, birdY);
        canvas.RotateDegrees(angle);
        var dst = new SKRect(-80, -65, 80, 65);
        using var bp = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawBitmap(birdBmp, dst, bp);
        canvas.Restore();
    }

    static void DrawVignette(SKCanvas canvas)
    {
        using var grad = SKShader.CreateRadialGradient(
            new SKPoint(W / 2f, H / 2f), W * 0.7f,
            new[] { SKColors.Transparent, SKColors.Black.WithAlpha(140) },
            new[] { 0.55f, 1f }, SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = grad };
        canvas.DrawRect(0, 0, W, H, p);
    }

    static void DrawScoreHud(SKCanvas canvas, int score)
    {
        using var shadow = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 28,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
        };
        using var fg = new SKPaint
        {
            Color = new SKColor(0xF2, 0xD5, 0xA0),
            TextSize = 28,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
        };
        string txt = $"SCORE: {score}";
        canvas.DrawText(txt, 22, 42, shadow);
        canvas.DrawText(txt, 20, 40, fg);
    }

    static void DrawTitleCard(SKCanvas canvas, int f)
    {
        using var overlay = new SKPaint { Color = SKColors.Black.WithAlpha((byte)(255 * 0.7f)) };
        canvas.DrawRect(0, 0, W, H, overlay);

        using var title = new SKPaint
        {
            Color = new SKColor(0xC4, 0x62, 0x2D),
            TextSize = 56,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("POST-APOCALYPTIC", W / 2f, H / 2f - 10, title);

        using var sub = new SKPaint
        {
            Color = new SKColor(0xF2, 0xD5, 0xA0),
            TextSize = 30,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("FlappyBrain 🧠", W / 2f, H / 2f + 40, sub);
    }

    static void DrawGameOver(SKCanvas canvas, int sub)
    {
        using var overlay = new SKPaint { Color = SKColors.Black.WithAlpha((byte)(255 * Math.Min(0.7f, sub / 30f * 0.7f))) };
        canvas.DrawRect(0, 0, W, H, overlay);

        using var go = new SKPaint
        {
            Color = new SKColor(0xCC, 0x20, 0x10),
            TextSize = 80,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("GAME OVER", W / 2f, H / 2f, go);
    }
}
