using SkiaSharp;
using System;
using System.IO;

namespace FlappyBrainTheme6;

internal static class Program
{
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL_FRAMES = 900;
    const int TITLE_END = 60;
    const int GAMEPLAY_END = 840;
    const int GROUND_Y = 520;

    static readonly int[] FlapFrames = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };
    const int NEAR_MISS_FRAME = 640;

    // Pipe (Goliath suit) layout
    record PipePair(int SpawnFrame, float X0, int GapY, int GapHeight, bool LabelGoliath);
    static readonly PipePair[] Pipes = new[]
    {
        new PipePair(60, 900, 280, 180, false),
        new PipePair(60, 1180, 230, 180, true),
        new PipePair(60, 1460, 320, 170, false),
        new PipePair(60, 1740, 250, 180, true),
        new PipePair(60, 2020, 290, 175, false),
        new PipePair(60, 2300, 240, 180, true),
        new PipePair(60, 2580, 300, 175, false),
        new PipePair(60, 2860, 270, 180, true),
        new PipePair(60, 3140, 310, 170, false),
        new PipePair(60, 3420, 260, 180, true),
        new PipePair(60, 3700, 290, 175, false),
        new PipePair(60, 3980, 245, 180, true),
        new PipePair(60, 4260, 305, 170, false),
        new PipePair(60, 4540, 275, 175, true),
    };

    // Billboards
    record Billboard(float X0, string Line1, string Line2, SKColor Color1, SKColor Color2, float Size1, float Size2, bool Bold, bool GoldGradient);
    static readonly Billboard[] Billboards = new[]
    {
        new Billboard(800, "SMALL DOESN'T MEAN WEAK", "", new SKColor(0xF0,0xF0,0xF0), SKColors.White, 22, 0, false, false),
        new Billboard(1500, "UNDERDOGS ARMY", "", new SKColor(0xFF,0xD0,0x40), SKColors.White, 30, 0, true, false),
        new Billboard(2200, "SKIN IN THE GAME", "", new SKColor(0x20,0xC0,0xA0), SKColors.White, 26, 0, false, false),
        new Billboard(2900, "10,000 SPOONS", "", SKColors.White, SKColors.White, 32, 0, true, true),
        new Billboard(3600, "GOLIATH HAD 5 STONES", "YOU HAVE 10,000", new SKColor(0xF0,0xF0,0xF0), new SKColor(0xFF,0xD0,0x40), 20, 20, true, false),
        new Billboard(4300, "UNDERDOGS ARMY", "", new SKColor(0xFF,0xD0,0x40), SKColors.White, 30, 0, true, false),
    };

    // Far towers
    record Tower(float X, float W, float H, SKColor Color, string? Label);
    static readonly Tower[] FarTowers = BuildFarTowers();
    static Tower[] BuildFarTowers()
    {
        var rng = new Random(7);
        var list = new System.Collections.Generic.List<Tower>();
        float x = 0;
        while (x < 6000)
        {
            float w = 60 + (float)rng.NextDouble() * 70;
            float h = 300 + (float)rng.NextDouble() * 200;
            var col = new SKColor(0x1A, 0x20, 0x30);
            string? lbl = (rng.NextDouble() < 0.18) ? "GOLIATH CORP" : null;
            list.Add(new Tower(x, w, h, col, lbl));
            x += w + 4 + (float)rng.NextDouble() * 20;
        }
        return list.ToArray();
    }

    // Mid towers (with cracks)
    record MidTower(float X, float W, float H, bool Cracked);
    static readonly MidTower[] MidTowers = new[]
    {
        new MidTower(200, 130, 380, true),
        new MidTower(900, 110, 320, false),
        new MidTower(1600, 150, 410, true),
        new MidTower(2400, 120, 350, false),
        new MidTower(3100, 140, 390, true),
        new MidTower(3900, 130, 360, false),
        new MidTower(4700, 145, 400, true),
    };

    // Star windows: precompute window grid for far towers
    record Window(int TowerIdx, float OffX, float OffY, float W, float H, int FlickerSeed);
    static readonly Window[] FarWindows = BuildFarWindows();
    static Window[] BuildFarWindows()
    {
        var rng = new Random(42);
        var list = new System.Collections.Generic.List<Window>();
        for (int i = 0; i < FarTowers.Length; i++)
        {
            var t = FarTowers[i];
            for (float wy = 20; wy < t.H - 30; wy += 12)
            {
                for (float wx = 8; wx < t.W - 8; wx += 10)
                {
                    if (rng.NextDouble() < 0.55)
                    {
                        list.Add(new Window(i, wx, wy, 4, 5, rng.Next(1000)));
                    }
                }
            }
        }
        return list.ToArray();
    }

    // Rubble (mid layer)
    record Rubble(float X, float Y0, float Vy, float Size, int Seed);
    static readonly Rubble[] Rubbles = BuildRubble();
    static Rubble[] BuildRubble()
    {
        var rng = new Random(13);
        var list = new System.Collections.Generic.List<Rubble>();
        foreach (var mt in MidTowers)
        {
            if (!mt.Cracked) continue;
            for (int i = 0; i < 6; i++)
            {
                list.Add(new Rubble(
                    mt.X + (float)rng.NextDouble() * mt.W,
                    H - mt.H + (float)rng.NextDouble() * mt.H * 0.7f,
                    20 + (float)rng.NextDouble() * 30,
                    3 + (float)rng.NextDouble() * 4,
                    rng.Next(1000)));
            }
        }
        return list.ToArray();
    }

    // Data stream columns
    record DataCol(float X, int Seed);
    static readonly DataCol[] DataCols = BuildDataCols();
    static DataCol[] BuildDataCols()
    {
        var list = new DataCol[8];
        var rng = new Random(99);
        for (int i = 0; i < 8; i++)
            list[i] = new DataCol(50 + i * 95 + (float)rng.NextDouble() * 30, rng.Next(10000));
        return list;
    }

    static int Main()
    {
        Directory.CreateDirectory("/tmp/fb-t6-frames");

        var info = new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);

        // bird physics
        float birdY = 280;
        float birdVy = 0;
        float gravity = 0.42f;
        float flapImpulse = -7.5f;
        const float BIRD_X = 200;

        // particle trails
        var sparkList = new System.Collections.Generic.List<(float X, float Y, float Vx, float Vy, int Life, int Max, SKColor Col)>();

        for (int f = 0; f < TOTAL_FRAMES; f++)
        {
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            if (f < TITLE_END)
            {
                DrawTitle(canvas, f);
            }
            else
            {
                int gf = f - TITLE_END; // gameplay frame index 0..

                // Update bird
                if (Array.IndexOf(FlapFrames, f) >= 0)
                {
                    birdVy = flapImpulse;
                    // spawn flap sparks
                    var rng = new Random(f * 31 + 7);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = (float)(rng.NextDouble() * Math.PI * 2);
                        float spd = 1.2f + (float)rng.NextDouble() * 2.2f;
                        sparkList.Add((BIRD_X - 10, birdY, MathF.Cos(ang) * spd - 1.5f, MathF.Sin(ang) * spd, 0, 18,
                            rng.NextDouble() < 0.5 ? new SKColor(0xFF, 0xD0, 0x40) : new SKColor(0xC4, 0x9A, 0x20)));
                    }
                }
                if (f >= TITLE_END && f < GAMEPLAY_END)
                {
                    birdVy += gravity;
                    if (birdVy > 11) birdVy = 11;
                    birdY += birdVy;
                    if (birdY < 60) { birdY = 60; birdVy = 0; }
                    if (birdY > GROUND_Y - 30) { birdY = GROUND_Y - 30; birdVy = 0; }

                    // ambient gold trail behind spoon
                    if (f % 2 == 0)
                    {
                        var rng = new Random(f * 17);
                        sparkList.Add((BIRD_X - 30 + (float)rng.NextDouble() * 4, birdY + 4 + (float)rng.NextDouble() * 6,
                            -1.5f - (float)rng.NextDouble(), -0.2f + (float)rng.NextDouble() * 0.4f, 0, 28,
                            new SKColor(0xFF, 0xD0, 0x40, 200)));
                    }
                }

                // World scroll speed
                float worldX = gf * 2.4f;

                DrawSky(canvas);
                DrawDataStream(canvas, f);
                DrawFarTowers(canvas, worldX, f);
                DrawMidTowers(canvas, worldX, f);
                DrawBillboards(canvas, worldX);
                DrawRubble(canvas, worldX, gf);
                DrawStreet(canvas);
                DrawSpeedLines(canvas, f);
                DrawPipes(canvas, worldX, f);

                // Update sparks
                for (int i = sparkList.Count - 1; i >= 0; i--)
                {
                    var s = sparkList[i];
                    s.Life++;
                    s.X += s.Vx;
                    s.Y += s.Vy;
                    s.Vy += 0.05f;
                    if (s.Life >= s.Max) sparkList.RemoveAt(i);
                    else sparkList[i] = s;
                }
                DrawSparks(canvas, sparkList);

                // Near miss extra burst
                if (f == NEAR_MISS_FRAME)
                {
                    var rng = new Random(777);
                    for (int i = 0; i < 30; i++)
                    {
                        float ang = (float)(rng.NextDouble() * Math.PI * 2);
                        float spd = 1.5f + (float)rng.NextDouble() * 3.5f;
                        sparkList.Add((BIRD_X + 30, birdY, MathF.Cos(ang) * spd, MathF.Sin(ang) * spd, 0, 28,
                            rng.NextDouble() < 0.5 ? new SKColor(0xFF, 0xD0, 0x40) : new SKColor(0xFF, 0x80, 0x20)));
                    }
                }

                DrawBird(canvas, BIRD_X, birdY, birdVy, f);

                if (f >= GAMEPLAY_END)
                {
                    DrawGameOver(canvas, f);
                }
                else
                {
                    DrawHUD(canvas, f);
                }
            }

            DrawVignette(canvas);

            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.OpenWrite($"/tmp/fb-t6-frames/frame_{f:D4}.png");
            data.SaveTo(fs);

            if (f % 90 == 0) Console.WriteLine($"frame {f}/{TOTAL_FRAMES}");
        }
        Console.WriteLine("frames done");
        return 0;
    }

    static void DrawSky(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = true };
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, H),
            new[] { new SKColor(0x04, 0x0E, 0x1E), new SKColor(0x0A, 0x1E, 0x3A), new SKColor(0x14, 0x28, 0x50) },
            new[] { 0f, 0.55f, 1f },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        canvas.DrawRect(0, 0, W, H, paint);
    }

    static void DrawDataStream(SKCanvas canvas, int f)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Color = new SKColor(0x20, 0xC0, 0xA0, 22),
            TextSize = 11,
            Typeface = SKTypeface.FromFamilyName("monospace")
        };
        var rng = new Random();
        foreach (var c in DataCols)
        {
            var crng = new Random(c.Seed);
            int rows = 50;
            for (int r = 0; r < rows; r++)
            {
                float y = ((r * 14) + f * 2 + (c.Seed % 100)) % (H + 60) - 30;
                char ch = (crng.Next(r * 7 + f / 4 + c.Seed) % 4 < 2) ? '0' : '1';
                if (crng.NextDouble() < 0.2) ch = "0123456789ABCDEF"[crng.Next(16)];
                canvas.DrawText(ch.ToString(), c.X, y, paint);
            }
        }
    }

    static void DrawFarTowers(SKCanvas canvas, float worldX, int f)
    {
        float scrollX = worldX * 0.08f;
        using var bld = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        using var lblPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xC0, 0xC0, 0xC8, 140),
            TextSize = 8,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        for (int i = 0; i < FarTowers.Length; i++)
        {
            var t = FarTowers[i];
            float x = t.X - scrollX;
            // wrap
            float totalSpan = 6000;
            while (x < -200) x += totalSpan;
            while (x > W + 200) x -= totalSpan;
            if (x + t.W < -10 || x > W + 10) continue;

            bld.Color = t.Color;
            canvas.DrawRect(x, H - t.H, t.W, t.H, bld);
            // top edge highlight
            bld.Color = new SKColor(0x2A, 0x32, 0x44);
            canvas.DrawRect(x, H - t.H, t.W, 4, bld);

            if (t.Label != null)
            {
                canvas.DrawText(t.Label, x + 4, H - t.H + 50, lblPaint);
            }
        }

        // windows
        using var winPaint = new SKPaint { IsAntialias = false };
        foreach (var w in FarWindows)
        {
            var t = FarTowers[w.TowerIdx];
            float x = t.X - scrollX;
            float totalSpan = 6000;
            while (x < -200) x += totalSpan;
            while (x > W + 200) x -= totalSpan;
            if (x + t.W < -10 || x > W + 10) continue;

            // flicker
            int flick = ((f / 6) + w.FlickerSeed) % 100;
            byte alpha = (byte)(flick < 8 ? 80 : 200);
            // mix yellow / white
            SKColor wcol = (w.FlickerSeed % 3 == 0)
                ? new SKColor(0xFF, 0xD8, 0x80, alpha)
                : new SKColor(0xE8, 0xE0, 0xC0, alpha);
            winPaint.Color = wcol;
            canvas.DrawRect(x + w.OffX, H - t.H + w.OffY, w.W, w.H, winPaint);
        }
    }

    static void DrawMidTowers(SKCanvas canvas, float worldX, int f)
    {
        float scrollX = worldX * 0.15f;
        using var bld = new SKPaint { IsAntialias = false, Color = new SKColor(0x20, 0x28, 0x3A) };
        using var crack = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        };
        using var winPaint = new SKPaint { IsAntialias = false };

        foreach (var t in MidTowers)
        {
            float x = t.X - scrollX;
            float totalSpan = 5500;
            while (x < -300) x += totalSpan;
            while (x > W + 300) x -= totalSpan;
            if (x + t.W < -10 || x > W + 10) continue;

            canvas.DrawRect(x, H - t.H, t.W, t.H, bld);

            // windows
            var rng = new Random((int)t.X * 7 + 3);
            for (float wy = 30; wy < t.H - 40; wy += 18)
            {
                for (float wx = 12; wx < t.W - 12; wx += 16)
                {
                    if (rng.NextDouble() < 0.55)
                    {
                        int flick = ((f / 8) + rng.Next(100)) % 100;
                        byte a = (byte)(flick < 6 ? 90 : 220);
                        winPaint.Color = new SKColor(0xFF, 0xD8, 0x70, a);
                        canvas.DrawRect(x + wx, H - t.H + wy, 6, 8, winPaint);
                    }
                }
            }

            if (t.Cracked)
            {
                // jagged crack
                using var path = new SKPath();
                float cx = x + t.W * 0.4f;
                float cy = H - t.H + 30;
                path.MoveTo(cx, cy);
                var prng = new Random((int)t.X);
                for (int i = 0; i < 8; i++)
                {
                    cx += (float)(prng.NextDouble() - 0.3) * 18;
                    cy += t.H / 9f;
                    path.LineTo(cx, cy);
                }
                canvas.DrawPath(path, crack);
            }
        }
    }

    static void DrawRubble(SKCanvas canvas, float worldX, int gf)
    {
        float scrollX = worldX * 0.15f;
        using var p = new SKPaint { IsAntialias = false, Color = new SKColor(0x10, 0x14, 0x20) };
        foreach (var r in Rubbles)
        {
            float x = r.X - scrollX;
            float totalSpan = 5500;
            while (x < -100) x += totalSpan;
            while (x > W + 100) x -= totalSpan;
            float y = r.Y0 + ((gf * r.Vy / 60f) % 200);
            canvas.DrawRect(x, y, r.Size, r.Size, p);
        }
    }

    static void DrawBillboards(SKCanvas canvas, float worldX)
    {
        float scrollX = worldX * 0.4f;
        foreach (var b in Billboards)
        {
            float x = b.X0 - scrollX;
            float totalSpan = 5000;
            while (x < -400) x += totalSpan;
            while (x > W + 400) x -= totalSpan;
            if (x + 280 < -10 || x > W + 10) continue;

            float bx = x;
            float by = 70;

            // pole
            using (var pole = new SKPaint { Color = new SKColor(0x4A, 0x4A, 0x4A), IsAntialias = false })
            {
                canvas.DrawRect(bx + 137, by + 80, 6, 100, pole);
            }

            // rotate slight tilt
            canvas.Save();
            canvas.Translate(bx + 140, by + 40);
            canvas.RotateDegrees(((int)b.X0 % 5 - 2));
            canvas.Translate(-140, -40);

            // panel
            using (var bg = new SKPaint { Color = new SKColor(0x0A, 0x0A, 0x0A), IsAntialias = true })
                canvas.DrawRect(0, 0, 280, 80, bg);
            using (var border = new SKPaint { Color = new SKColor(0xFF, 0xD0, 0x40), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true })
                canvas.DrawRect(0, 0, 280, 80, border);

            // scan lines
            using (var scan = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 20), StrokeWidth = 1 })
            {
                for (float sy = 2; sy < 80; sy += 4)
                    canvas.DrawLine(2, sy, 278, sy, scan);
            }

            // text
            using var fontPaint = new SKPaint
            {
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", b.Bold ? SKFontStyle.Bold : SKFontStyle.Normal)
            };

            if (string.IsNullOrEmpty(b.Line2))
            {
                fontPaint.TextSize = b.Size1;
                if (b.GoldGradient)
                {
                    fontPaint.Color = new SKColor(0x8B, 0x69, 0x14);
                    var w1 = fontPaint.MeasureText(b.Line1);
                    canvas.DrawText(b.Line1, 140 - w1 / 2 + 2, 50, fontPaint);
                    fontPaint.Color = new SKColor(0xFF, 0xD0, 0x40);
                    canvas.DrawText(b.Line1, 140 - w1 / 2, 48, fontPaint);
                }
                else
                {
                    fontPaint.Color = b.Color1;
                    var w1 = fontPaint.MeasureText(b.Line1);
                    canvas.DrawText(b.Line1, 140 - w1 / 2, 50, fontPaint);
                }
            }
            else
            {
                fontPaint.TextSize = b.Size1;
                fontPaint.Color = b.Color1;
                var w1 = fontPaint.MeasureText(b.Line1);
                canvas.DrawText(b.Line1, 140 - w1 / 2, 32, fontPaint);
                fontPaint.TextSize = b.Size2;
                fontPaint.Color = b.Color2;
                var w2 = fontPaint.MeasureText(b.Line2);
                canvas.DrawText(b.Line2, 140 - w2 / 2, 62, fontPaint);
            }
            canvas.Restore();
        }
    }

    static void DrawStreet(SKCanvas canvas)
    {
        // bottom 80px street glow
        using var p = new SKPaint();
        using var sh = SKShader.CreateLinearGradient(
            new SKPoint(0, GROUND_Y), new SKPoint(0, H),
            new[] { new SKColor(0x0A, 0x0A, 0x0A), new SKColor(0x1A, 0x10, 0x20) },
            null, SKShaderTileMode.Clamp);
        p.Shader = sh;
        canvas.DrawRect(0, GROUND_Y, W, H - GROUND_Y, p);

        // distant city lights — colored streaks
        using var lp = new SKPaint { IsAntialias = false };
        var rng = new Random(123);
        for (int i = 0; i < 30; i++)
        {
            int rcol = rng.Next(4);
            SKColor c = rcol switch
            {
                0 => new SKColor(0xFF, 0xD0, 0x40, 110),
                1 => new SKColor(0xCC, 0x20, 0x20, 100),
                2 => new SKColor(0x20, 0xC0, 0xA0, 90),
                _ => new SKColor(0xE8, 0xE0, 0xC0, 100),
            };
            lp.Color = c;
            float x = rng.Next(W);
            float y = GROUND_Y + 4 + rng.Next(20);
            float w = 4 + rng.Next(12);
            canvas.DrawRect(x, y, w, 1.5f, lp);
        }
    }

    static void DrawSpeedLines(SKCanvas canvas, int f)
    {
        bool flapNear = false;
        foreach (var ff in FlapFrames) if (Math.Abs(f - ff) <= 4) { flapNear = true; break; }
        var rng = new Random(f / 3 + 100);
        using var p = new SKPaint { IsAntialias = false };
        for (int i = 0; i < 20; i++)
        {
            float y = rng.Next(H - 100);
            float len = (flapNear ? 150 : 80) + rng.Next(100);
            byte a = (byte)((flapNear ? 100 : 40) + rng.Next(40));
            p.Color = new SKColor(0xFF, 0xD0, 0x40, a);
            p.StrokeWidth = 1.2f;
            float x = (f * 6 + i * 47) % W;
            canvas.DrawLine(x, y, x + len, y, p);
        }
    }

    static void DrawPipes(SKCanvas canvas, float worldX, int f)
    {
        const float scroll = 1f;
        using var jacket = new SKPaint { Color = new SKColor(0x2A, 0x2A, 0x35), IsAntialias = false };
        using var lapel = new SKPaint { Color = new SKColor(0x3A, 0x3A, 0x45), IsAntialias = true };
        using var tie = new SKPaint { Color = new SKColor(0xC4, 0x00, 0x20), IsAntialias = false };
        using var shirt = new SKPaint { Color = new SKColor(0xE8, 0xE8, 0xE8), IsAntialias = false };
        using var brief = new SKPaint { Color = new SKColor(0x3A, 0x3A, 0x45), IsAntialias = true };
        using var briefStroke = new SKPaint { Color = new SKColor(0x1A, 0x1A, 0x22), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var lblPaint = new SKPaint
        {
            Color = new SKColor(0x5A, 0x5A, 0x6A),
            IsAntialias = true,
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        const int PW = 95;

        for (int idx = 0; idx < Pipes.Length; idx++)
        {
            var pp = Pipes[idx];
            float x = pp.X0 - worldX * scroll;
            if (x + PW < -20 || x > W + 20) continue;

            int gapTop = pp.GapY;
            int gapBot = pp.GapY + pp.GapHeight;

            // top column
            DrawSuitColumn(canvas, x, 0, PW, gapTop, jacket, lapel, tie, shirt, brief, briefStroke, isTop: true);
            // bottom column
            DrawSuitColumn(canvas, x, gapBot, PW, GROUND_Y - gapBot, jacket, lapel, tie, shirt, brief, briefStroke, isTop: false);

            if (pp.LabelGoliath)
            {
                canvas.DrawText("GOLIATH", x + 18, gapTop - 50, lblPaint);
                canvas.DrawText("GOLIATH", x + 18, gapBot + 80, lblPaint);
            }
        }
    }

    static void DrawSuitColumn(SKCanvas canvas, float x, float y, float w, float h,
        SKPaint jacket, SKPaint lapel, SKPaint tie, SKPaint shirt, SKPaint brief, SKPaint briefStroke, bool isTop)
    {
        if (h <= 0) return;
        // shirt cuffs at top and bottom of column body (the column body excludes the cap)
        float capH = 28;
        float bodyY = isTop ? y : y + capH;
        float bodyH = h - capH;
        if (bodyH < 10) bodyH = 10;

        // jacket body
        canvas.DrawRect(x, bodyY, w, bodyH, jacket);

        // lapel V
        using (var path = new SKPath())
        {
            path.MoveTo(x + w * 0.15f, isTop ? bodyY + bodyH - 60 : bodyY);
            path.LineTo(x + w * 0.5f, isTop ? bodyY + bodyH - 25 : bodyY + 35);
            path.LineTo(x + w * 0.85f, isTop ? bodyY + bodyH - 60 : bodyY);
            path.LineTo(x + w * 0.85f, isTop ? bodyY + bodyH - 30 : bodyY + 5);
            path.LineTo(x + w * 0.5f, isTop ? bodyY + bodyH : bodyY + 60);
            path.LineTo(x + w * 0.15f, isTop ? bodyY + bodyH - 30 : bodyY + 5);
            path.Close();
            canvas.DrawPath(path, lapel);
        }

        // tie - thin red strip down center of body
        canvas.DrawRect(x + w / 2 - 4, bodyY, 8, bodyH, tie);

        // shirt cuffs
        canvas.DrawRect(x, bodyY, w, 5, shirt);
        canvas.DrawRect(x, bodyY + bodyH - 5, w, 5, shirt);

        // briefcase cap
        float capY = isTop ? y + h - capH : y;
        canvas.DrawRect(x - 3, capY, w + 6, capH, brief);
        // briefcase handle arc on the side facing away from gap (i.e., outer side)
        if (isTop)
        {
            // handle arc above the cap (visible top)
            using var hp = new SKPath();
            hp.MoveTo(x + w * 0.3f, capY);
            hp.QuadTo(x + w * 0.5f, capY - 14, x + w * 0.7f, capY);
            canvas.DrawPath(hp, briefStroke);
        }
        else
        {
            // handle below
            using var hp = new SKPath();
            hp.MoveTo(x + w * 0.3f, capY + capH);
            hp.QuadTo(x + w * 0.5f, capY + capH + 14, x + w * 0.7f, capY + capH);
            canvas.DrawPath(hp, briefStroke);
        }
    }

    static void DrawSparks(SKCanvas canvas, System.Collections.Generic.List<(float X, float Y, float Vx, float Vy, int Life, int Max, SKColor Col)> sparks)
    {
        using var p = new SKPaint { IsAntialias = true };
        foreach (var s in sparks)
        {
            float t = 1f - (float)s.Life / s.Max;
            byte a = (byte)Math.Clamp(s.Col.Alpha == 255 ? (int)(255 * t) : (int)(s.Col.Alpha * t), 0, 255);
            p.Color = s.Col.WithAlpha(a);
            float r = 2.5f + 2 * t;
            canvas.DrawCircle(s.X, s.Y, r, p);
        }
    }

    static void DrawBird(SKCanvas canvas, float x, float y, float vy, int f)
    {
        // tilt based on velocity
        float tilt = Math.Clamp(vy * 3.5f, -22, 30);

        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(tilt);

        // ROCKET EXHAUST behind spoon (handle direction)
        DrawExhaust(canvas, f);

        // SPOON HANDLE — angles back/up. Drawn first so bowl overlaps where joined.
        using (var handle = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            StrokeCap = SKStrokeCap.Round
        })
        {
            canvas.DrawLine(15, 8, -75, 18, handle);
        }
        // handle highlight
        using (var hl = new SKPaint
        {
            Color = new SKColor(0xFF, 0xE0, 0x80),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        })
        {
            canvas.DrawLine(15, 5, -75, 15, hl);
        }

        // SPOON BOWL — oval
        using (var bowl = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD0, 0x40),
            IsAntialias = true
        })
        {
            canvas.DrawOval(20, 8, 28, 22, bowl);
        }
        // bowl rim shadow
        using (var rim = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        })
        {
            canvas.DrawOval(20, 8, 28, 22, rim);
        }
        // bowl interior shine
        using (var shine = new SKPaint
        {
            Color = new SKColor(0xFF, 0xF0, 0xB0, 180),
            IsAntialias = true
        })
        {
            canvas.DrawOval(15, 0, 12, 6, shine);
        }

        // FOUNDER sitting in the bowl
        // body (hoodie)
        using (var body = new SKPaint { Color = new SKColor(0xE8, 0xE8, 0xE8), IsAntialias = true })
        {
            canvas.DrawRoundRect(8, -22, 30, 32, 8, 8, body);
        }
        // hoodie shading
        using (var sh = new SKPaint { Color = new SKColor(0xB0, 0xB0, 0xB0, 140), IsAntialias = true })
        {
            canvas.DrawRoundRect(28, -20, 9, 28, 4, 4, sh);
        }
        // head
        using (var head = new SKPaint { Color = new SKColor(0xE8, 0xC8, 0xA8), IsAntialias = true })
        {
            canvas.DrawCircle(22, -28, 11, head);
        }
        // hood up
        using (var hood = new SKPaint { Color = new SKColor(0xC0, 0xC0, 0xC0), IsAntialias = true })
        {
            using var path = new SKPath();
            path.MoveTo(11, -28);
            path.QuadTo(22, -45, 33, -28);
            path.LineTo(33, -22);
            path.LineTo(11, -22);
            path.Close();
            canvas.DrawPath(path, hood);
        }
        // eyes (defiant)
        using (var eye = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.DrawCircle(26, -29, 1.5f, eye);
        }
        // raised fist forward
        using (var fist = new SKPaint { Color = new SKColor(0xE8, 0xC8, 0xA8), IsAntialias = true })
        {
            canvas.DrawCircle(50, -10, 6, fist);
        }
        using (var arm = new SKPaint
        {
            Color = new SKColor(0xE8, 0xE8, 0xE8),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        })
        {
            canvas.DrawLine(35, -8, 47, -10, arm);
        }

        canvas.Restore();
    }

    static void DrawExhaust(SKCanvas canvas, int f)
    {
        // 3-4 overlapping ellipses behind handle
        // handle end is around (-75, 18)
        using var p = new SKPaint { IsAntialias = true };
        float pulse = 1f + 0.15f * MathF.Sin(f * 0.6f);

        p.Color = new SKColor(0xFF, 0x80, 0x20, 160);
        canvas.DrawOval(-115, 13, 28 * pulse, 9, p);

        p.Color = new SKColor(0xFF, 0xA0, 0x30, 200);
        canvas.DrawOval(-100, 14, 22 * pulse, 8, p);

        p.Color = new SKColor(0xFF, 0xD0, 0x40, 230);
        canvas.DrawOval(-88, 16, 16 * pulse, 7, p);

        p.Color = new SKColor(0xFF, 0xF0, 0xB0, 220);
        canvas.DrawOval(-80, 17, 8, 5, p);
    }

    static void DrawHUD(SKCanvas canvas, int f)
    {
        // count disruptions: how many pipe pairs we've passed
        int count = 0;
        float worldX = (f - TITLE_END) * 2.4f;
        const float BIRD_X = 200;
        int? lastPassed = null;
        for (int i = 0; i < Pipes.Length; i++)
        {
            float px = Pipes[i].X0 - worldX;
            if (px + 50 < BIRD_X) { count = i + 1; lastPassed = i; }
        }

        // flash effect for 12 frames after passing a pipe
        bool flash = false;
        if (lastPassed.HasValue)
        {
            float px = Pipes[lastPassed.Value].X0 - worldX;
            if (px + 50 < BIRD_X && BIRD_X - (px + 50) < 28) flash = true;
        }

        using var lblPaint = new SKPaint
        {
            IsAntialias = true,
            Color = flash ? new SKColor(0xFF, 0xFF, 0xC0) : new SKColor(0xFF, 0xD0, 0x40),
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        using var shadow = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x04, 0x0E, 0x1E, 220),
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        string txt = $"DISRUPTIONS: {count}";
        canvas.DrawText(txt, 22, 42, shadow);
        canvas.DrawText(txt, 20, 40, lblPaint);
    }

    static void DrawGameOver(SKCanvas canvas, int f)
    {
        int local = f - GAMEPLAY_END;
        float t = Math.Clamp(local / 30f, 0, 1);
        // overlay
        using (var ov = new SKPaint { Color = new SKColor(0x04, 0x08, 0x14, (byte)(180 * t)) })
            canvas.DrawRect(0, 0, W, H, ov);

        if (local < 5) return;

        using var disrupted = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xCC, 0x20, 0x20),
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        using var disruptedShadow = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x40, 0x05, 0x05),
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        var dstr = "DISRUPTED";
        var dw = disrupted.MeasureText(dstr);
        canvas.DrawText(dstr, W / 2 - dw / 2 + 4, 230 + 4, disruptedShadow);
        canvas.DrawText(dstr, W / 2 - dw / 2, 230, disrupted);

        // count
        int count = Pipes.Length;
        using var cp = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xD0, 0x40),
            TextSize = 36,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        var cstr = $"DISRUPTIONS: {count}";
        var cw = cp.MeasureText(cstr);
        canvas.DrawText(cstr, W / 2 - cw / 2, 290, cp);

        using var sub = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x20, 0xC0, 0xA0),
            TextSize = 24,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic)
        };
        var sstr = "The Underdogs Army never quits.";
        var sw = sub.MeasureText(sstr);
        canvas.DrawText(sstr, W / 2 - sw / 2, 340, sub);
    }

    static void DrawTitle(SKCanvas canvas, int f)
    {
        // bg
        using (var bg = new SKPaint { Color = new SKColor(0x04, 0x0E, 0x1E) })
            canvas.DrawRect(0, 0, W, H, bg);

        // gold spoon icon, fade in
        float fade = Math.Clamp(f / 30f, 0, 1);
        canvas.Save();
        canvas.Translate(W / 2f, 140);
        // big spoon
        using (var handle = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20).WithAlpha((byte)(255 * fade)),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 16,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        })
        {
            canvas.DrawLine(0, 0, 0, -80, handle);
        }
        using (var bowl = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD0, 0x40).WithAlpha((byte)(255 * fade)),
            IsAntialias = true
        })
        {
            canvas.DrawOval(0, 30, 50, 35, bowl);
        }
        using (var rim = new SKPaint
        {
            Color = new SKColor(0xC4, 0x9A, 0x20).WithAlpha((byte)(255 * fade)),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        })
        {
            canvas.DrawOval(0, 30, 50, 35, rim);
        }
        using (var shine = new SKPaint
        {
            Color = new SKColor(0xFF, 0xF0, 0xB0).WithAlpha((byte)(200 * fade)),
            IsAntialias = true
        })
        {
            canvas.DrawOval(-15, 22, 18, 8, shine);
        }
        canvas.Restore();

        // titles
        using var title = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xD0, 0x40),
            TextSize = 64,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        var ts = "10,000 SPOONS";
        var tw = title.MeasureText(ts);
        // shadow
        using (var ts2 = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x8B, 0x69, 0x14),
            TextSize = 64,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        })
        {
            canvas.DrawText(ts, W / 2 - tw / 2 + 3, 240 + 3, ts2);
        }
        canvas.DrawText(ts, W / 2 - tw / 2, 240, title);

        using var sub = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xE0, 0xE0, 0xE0),
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        var ss = "UNDERDOGS ARMY";
        var sw = sub.MeasureText(ss);
        canvas.DrawText(ss, W / 2 - sw / 2, 310, sub);

        using var tag = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x20, 0xC0, 0xA0),
            TextSize = 22,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic)
        };
        var gs = "Small doesn't mean weak. It means FAST.";
        var gw = tag.MeasureText(gs);
        canvas.DrawText(gs, W / 2 - gw / 2, 360, tag);

        // flash
        if (f >= 55)
        {
            float a = (f - 55) / 4f;
            using var fl = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(a, 0, 1))) };
            canvas.DrawRect(0, 0, W, H, fl);
        }
    }

    static void DrawVignette(SKCanvas canvas)
    {
        using var p = new SKPaint();
        using var sh = SKShader.CreateRadialGradient(
            new SKPoint(W / 2f, H / 2f), W * 0.7f,
            new[] { new SKColor(0, 0, 0, 0), new SKColor(0x04, 0x0E, 0x1E, 200) },
            new[] { 0.65f, 1f },
            SKShaderTileMode.Clamp);
        p.Shader = sh;
        canvas.DrawRect(0, 0, W, H, p);
    }
}
