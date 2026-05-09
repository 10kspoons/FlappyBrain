using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme2;

internal static class Program
{
    private const int Width = 800;
    private const int Height = 600;
    private const int Fps = 30;
    private const int TotalFrames = 900;
    private const string OutDir = "/tmp/fb-t2-frames";

    private static readonly int[] FlapFrames =
    {
        80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820
    };

    // Pipe configuration: each pipe (x position, gap center y, gap height)
    private static readonly (int spawnFrame, float gapY, float gapH)[] Pipes =
    {
        (60,   300, 180),
        (180,  260, 180),
        (300,  340, 180),
        (420,  280, 170),
        (540,  320, 170),
        (660,  260, 170),
        (780,  300, 170),
    };

    private static readonly Random Rng = new(1337);

    // Pre-generated visual jitter
    private static readonly List<(float x, float y, float r, float dy, byte alpha)> CloudPuffs = new();
    private static readonly List<(float x, float y, float r, SKColor c)> HillBumps = new();
    private static readonly List<(float baseX, float trunkY, float canopyR, byte rng)> MidTrees = new();
    private static readonly List<(float x, float y, float r, SKColor c)> CanopyLeaves = new();
    private static readonly List<(float x, float y, float speed, SKColor c, float r, float drift)> Particles = new();
    private static readonly List<(float x, float y, float speed, float phase, int colorIdx)> Lorikeets = new();

    // Per-pipe bark patches (deterministic)
    private static readonly Dictionary<int, List<(float dx, float dy, float rx, float ry, SKColor c)>> BarkCache = new();
    // Per-pipe leaf cluster (deterministic)
    private static readonly Dictionary<int, List<(float dx, float dy, float rx, float ry, SKColor c)>> LeafCache = new();

    private static int Main()
    {
        Directory.CreateDirectory(OutDir);
        InitWorld();

        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        for (int frame = 0; frame < TotalFrames; frame++)
        {
            RenderFrame(surface.Canvas, frame);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            string path = Path.Combine(OutDir, $"frame_{frame:0000}.png");
            using var fs = File.OpenWrite(path);
            data.SaveTo(fs);

            if (frame % 60 == 0)
                Console.WriteLine($"Frame {frame}/{TotalFrames}");
        }

        Console.WriteLine($"Wrote {TotalFrames} frames to {OutDir}");
        return 0;
    }

    private static void InitWorld()
    {
        // 4 cloud groups, each 3-5 overlapping circles
        for (int g = 0; g < 4; g++)
        {
            float cx = Rng.Next(50, Width - 50);
            float cy = Rng.Next(40, 200);
            int puffs = Rng.Next(3, 6);
            for (int p = 0; p < puffs; p++)
            {
                float ox = (float)(Rng.NextDouble() * 60 - 30);
                float oy = (float)(Rng.NextDouble() * 25 - 12);
                float r = Rng.Next(22, 38);
                byte a = (byte)Rng.Next(204, 256); // 80-100%
                CloudPuffs.Add((cx + ox, cy + oy, r, 0, a));
            }
        }

        // Rolling hills bumps
        for (int i = 0; i < 14; i++)
        {
            float x = i * 80 + Rng.Next(-20, 20);
            float y = 470 + Rng.Next(-10, 20);
            float r = Rng.Next(70, 110);
            HillBumps.Add((x, y, r, new SKColor(0x3A, 0x8A, 0x25)));
        }

        // Mid eucalyptus trees
        for (int i = 0; i < 8; i++)
        {
            float bx = i * 120 + Rng.Next(-30, 30);
            float ty = 380 + Rng.Next(-20, 20);
            float cr = Rng.Next(40, 60);
            MidTrees.Add((bx, ty, cr, (byte)Rng.Next(0, 255)));
        }

        // Dense canopy ground texture
        for (int i = 0; i < 200; i++)
        {
            float x = Rng.Next(0, Width);
            float y = Rng.Next(500, 600);
            float r = Rng.Next(4, 12);
            var dark = new SKColor(0x1A, 0x50, 0x10);
            var med = new SKColor(0x2D, 0x7A, 0x1F);
            CanopyLeaves.Add((x, y, r, Rng.NextDouble() < 0.5 ? dark : med));
        }

        // Floating particles (leaves and petals)
        for (int i = 0; i < 40; i++)
        {
            float x = Rng.Next(0, Width);
            float y = Rng.Next(0, Height);
            float speed = (float)(0.3 + Rng.NextDouble() * 0.7);
            var leaf = new SKColor(0x90, 0xC0, 0x60);
            var petal = SKColors.White;
            float r = (float)(3 + Rng.NextDouble() * 3);
            float drift = (float)(Rng.NextDouble() * Math.PI * 2);
            Particles.Add((x, y, speed, Rng.NextDouble() < 0.5 ? leaf : petal, r, drift));
        }

        // Rainbow lorikeets
        for (int i = 0; i < 4; i++)
        {
            float x = Rng.Next(0, Width);
            float y = Rng.Next(150, 350);
            float speed = (float)(0.4 + Rng.NextDouble() * 0.4);
            float phase = (float)(Rng.NextDouble() * Math.PI * 2);
            Lorikeets.Add((x, y, speed, phase, i));
        }
    }

