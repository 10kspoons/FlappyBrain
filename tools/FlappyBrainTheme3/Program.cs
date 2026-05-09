using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme3;

internal static class Program
{
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL_FRAMES = 900;
    const int GROUND_H = 80;
    const string OUT = "/tmp/fb-t3-frames";

    static readonly int[] FlapFrames = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    record Pipe(float X, float GapY, float GapH, bool Scored);
    record Particle(float X, float Y, float Vx, float Vy, float Size, int Life, int MaxLife, byte Kind);
    record Smoke(float X, float Y, float Size, float Drift);

    static int Main()
    {
        Directory.CreateDirectory(OUT);

        // Bird state
        float birdX = 200f;
        float birdY = 280f;
        float birdVy = 0f;
        const float gravity = 0.45f;
        const float flapImpulse = -7.5f;

        var pipes = new List<Pipe>();
        var particles = new List<Particle>();
        var smokes = new List<Smoke>();
        var rng = new Random(42);

        int score = 0;
        float pipeTimer = 0f;
        float worldX = 0f; // for parallax
        bool dead = false;

        // Pre-seed smoke puffs
        for (int i = 0; i < 8; i++)
        {
            smokes.Add(new Smoke(
                100 + rng.Next(700),
                200 + rng.Next(200),
                10 + rng.Next(30),
                (float)rng.NextDouble() * 0.3f));
        }

        for (int frame = 0; frame < TOTAL_FRAMES; frame++)
        {
            using var bmp = new SKBitmap(W, H);
            using var canvas = new SKCanvas(bmp);

            // ----- Sky -----
            DrawSky(canvas);
            DrawCloudStreaks(canvas, frame);

            // ----- Background cogs (decorative, low opacity) -----
            DrawBackgroundCogs(canvas, frame);

            // ----- Far parallax: city skyline -----
            DrawFarSkyline(canvas, worldX);

            // ----- Mid parallax: chimney stacks -----
            DrawChimneyStacks(canvas, worldX, smokes, frame);

            // Update smokes
            for (int i = smokes.Count - 1; i >= 0; i--)
            {
                var s = smokes[i];
                float ny = s.Y - 0.6f;
                float nx = s.X - 0.4f - s.Drift;
                float nsz = s.Size + 0.4f;
                if (nsz > 60 || ny < 100)
                {
                    smokes[i] = new Smoke(
                        rng.Next(W) + 100,
                        320 + rng.Next(80),
                        10 + rng.Next(20),
                        (float)rng.NextDouble() * 0.3f);
                }
                else
                {
                    smokes[i] = new Smoke(nx, ny, nsz, s.Drift);
                }
            }

            // ----- Ground -----
            DrawGround(canvas);

            // ----- Title card (0-59) -----
            if (frame < 60)
            {
                // freeze bird, no pipes update
                DrawBird(canvas, birdX, birdY, frame, false);
                DrawForegroundCogs(canvas, frame);
                DrawVignette(canvas);
                DrawTitleCard(canvas, frame);
                SaveFrame(bmp, frame);
                continue;
            }

            // ----- Gameplay simulation (60-839) -----
            if (frame >= 60 && frame < 840)
            {
                // Auto-flap
                foreach (int f in FlapFrames)
                {
                    if (f == frame) birdVy = flapImpulse;
                }

                birdVy += gravity;
                birdY += birdVy;
                if (birdY < 30) { birdY = 30; birdVy = 0; }
                if (birdY > H - GROUND_H - 60) { birdY = H - GROUND_H - 60; birdVy = 0; }

                // Pipes
                pipeTimer += 1f;
                if (pipeTimer > 90f)
                {
                    pipeTimer = 0f;
                    float gapH = 180f;
                    float gapY = 120 + (float)rng.NextDouble() * (H - GROUND_H - gapH - 200);
                    pipes.Add(new Pipe(W + 50, gapY, gapH, false));
                }

                for (int i = pipes.Count - 1; i >= 0; i--)
                {
                    var p = pipes[i];
                    float nx = p.X - 3.5f;
                    bool scored = p.Scored;
                    if (!scored && nx + 60 < birdX)
                    {
                        scored = true;
                        score++;
                    }
                    if (nx < -100)
                    {
                        pipes.RemoveAt(i);
                    }
                    else
                    {
                        pipes[i] = new Pipe(nx, p.GapY, p.GapH, scored);
                    }
                }

                // Steam particles from jetpack
                if (frame % 2 == 0)
                {
                    particles.Add(new Particle(
                        birdX - 30, birdY + 25 + rng.Next(10),
                        -1.5f + (float)rng.NextDouble() * 0.5f,
                        1.0f + (float)rng.NextDouble(),
                        20 + (float)rng.NextDouble() * 20,
                        0, 40, 0)); // steam
                }
                // Random sparks
                if (frame % 5 == 0)
                {
                    particles.Add(new Particle(
                        birdX - 25, birdY + 30,
                        -3f + (float)rng.NextDouble(),
                        -1f + (float)rng.NextDouble() * 3f,
                        2f, 0, 15, 1)); // spark
                }

                worldX += 1.5f;
            }

            // Update particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var pa = particles[i];
                int life = pa.Life + 1;
                if (life > pa.MaxLife) { particles.RemoveAt(i); continue; }
                particles[i] = new Particle(
                    pa.X + pa.Vx, pa.Y + pa.Vy,
                    pa.Vx * 0.98f, pa.Vy * 0.98f,
                    pa.Size + (pa.Kind == 0 ? 0.3f : 0f),
                    life, pa.MaxLife, pa.Kind);
            }

            // Draw pipes
            foreach (var p in pipes)
            {
                DrawPipe(canvas, p.X, p.GapY, p.GapH, frame);
            }

            // Draw particles
            DrawParticles(canvas, particles);

            // Draw bird
            bool gameOver = frame >= 840;
            DrawBird(canvas, birdX, birdY, frame, gameOver);

            // Foreground cogs
            DrawForegroundCogs(canvas, frame);

            // HUD
            DrawHUD(canvas, score);

            // Vignette
            DrawVignette(canvas);

            // Game over overlay
            if (gameOver)
            {
                DrawGameOver(canvas, frame);
            }

            SaveFrame(bmp, frame);

            if (frame % 100 == 0)
            {
                Console.WriteLine($"Frame {frame}/{TOTAL_FRAMES}");
            }
        }