    private static void RenderFrame(SKCanvas canvas, int frame)
    {
        canvas.Clear(SKColors.Black);

        DrawSky(canvas);
        DrawClouds(canvas, frame);
        DrawHills(canvas, frame);
        DrawMidTrees(canvas, frame);
        DrawLorikeets(canvas, frame);
        DrawGroundCanopy(canvas, frame);

        // Pipes (eucalyptus trunks)
        DrawPipes(canvas, frame);

        DrawParticles(canvas, frame);

        // Bird & state
        var (birdX, birdY, score, gameOver) = ComputeBird(frame);
        DrawBird(canvas, birdX, birdY, score, frame);

        DrawHud(canvas, score);
        DrawVignette(canvas);

        // Title card
        if (frame < 60)
        {
            DrawTitleCard(canvas, frame);
        }

        if (gameOver)
        {
            DrawGameOver(canvas, frame, score);
        }
    }

    private static void DrawSky(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, Height),
                new[]
                {
                    new SKColor(0x4A, 0x90, 0xD9),
                    new SKColor(0x87, 0xCE, 0xEB),
                    new SKColor(0xE8, 0xF4, 0xFF),
                },
                new float[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, Width, Height, paint);
    }

    private static void DrawClouds(SKCanvas canvas, int frame)
    {
        float drift = -frame * 0.15f;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var (x, y, r, _, alpha) in CloudPuffs)
        {
            float cx = ((x + drift) % (Width + 200) + (Width + 200)) % (Width + 200) - 100;
            paint.Color = SKColors.White.WithAlpha(alpha);
            canvas.DrawCircle(cx, y, r, paint);
        }
    }

    private static void DrawHills(SKCanvas canvas, int frame)
    {
        float drift = -frame * 0.1f;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var (x, y, r, c) in HillBumps)
        {
            float cx = ((x + drift) % (Width + 200) + (Width + 200)) % (Width + 200) - 100;
            paint.Color = c;
            canvas.DrawCircle(cx, y, r, paint);
        }
    }

    private static void DrawMidTrees(SKCanvas canvas, int frame)
    {
        float drift = -frame * 0.2f;
        using var trunkPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0x7A, 0x90, 0x60), Style = SKPaintStyle.Fill };
        using var leafPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0x2D, 0x7A, 0x1F), Style = SKPaintStyle.Fill };
        using var leafDark = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x50, 0x10), Style = SKPaintStyle.Fill };

        foreach (var (baseX, trunkY, canopyR, rseed) in MidTrees)
        {
            float cx = ((baseX + drift) % (Width + 240) + (Width + 240)) % (Width + 240) - 120;
            // Trunk
            canvas.DrawRect(cx - 5, trunkY, 10, 480 - trunkY, trunkPaint);
            // Drooping leaf clusters
            var rnd = new Random(rseed);
            for (int i = 0; i < 6; i++)
            {
                float ox = (float)(rnd.NextDouble() * canopyR * 1.4 - canopyR * 0.7);
                float oy = (float)(rnd.NextDouble() * 30 - 15);
                float rx = (float)(canopyR * 0.45 + rnd.NextDouble() * 10);
                float ry = (float)(canopyR * 0.65 + rnd.NextDouble() * 15);
                var paint = rnd.NextDouble() < 0.4 ? leafDark : leafPaint;
                canvas.Save();
                canvas.Translate(cx + ox, trunkY + oy);
                canvas.DrawOval(0, 0, rx, ry, paint);
                canvas.Restore();
            }
        }
    }

    private static void DrawLorikeets(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var (x, y, speed, phase, idx) in Lorikeets)
        {
            float t = frame * speed;
            float cx = ((x + t) % (Width + 100) + (Width + 100)) % (Width + 100) - 50;
            float cy = y + (float)Math.Sin((frame * 0.05f) + phase) * 15;

            // Tiny multicolour bird ~20px
            paint.Color = new SKColor(0xE0, 0x30, 0x30); // red head
            canvas.DrawCircle(cx + 6, cy - 2, 3, paint);
            paint.Color = new SKColor(0x30, 0xC0, 0x50); // green body
            canvas.DrawCircle(cx, cy, 5, paint);
            paint.Color = new SKColor(0x30, 0x80, 0xE0); // blue wing
            float wingFlap = (float)Math.Sin(frame * 0.3f + phase) * 2;
            canvas.DrawOval(cx - 3, cy - 3 + wingFlap, 4, 2, paint);
            paint.Color = new SKColor(0xF0, 0xC0, 0x30); // yellow tail
            canvas.DrawOval(cx - 6, cy + 1, 3, 1.5f, paint);
        }
    }

    private static void DrawGroundCanopy(SKCanvas canvas, int frame)
    {
        // Base gradient
        using var basePaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 500),
                new SKPoint(0, 600),
                new[] { new SKColor(0x2D, 0x7A, 0x1F), new SKColor(0x1A, 0x50, 0x10) },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 500, Width, 100, basePaint);

        // Leaf texture
        using var leafPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var (x, y, r, c) in CanopyLeaves)
        {
            leafPaint.Color = c;
            canvas.DrawOval(x, y, r * 1.2f, r * 0.7f, leafPaint);
        }
    }

    private static void DrawPipes(SKCanvas canvas, int frame)
    {
        // Each pipe scrolls left at 2.0 px/frame, spawning at right (Width+60)
        float pipeSpeed = 2.0f;
        const float pipeW = 70;

        for (int i = 0; i < Pipes.Length; i++)
        {
            var (spawnFrame, gapY, gapH) = Pipes[i];
            float age = frame - spawnFrame;
            if (age < 0) continue;
            float x = Width + 60 - age * pipeSpeed;
            if (x + pipeW < -50) continue;

            DrawEucalyptusPipe(canvas, i, x, pipeW, gapY, gapH);
        }
    }

    private static void DrawEucalyptusPipe(SKCanvas canvas, int pipeIdx, float x, float pipeW, float gapY, float gapH)
    {
        var bodyColor = new SKColor(0x6B, 0x8C, 0x5A);
        var barkDark = new SKColor(0x4A, 0x60, 0x40);
        var barkLight = new SKColor(0x8A, 0xAA, 0x70);
        var leafGreen = new SKColor(0x2D, 0x7A, 0x1F);
        var leafDark = new SKColor(0x1A, 0x50, 0x10);

        float topBottom = gapY - gapH / 2;
        float botTop = gapY + gapH / 2;

        using var bodyPaint = new SKPaint { IsAntialias = true, Color = bodyColor, Style = SKPaintStyle.Fill };
        using var bark = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Top trunk: from 0 to topBottom
        canvas.DrawRect(x, 0, pipeW, topBottom, bodyPaint);
        // Bottom trunk: from botTop to 600
        canvas.DrawRect(x, botTop, pipeW, Height - botTop, bodyPaint);

        // Bark patches (cached per pipe)
        if (!BarkCache.TryGetValue(pipeIdx, out var patches))
        {
            patches = new List<(float, float, float, float, SKColor)>();
            var prng = new Random(1000 + pipeIdx);
            for (int p = 0; p < 30; p++)
            {
                float dx = (float)(prng.NextDouble() * pipeW);
                float dy = (float)(prng.NextDouble() * Height);
                float rx = (float)(10 + prng.NextDouble() * 10);
                float ry = (float)(8 + prng.NextDouble() * 12);
                var c = prng.NextDouble() < 0.5 ? barkDark : barkLight;
                patches.Add((dx, dy, rx, ry, c));
            }
            BarkCache[pipeIdx] = patches;
        }
        foreach (var (dx, dy, rx, ry, c) in patches)
        {
            // Skip patches inside gap
            if (dy > topBottom - 5 && dy < botTop + 5) continue;
            bark.Color = c;
            canvas.DrawOval(x + dx, dy, rx, ry, bark);
        }

        // Leaf clusters at gap edges (top trunk's bottom, bottom trunk's top)
        if (!LeafCache.TryGetValue(pipeIdx, out var leaves))
        {
            leaves = new List<(float, float, float, float, SKColor)>();
            var prng = new Random(2000 + pipeIdx);
            // Top cap (8-12 ovals around topBottom)
            int n1 = prng.Next(10, 13);
            for (int p = 0; p < n1; p++)
            {
                float dx = (float)(prng.NextDouble() * (pipeW + 30) - 15);
                float dy = (float)(prng.NextDouble() * 30 - 5); // y offset from edge
                float rx = (float)(20 + prng.NextDouble() * 30);
                float ry = (float)(15 + prng.NextDouble() * 25);
                var c = prng.NextDouble() < 0.4 ? leafDark : leafGreen;
                leaves.Add((dx, dy, rx, ry, c));
            }
            // Bottom cap
            int n2 = prng.Next(10, 13);
            for (int p = 0; p < n2; p++)
            {
                float dx = (float)(prng.NextDouble() * (pipeW + 30) - 15);
                float dy = -(float)(prng.NextDouble() * 30 - 5); // negative offset (above)
                float rx = (float)(20 + prng.NextDouble() * 30);
                float ry = (float)(15 + prng.NextDouble() * 25);
                var c = prng.NextDouble() < 0.4 ? leafDark : leafGreen;
                // Mark as bottom cap with negative dy convention
                leaves.Add((dx, -10000 + dy, rx, ry, c)); // sentinel: very negative dy means bottom
            }
            LeafCache[pipeIdx] = leaves;
        }

        using var leafPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        int half = leaves.Count / 2;
        // Top cap: at topBottom
        for (int p = 0; p < leaves.Count; p++)
        {
            var (dx, dy, rx, ry, c) = leaves[p];
            leafPaint.Color = c;
            float cx = x + dx;
            float cy;
            if (dy < -5000)
            {
                // Bottom cap
                float realDy = dy + 10000;
                cy = botTop + realDy;
            }
            else
            {
                cy = topBottom + dy;
            }
            canvas.DrawOval(cx, cy, rx, ry, leafPaint);
        }
    }

    private static (float x, float y, int score, bool gameOver) ComputeBird(int frame)
    {
        // Bird stays around x = 220
        float bx = 220;
        // Simple physics: gravity + flap impulses, reset for game-over phase
        float by = 300;
        float vy = 0;
        int gameStart = 0;
        int gameEnd = 840;

        if (frame < gameEnd)
        {
            for (int f = gameStart; f <= frame; f++)
            {
                vy += 0.45f; // gravity
                if (Array.IndexOf(FlapFrames, f) >= 0)
                {
                    vy = -7.5f;
                }
                by += vy;
                if (by > 480) { by = 480; vy = 0; }
                if (by < 30) { by = 30; vy = 0; }
            }
        }
        else
        {
            // Game over: bird falls
            // Compute pre-end position then drop
            for (int f = gameStart; f < gameEnd; f++)
            {
                vy += 0.45f;
                if (Array.IndexOf(FlapFrames, f) >= 0) vy = -7.5f;
                by += vy;
                if (by > 480) { by = 480; vy = 0; }
                if (by < 30) { by = 30; vy = 0; }
            }
            int dropFrames = frame - gameEnd;
            for (int f = 0; f < dropFrames; f++)
            {
                vy += 0.6f;
                by += vy;
                if (by > 480) { by = 480; vy = 0; }
            }
        }

        // Score = number of pipes passed (whose x has gone left of bird)
        int score = 0;
        float pipeSpeed = 2.0f;
        const float pipeW = 70;
        for (int i = 0; i < Pipes.Length; i++)
        {
            var (spawnFrame, _, _) = Pipes[i];
            float age = frame - spawnFrame;
            if (age < 0) continue;
            float x = Width + 60 - age * pipeSpeed;
            if (x + pipeW < bx) score++;
        }

        bool gameOver = frame >= gameEnd;
        return (bx, by, score, gameOver);
    }

    private static void DrawBird(SKCanvas canvas, float x, float y, int score, int frame)
    {
        canvas.Save();
        canvas.Translate(x, y);

        // Slight bobbing rotation based on flap proximity
        float tilt = 0;
        foreach (var f in FlapFrames)
        {
            int d = frame - f;
            if (d >= 0 && d < 10) tilt = -8 * (1 - d / 10f);
        }
        if (tilt == 0 && frame > 60 && frame < 840) tilt = 5; // slight nose-down between flaps
        canvas.RotateDegrees(tilt);

        if (score <= 2)
        {
            DrawPlatypusBarrel(canvas, frame);
        }
        else if (score <= 5)
        {
            DrawWombatHangGlider(canvas, frame);
        }
        else
        {
            DrawQuokkaLeafGlider(canvas, frame);
        }

        canvas.Restore();
    }

    private static void DrawPlatypusBarrel(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Wings (canvas triangles), flapping
        float flap = (float)Math.Sin(frame * 0.5) * 8;
        paint.Color = new SKColor(0xCC, 0xCC, 0xCC);
        var wingL = new SKPath();
        wingL.MoveTo(-50, -10);
        wingL.LineTo(-90, -40 + flap);
        wingL.LineTo(-30, -25);
        wingL.Close();
        canvas.DrawPath(wingL, paint);
        var wingR = new SKPath();
        wingR.MoveTo(50, -10);
        wingR.LineTo(90, -40 + flap);
        wingR.LineTo(30, -25);
        wingR.Close();
        canvas.DrawPath(wingR, paint);

        // Barrel hull (trapezoid)
        paint.Color = new SKColor(0x8B, 0x5E, 0x3C);
        var barrel = new SKPath();
        barrel.MoveTo(-50, -20);
        barrel.LineTo(50, -20);
        barrel.LineTo(45, 20);
        barrel.LineTo(-45, 20);
        barrel.Close();
        canvas.DrawPath(barrel, paint);
        // Barrel bands
        paint.Color = new SKColor(0x5C, 0x3D, 0x24);
        paint.StrokeWidth = 3;
        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(-48, -10, 48, -10, paint);
        canvas.DrawLine(-47, 5, 47, 5, paint);
        paint.Style = SKPaintStyle.Fill;

        // Beaver tail back
        paint.Color = new SKColor(0x6B, 0x4A, 0x2A);
        canvas.DrawOval(-65, 5, 18, 8, paint);

        // Duck bill front
        paint.Color = new SKColor(0xE8, 0x80, 0x30);
        canvas.DrawOval(55, 0, 18, 7, paint);

        // Eyes
        paint.Color = SKColors.White;
        canvas.DrawCircle(35, -8, 5, paint);
        paint.Color = SKColors.Black;
        canvas.DrawCircle(36, -8, 2.5f, paint);
    }

    private static void DrawWombatHangGlider(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Hang glider wing above (red triangle fabric)
        paint.Color = new SKColor(0xC8, 0x40, 0x20);
        var wing = new SKPath();
        wing.MoveTo(-60, -25);
        wing.LineTo(60, -25);
        wing.LineTo(0, -55);
        wing.Close();
        canvas.DrawPath(wing, paint);
        // White struts
        paint.Color = SKColors.White;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        canvas.DrawLine(-60, -25, 0, -55, paint);
        canvas.DrawLine(60, -25, 0, -55, paint);
        canvas.DrawLine(0, -25, 0, -55, paint);
        // Cable to body
        paint.Color = new SKColor(0x50, 0x50, 0x50);
        paint.StrokeWidth = 1.5f;
        canvas.DrawLine(-20, -25, -20, -8, paint);
        canvas.DrawLine(20, -25, 20, -8, paint);
        paint.Style = SKPaintStyle.Fill;

        // Wombat body (chunky grey rectangle with rounded corners)
        paint.Color = new SKColor(0x88, 0x88, 0x80);
        var body = new SKRoundRect(new SKRect(-40, -8, 40, 35), 12, 12);
        canvas.DrawRoundRect(body, paint);

        // Ears
        canvas.DrawCircle(-28, -10, 6, paint);
        canvas.DrawCircle(28, -10, 6, paint);
        paint.Color = new SKColor(0x60, 0x60, 0x55);
        canvas.DrawCircle(-28, -10, 3, paint);
        canvas.DrawCircle(28, -10, 3, paint);

        // Snout
        paint.Color = new SKColor(0x6B, 0x6B, 0x60);
        canvas.DrawOval(0, 8, 14, 8, paint);

        // Nose
        paint.Color = SKColors.Black;
        canvas.DrawCircle(0, 4, 3, paint);

        // Eyes
        paint.Color = SKColors.White;
        canvas.DrawCircle(-12, -2, 4, paint);
        canvas.DrawCircle(12, -2, 4, paint);
        paint.Color = SKColors.Black;
        canvas.DrawCircle(-11, -2, 2, paint);
        canvas.DrawCircle(13, -2, 2, paint);

        // Dangling chunky legs
        paint.Color = new SKColor(0x88, 0x88, 0x80);
        canvas.DrawRect(-25, 30, 12, 18, paint);
        canvas.DrawRect(13, 30, 12, 18, paint);
        // Paws
        paint.Color = new SKColor(0x40, 0x40, 0x38);
        canvas.DrawOval(-19, 47, 8, 4, paint);
        canvas.DrawOval(19, 47, 8, 4, paint);
    }

    private static void DrawQuokkaLeafGlider(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Palm frond wing above (large green leaf)
        canvas.Save();
        float waveAngle = (float)Math.Sin(frame * 0.2f) * 3;
        canvas.RotateDegrees(waveAngle, 0, -25);
        paint.Color = new SKColor(0x2D, 0x7A, 0x1F);
        var leaf = new SKPath();
        leaf.MoveTo(-70, -25);
        leaf.QuadTo(-35, -55, 0, -50);
        leaf.QuadTo(35, -55, 70, -25);
        leaf.QuadTo(35, -15, 0, -18);
        leaf.QuadTo(-35, -15, -70, -25);
        leaf.Close();
        canvas.DrawPath(leaf, paint);
        // Vein lines
        paint.Color = new SKColor(0x1A, 0x50, 0x10);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        canvas.DrawLine(-65, -25, 65, -25, paint);
        for (int i = -3; i <= 3; i++)
        {
            float lx = i * 18;
            canvas.DrawLine(lx, -25, lx * 0.6f, -45, paint);
        }
        paint.Style = SKPaintStyle.Fill;
        canvas.Restore();

        // Stem grip (small line)
        paint.Color = new SKColor(0x60, 0x40, 0x20);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 3;
        canvas.DrawLine(0, -22, 0, -8, paint);
        paint.Style = SKPaintStyle.Fill;

        // Round tan body
        paint.Color = new SKColor(0xC8, 0xA0, 0x60);
        canvas.DrawOval(0, 8, 35, 32, paint);

        // Large round ears
        canvas.DrawCircle(-22, -14, 11, paint);
        canvas.DrawCircle(22, -14, 11, paint);
        paint.Color = new SKColor(0xA0, 0x80, 0x50);
        canvas.DrawCircle(-22, -14, 6, paint);
        canvas.DrawCircle(22, -14, 6, paint);

        // Face (lighter front)
        paint.Color = new SKColor(0xE0, 0xC0, 0x90);
        canvas.DrawOval(0, 8, 22, 22, paint);

        // Huge smile (wide white arc)
        paint.Color = SKColors.White;
        var smile = new SKPath();
        smile.MoveTo(-15, 12);
        smile.QuadTo(0, 28, 15, 12);
        smile.QuadTo(0, 22, -15, 12);
        smile.Close();
        canvas.DrawPath(smile, paint);
        // Smile outline
        paint.Color = new SKColor(0x40, 0x20, 0x10);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        var smileOutline = new SKPath();
        smileOutline.MoveTo(-15, 12);
        smileOutline.QuadTo(0, 28, 15, 12);
        canvas.DrawPath(smileOutline, paint);
        paint.Style = SKPaintStyle.Fill;

        // Nose
        paint.Color = SKColors.Black;
        canvas.DrawCircle(0, 5, 2.5f, paint);

        // Eyes
        paint.Color = SKColors.White;
        canvas.DrawCircle(-9, -2, 4, paint);
        canvas.DrawCircle(9, -2, 4, paint);
        paint.Color = SKColors.Black;
        canvas.DrawCircle(-8, -1, 2, paint);
        canvas.DrawCircle(10, -1, 2, paint);

        // Tiny paws gripping stem
        paint.Color = new SKColor(0xA0, 0x80, 0x50);
        canvas.DrawOval(-4, -10, 4, 3, paint);
        canvas.DrawOval(4, -10, 4, 3, paint);
    }

    private static void DrawParticles(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        for (int i = 0; i < Particles.Count; i++)
        {
            var (x, y, speed, c, r, drift) = Particles[i];
            float t = frame;
            float px = x - t * speed * 0.6f + (float)Math.Sin((t * 0.03f) + drift) * 12;
            float py = y + t * speed * 0.4f;
            px = ((px % (Width + 40)) + (Width + 40)) % (Width + 40) - 20;
            py = ((py % (Height + 40)) + (Height + 40)) % (Height + 40) - 20;
            paint.Color = c.WithAlpha(180);
            canvas.DrawCircle(px, py, r, paint);
        }
    }

    private static void DrawHud(SKCanvas canvas, int score)
    {
        using var shadow = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(220),
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            Style = SKPaintStyle.Fill
        };
        using var text = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x1A, 0x50, 0x10),
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            Style = SKPaintStyle.Fill
        };
        string s = $"SCORE: {score}";
        canvas.DrawText(s, 22, 42, shadow);
        canvas.DrawText(s, 20, 40, text);
    }

    private static void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(Width / 2f, Height / 2f),
                Math.Max(Width, Height) * 0.7f,
                new[]
                {
                    new SKColor(0, 0, 0, 0),
                    new SKColor(0x1A, 0x50, 0x10, 0),
                    new SKColor(0x1A, 0x50, 0x10, 110),
                },
                new float[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, Width, Height, paint);
    }

    private static void DrawTitleCard(SKCanvas canvas, int frame)
    {
        float fade = frame < 50 ? 1f : (60 - frame) / 10f;
        if (fade < 0) fade = 0;
        byte ovAlpha = (byte)(102 * fade); // 40% * fade
        using var overlay = new SKPaint { Color = SKColors.White.WithAlpha(ovAlpha) };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        byte tAlpha = (byte)(255 * fade);
        using var title = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x2D, 0x7A, 0x1F).WithAlpha(tAlpha),
            TextSize = 64,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center,
            Style = SKPaintStyle.Fill
        };
        using var sub = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x4A, 0x90, 0xD9).WithAlpha(tAlpha),
            TextSize = 32,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawText("SKY SAFARI", Width / 2f, 260, title);
        canvas.DrawText("FlappyBrain 🧠", Width / 2f, 310, sub);
    }

    private static void DrawGameOver(SKCanvas canvas, int frame, int score)
    {
        int t = frame - 840;
        float fade = Math.Min(1f, t / 20f);
        byte ovA = (byte)(140 * fade);
        using var overlay = new SKPaint { Color = new SKColor(0x2D, 0x7A, 0x1F, ovA) };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        byte tA = (byte)(255 * fade);
        using var go = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x1A, 0x50, 0x10).WithAlpha(tA),
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center,
            Style = SKPaintStyle.Fill
        };
        using var goShadow = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(tA),
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawText("GAME OVER", Width / 2f + 3, 263, goShadow);
        canvas.DrawText("GAME OVER", Width / 2f, 260, go);

        using var sc = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(tA),
            TextSize = 36,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawText($"Score: {score}", Width / 2f, 320, sc);
    }
}