        Console.WriteLine($"Done: {TOTAL_FRAMES} frames");
        return 0;
    }

    static void SaveFrame(SKBitmap bmp, int frame)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        using var fs = File.OpenWrite(Path.Combine(OUT, $"frame_{frame:D4}.png"));
        data.SaveTo(fs);
    }

    static void DrawSky(SKCanvas c)
    {
        using var paint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, H),
            new[] { new SKColor(0x2A, 0x1A, 0x05), new SKColor(0x7A, 0x4A, 0x0A), new SKColor(0xC4, 0x9A, 0x20) },
            new float[] { 0f, 0.55f, 1f },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        c.DrawRect(0, 0, W, H, paint);
    }

    static void DrawCloudStreaks(SKCanvas c, int frame)
    {
        float[] ys = { 90, 130, 175, 215 };
        using var paint = new SKPaint
        {
            Color = new SKColor(0xD4, 0xB0, 0x60, 60),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
        };
        for (int i = 0; i < ys.Length; i++)
        {
            float y = ys[i];
            float drift = (frame * 0.3f * (1 + i * 0.2f)) % (W + 200);
            for (int s = -1; s < 4; s++)
            {
                float xs = s * 240 - drift;
                c.DrawLine(xs, y, xs + 180, y + (i % 2 == 0 ? 0 : 1), paint);
            }
        }
        paint.Color = new SKColor(0xD4, 0xB0, 0x60, 40);
        paint.StrokeWidth = 1;
        for (int i = 0; i < ys.Length; i++)
        {
            float y = ys[i] + 8;
            float drift = (frame * 0.2f * (1 + i * 0.15f)) % (W + 200);
            for (int s = -1; s < 4; s++)
            {
                float xs = s * 220 - drift + 30;
                c.DrawLine(xs, y, xs + 140, y, paint);
            }
        }
    }

    static void DrawBackgroundCogs(SKCanvas c, int frame)
    {
        // 3 large cogs, very low opacity, slow rotation
        DrawCog(c, 150, 180, 100, 8, frame * 0.005f, new SKColor(0x8B, 0x69, 0x14, 38));
        DrawCog(c, 650, 130, 140, 10, -frame * 0.004f, new SKColor(0x8B, 0x69, 0x14, 38));
        DrawCog(c, 400, 250, 200, 12, frame * 0.003f, new SKColor(0x8B, 0x69, 0x14, 30));
    }

    static void DrawForegroundCogs(SKCanvas c, int frame)
    {
        DrawCog(c, 60, H - GROUND_H - 30, 90, 10, frame * 0.02f, new SKColor(0x8B, 0x69, 0x14, 102));
        DrawCog(c, W - 60, H - GROUND_H - 40, 100, 12, -frame * 0.018f, new SKColor(0x8B, 0x69, 0x14, 102));
    }

    static void DrawCog(SKCanvas c, float cx, float cy, float radius, int teeth, float angle, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };

        c.Save();
        c.Translate(cx, cy);
        c.RotateRadians(angle);

        // Teeth as small rectangles around outer ring
        float toothLen = radius * 0.18f;
        float toothW = radius * 0.22f;
        for (int i = 0; i < teeth; i++)
        {
            float a = (float)(i * Math.PI * 2 / teeth);
            c.Save();
            c.RotateRadians(a);
            c.DrawRect(-toothW / 2, -radius - toothLen + 2, toothW, toothLen, paint);
            c.Restore();
        }
        // Outer ring
        c.DrawCircle(0, 0, radius, paint);
        // Inner hole
        using var inner = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)(color.Alpha / 2)),
            IsAntialias = true,
            BlendMode = SKBlendMode.DstOut
        };
        c.DrawCircle(0, 0, radius * 0.35f, inner);
        // Spokes
        using var spoke = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = radius * 0.08f };
        for (int i = 0; i < 4; i++)
        {
            float a = (float)(i * Math.PI / 2);
            c.DrawLine(0, 0, (float)Math.Cos(a) * radius * 0.7f, (float)Math.Sin(a) * radius * 0.7f, spoke);
        }
        c.Restore();
    }

    static void DrawFarSkyline(SKCanvas c, float worldX)
    {
        float offset = (worldX * 0.1f) % 400;
        using var paint = new SKPaint { Color = new SKColor(0x2A, 0x20, 0x10), IsAntialias = false };
        float baseY = H - GROUND_H - 10;

        // Repeating block buildings
        for (int i = -1; i < 8; i++)
        {
            float x = i * 110 - offset;
            float[] heights = { 60, 90, 70, 110, 80, 75, 100, 65, 95 };
            float h = heights[((i % heights.Length) + heights.Length) % heights.Length];
            c.DrawRect(x, baseY - h, 90, h, paint);
            // windows
            using var win = new SKPaint { Color = new SKColor(0x5A, 0x40, 0x10), IsAntialias = false };
            for (int wy = 0; wy < (int)(h / 18); wy++)
            {
                for (int wx = 0; wx < 3; wx++)
                {
                    if (((wx + wy + i) % 3) != 0)
                        c.DrawRect(x + 12 + wx * 22, baseY - h + 10 + wy * 18, 6, 8, win);
                }
            }
        }

        // Clock tower at fixed-ish position
        {
            float towerOffset = (worldX * 0.1f) % 800;
            float tx = 500 - towerOffset;
            for (int rep = 0; rep < 2; rep++)
            {
                float xx = tx + rep * 800;
                if (xx < -150 || xx > W + 50) continue;
                using var tpaint = new SKPaint { Color = new SKColor(0x3A, 0x30, 0x20), IsAntialias = false };
                c.DrawRect(xx, baseY - 200, 60, 200, tpaint);
                c.DrawRect(xx - 10, baseY - 220, 80, 25, tpaint);
                // clock face
                using var face = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = true };
                c.DrawCircle(xx + 30, baseY - 160, 16, face);
                using var faceInner = new SKPaint { Color = new SKColor(0x1A, 0x10, 0x05), IsAntialias = true };
                c.DrawCircle(xx + 30, baseY - 160, 12, faceInner);
                using var tick = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
                for (int t = 0; t < 12; t++)
                {
                    double a = t * Math.PI / 6;
                    float x1 = xx + 30 + (float)Math.Cos(a) * 8;
                    float y1 = baseY - 160 + (float)Math.Sin(a) * 8;
                    float x2 = xx + 30 + (float)Math.Cos(a) * 11;
                    float y2 = baseY - 160 + (float)Math.Sin(a) * 11;
                    c.DrawLine(x1, y1, x2, y2, tick);
                }
                // hands
                c.DrawLine(xx + 30, baseY - 160, xx + 30, baseY - 168, tick);
                c.DrawLine(xx + 30, baseY - 160, xx + 36, baseY - 160, tick);
            }
        }
    }

    static void DrawChimneyStacks(SKCanvas c, float worldX, List<Smoke> smokes, int frame)
    {
        float offset = (worldX * 0.2f) % 350;
        using var paint = new SKPaint { Color = new SKColor(0x3A, 0x30, 0x20), IsAntialias = false };
        float baseY = H - GROUND_H - 5;
        float[] xs = { 80, 230, 380, 530, 680, 830, 980 };
        float[] hs = { 160, 130, 180, 110, 170, 140, 175 };
        for (int i = 0; i < xs.Length; i++)
        {
            float x = xs[i] - offset;
            // wrap
            for (int rep = -1; rep < 2; rep++)
            {
                float xx = x + rep * 1050;
                if (xx < -40 || xx > W + 40) continue;
                c.DrawRect(xx, baseY - hs[i], 18, hs[i], paint);
                // cap
                using var cap = new SKPaint { Color = new SKColor(0x5A, 0x40, 0x20), IsAntialias = false };
                c.DrawRect(xx - 3, baseY - hs[i] - 6, 24, 8, cap);
            }
        }

        // Smoke puffs from a few stacks
        using var smokePaint = new SKPaint { IsAntialias = true };
        foreach (var s in smokes)
        {
            float t = (s.Size - 10) / 50f;
            byte alpha = (byte)Math.Clamp(180 * (1 - t), 0, 255);
            smokePaint.Color = new SKColor(0x80, 0x80, 0x70, alpha);
            c.DrawCircle(s.X, s.Y, s.Size, smokePaint);
        }
    }

    static void DrawGround(SKCanvas c)
    {
        using var p = new SKPaint();
        using var sh = SKShader.CreateLinearGradient(
            new SKPoint(0, H - GROUND_H), new SKPoint(0, H),
            new[] { new SKColor(0x1A, 0x1A, 0x1A), new SKColor(0x2A, 0x2A, 0x1A) },
            null, SKShaderTileMode.Clamp);
        p.Shader = sh;
        c.DrawRect(0, H - GROUND_H, W, GROUND_H, p);

        // Factory silhouettes (foreground, dark blocky)
        using var fac = new SKPaint { Color = new SKColor(0x10, 0x0A, 0x05), IsAntialias = false };
        float[] fxs = { 0, 90, 170, 280, 360, 470, 560, 660, 740 };
        float[] fhs = { 30, 45, 25, 50, 35, 55, 28, 48, 40 };
        for (int i = 0; i < fxs.Length; i++)
        {
            c.DrawRect(fxs[i], H - GROUND_H - fhs[i], 70, fhs[i], fac);
        }
        // ground top line
        using var line = new SKPaint { Color = new SKColor(0x40, 0x30, 0x10), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        c.DrawLine(0, H - GROUND_H, W, H - GROUND_H, line);
    }

    static void DrawBird(SKCanvas c, float x, float y, int frame, bool gameOver)
    {
        float bob = (float)Math.Sin(frame * 0.15) * 2.5f;
        float py = y + bob;

        // Slight tilt based on frame proximity to flap
        float tilt = 0;
        foreach (int f in FlapFrames)
        {
            int d = frame - f;
            if (d >= 0 && d < 8) tilt = -0.25f * (1 - d / 8f);
        }
        if (tilt == 0) tilt = 0.15f;

        c.Save();
        c.Translate(x, py);
        c.RotateRadians(tilt);

        using var aa = new SKPaint { IsAntialias = true };

        // Jetpack cylinders behind body
        using var jp = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = true };
        c.DrawRoundRect(new SKRect(-50, -10, -30, 30), 4, 4, jp);
        c.DrawRoundRect(new SKRect(-50, -30, -30, -10), 4, 4, jp);
        // Bands
        using var band = new SKPaint { Color = new SKColor(0x5A, 0x40, 0x05), IsAntialias = true };
        c.DrawRect(-50, -2, 20, 4, band);
        c.DrawRect(-50, -22, 20, 4, band);

        // Flame plumes
        using var flameO = new SKPaint { Color = new SKColor(0xFF, 0x80, 0x20), IsAntialias = true };
        using var flameY = new SKPaint { Color = new SKColor(0xFF, 0xD0, 0x40), IsAntialias = true };
        float flicker = 1 + (float)Math.Sin(frame * 0.6) * 0.2f;
        DrawTriangle(c, -40, 30, -45, 30 + 18 * flicker, -35, 30 + 14 * flicker, flameO);
        DrawTriangle(c, -40, -30, -45, -30 - 18 * flicker, -35, -30 - 14 * flicker, flameO);
        DrawTriangle(c, -40, 30, -42, 30 + 10 * flicker, -38, 30 + 8 * flicker, flameY);
        DrawTriangle(c, -40, -30, -42, -30 - 10 * flicker, -38, -30 - 8 * flicker, flameY);

        // Body (round koala)
        using var body = new SKPaint { Color = new SKColor(0x8B, 0x60, 0x40), IsAntialias = true };
        c.DrawOval(new SKRect(-35, -25, 42, 38), body);

        // Ears (semicircles) poking through helmet sides
        using var ear = new SKPaint { Color = new SKColor(0x6A, 0x40, 0x25), IsAntialias = true };
        c.DrawCircle(-20, -25, 14, ear);
        c.DrawCircle(28, -25, 14, ear);
        using var earIn = new SKPaint { Color = new SKColor(0xC0, 0x90, 0x70), IsAntialias = true };
        c.DrawCircle(-20, -22, 7, earIn);
        c.DrawCircle(28, -22, 7, earIn);

        // Brass diving helmet (over head)
        using var helmet = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true };
        c.DrawCircle(5, -5, 36, helmet);
        // Helmet rim
        using var rim = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
        c.DrawCircle(5, -5, 36, rim);
        // Bolts on helmet
        using var bolt = new SKPaint { Color = new SKColor(0x5A, 0x40, 0x05), IsAntialias = true };
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3;
            c.DrawCircle(5 + (float)Math.Cos(a) * 32, -5 + (float)Math.Sin(a) * 32, 2.5f, bolt);
        }

        // Porthole window (dark inner circle)
        using var port = new SKPaint { Color = new SKColor(0x1A, 0x0A, 0x0A), IsAntialias = true };
        c.DrawCircle(5, -5, 18, port);
        // Cross-hair lines
        using var ch = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true, StrokeWidth = 2 };
        c.DrawLine(5 - 18, -5, 5 + 18, -5, ch);
        c.DrawLine(5, -5 - 18, 5, -5 + 18, ch);
        // Ring inside porthole
        using var ringIn = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        c.DrawCircle(5, -5, 12, ringIn);

        // Eyes inside porthole
        using var eye = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF), IsAntialias = true };
        if (gameOver)
        {
            using var x1 = new SKPaint { Color = SKColors.White, StrokeWidth = 2.5f, IsAntialias = true };
            c.DrawLine(-3, -10, 3, -4, x1); c.DrawLine(3, -10, -3, -4, x1);
            c.DrawLine(9, -10, 15, -4, x1); c.DrawLine(15, -10, 9, -4, x1);
        }
        else
        {
            c.DrawCircle(-2, -7, 3.5f, eye);
            c.DrawCircle(12, -7, 3.5f, eye);
            using var pupil = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            c.DrawCircle(-1, -7, 1.8f, pupil);
            c.DrawCircle(13, -7, 1.8f, pupil);
        }

        // Monocle (gold ring upper right of helmet)
        using var monRing = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        c.DrawCircle(28, -18, 8, monRing);
        // Monocle chain
        c.DrawLine(34, -14, 38, -2, monRing);
        // Glint
        using var glint = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 200), IsAntialias = true, StrokeWidth = 1.8f };
        c.DrawLine(24, -22, 28, -18, glint);

        // Mechanical arm extending forward
        using var armP = new SKPaint { Color = new SKColor(0x6A, 0x4A, 0x10), IsAntialias = true };
        c.DrawRect(30, 8, 22, 8, armP);
        // Joint
        using var joint = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true };
        c.DrawCircle(30, 12, 4, joint);
        c.DrawCircle(52, 12, 3, joint);
        // Claw
        using var claw = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
        c.DrawLine(52, 12, 60, 7, claw);
        c.DrawLine(52, 12, 60, 17, claw);

        c.Restore();
    }

    static void DrawTriangle(SKCanvas c, float x1, float y1, float x2, float y2, float x3, float y3, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(x1, y1);
        path.LineTo(x2, y2);
        path.LineTo(x3, y3);
        path.Close();
        c.DrawPath(path, paint);
    }

    static void DrawPipe(SKCanvas c, float x, float gapY, float gapH, int frame)
    {
        const float pipeW = 70;
        float topH = gapY;
        float botY = gapY + gapH;
        float botH = H - GROUND_H - botY;

        if (topH < 0) topH = 0;
        if (botH < 0) botH = 0;

        using var body = new SKPaint { Color = new SKColor(0x8B, 0x69, 0x14), IsAntialias = false };
        using var rivet = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20), IsAntialias = true };
        using var cap = new SKPaint { Color = new SKColor(0x6A, 0x4A, 0x0A), IsAntialias = false };
        using var bolt = new SKPaint { Color = new SKColor(0x1A, 0x10, 0x05), IsAntialias = true };
        using var highlight = new SKPaint { Color = new SKColor(0xC4, 0x9A, 0x20, 80), IsAntialias = false };

        // Top pipe
        if (topH > 0)
        {
            c.DrawRect(x, 0, pipeW, topH, body);
            // vertical highlight stripe
            c.DrawRect(x + 8, 0, 6, topH, highlight);
            // Rivets every 40px along both edges
            for (float ry = 20; ry < topH - 5; ry += 40)
            {
                c.DrawCircle(x + 5, ry, 4, rivet);
                c.DrawCircle(x + pipeW - 5, ry, 4, rivet);
            }
            // Flanged cap
            float capY = topH - 18;
            c.DrawRect(x - 8, capY, pipeW + 16, 18, cap);
            // Bolt holes on cap
            for (int b = 0; b < 4; b++)
            {
                c.DrawCircle(x - 4 + b * (pipeW + 8) / 3f, capY + 9, 2.5f, bolt);
            }
            // Steam vents at opening (downward direction)
            DrawSteamVent(c, x + pipeW / 2, topH + 20, frame);
        }

        // Bottom pipe
        if (botH > 0)
        {
            c.DrawRect(x, botY, pipeW, botH, body);
            c.DrawRect(x + 8, botY, 6, botH, highlight);
            for (float ry = botY + 30; ry < botY + botH; ry += 40)
            {
                c.DrawCircle(x + 5, ry, 4, rivet);
                c.DrawCircle(x + pipeW - 5, ry, 4, rivet);
            }
            float capY = botY;
            c.DrawRect(x - 8, capY, pipeW + 16, 18, cap);
            for (int b = 0; b < 4; b++)
            {
                c.DrawCircle(x - 4 + b * (pipeW + 8) / 3f, capY + 9, 2.5f, bolt);
            }
            DrawSteamVent(c, x + pipeW / 2, botY - 20, frame);
        }
    }

    static void DrawSteamVent(SKCanvas c, float cx, float cy, int frame)
    {
        using var p = new SKPaint { IsAntialias = true };
        var rng = new Random((int)(cx * 13 + cy * 7));
        for (int i = 0; i < 7; i++)
        {
            float ox = -25 + (float)rng.NextDouble() * 50;
            float oy = -15 + (float)rng.NextDouble() * 30;
            float drift = (frame * 0.5f + i * 7) % 20;
            float sz = 10 + (float)rng.NextDouble() * 20;
            byte alpha = (byte)(rng.Next(38, 64));
            p.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);
            c.DrawOval(new SKRect(cx + ox - sz, cy + oy - sz / 2 - drift / 2, cx + ox + sz, cy + oy + sz / 2 - drift / 2), p);
        }
    }

    static void DrawParticles(SKCanvas c, List<Particle> particles)
    {
        foreach (var pa in particles)
        {
            float t = pa.Life / (float)pa.MaxLife;
            if (pa.Kind == 0)
            {
                // steam
                byte alpha = (byte)Math.Clamp(40 * (1 - t), 0, 40);
                using var p = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, alpha), IsAntialias = true };
                c.DrawCircle(pa.X, pa.Y, pa.Size, p);
            }
            else
            {
                // spark
                byte alpha = (byte)Math.Clamp(255 * (1 - t), 0, 255);
                using var p = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00, alpha), IsAntialias = true };
                c.DrawCircle(pa.X, pa.Y, pa.Size, p);
            }
        }
    }

    static void DrawHUD(SKCanvas c, int score)
    {
        using var shadow = new SKPaint
        {
            Color = new SKColor(0x10, 0x05, 0x00, 200),
            TextSize = 28,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        using var text = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20),
            TextSize = 28,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        string s = $"SCORE: {score}";
        c.DrawText(s, 22, 42, shadow);
        c.DrawText(s, 20, 40, text);
    }

    static void DrawVignette(SKCanvas c)
    {
        using var paint = new SKPaint();
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(W / 2, H / 2), Math.Max(W, H) * 0.7f,
            new[] { new SKColor(0, 0, 0, 0), new SKColor(0x2A, 0x10, 0x00, 160) },
            new float[] { 0.55f, 1f },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        c.DrawRect(0, 0, W, H, paint);
    }

    static void DrawTitleCard(SKCanvas c, int frame)
    {
        // Fade out near end
        float alpha = 1f;
        if (frame > 45) alpha = 1f - (frame - 45) / 14f;
        alpha = Math.Clamp(alpha, 0f, 1f);

        using var overlay = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x05, (byte)(180 * alpha)) };
        c.DrawRect(0, 0, W, H, overlay);

        using var title = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20, (byte)(255 * alpha)),
            TextSize = 56,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0x1A, 0x0A, 0x00, (byte)(220 * alpha)),
            TextSize = 56,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        c.DrawText("STEAMPUNK DOWNUNDER", W / 2 + 3, 230 + 3, titleShadow);
        c.DrawText("STEAMPUNK DOWNUNDER", W / 2, 230, title);

        using var sub = new SKPaint
        {
            Color = new SKColor(0xD4, 0xB0, 0x60, (byte)(255 * alpha)),
            TextSize = 30,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        c.DrawText("FlappyBrain 🧠", W / 2, 290, sub);

        // Decorative cog flanks
        DrawCog(c, 130, 220, 60, 10, frame * 0.05f, new SKColor(0xC4, 0x9A, 0x20, (byte)(180 * alpha)));
        DrawCog(c, 670, 220, 60, 10, -frame * 0.05f, new SKColor(0xC4, 0x9A, 0x20, (byte)(180 * alpha)));
    }

    static void DrawGameOver(SKCanvas c, int frame)
    {
        float t = (frame - 840) / 30f;
        if (t > 1) t = 1;

        using var overlay = new SKPaint { Color = new SKColor(0x2A, 0x1A, 0x05, (byte)(180 * t)) };
        c.DrawRect(0, 0, W, H, overlay);

        using var title = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20, (byte)(255 * t)),
            TextSize = 80,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0x10, 0x05, 0x00, (byte)(255 * t)),
            TextSize = 80,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        c.DrawText("GAME OVER", W / 2 + 4, 290 + 4, titleShadow);
        c.DrawText("GAME OVER", W / 2, 290, title);

        // Gear icons either side
        DrawCog(c, 160, 270, 45, 8, frame * 0.06f, new SKColor(0xC4, 0x9A, 0x20, (byte)(220 * t)));
        DrawCog(c, W - 160, 270, 45, 8, -frame * 0.06f, new SKColor(0xC4, 0x9A, 0x20, (byte)(220 * t)));
    }
}
