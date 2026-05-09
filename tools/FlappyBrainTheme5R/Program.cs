using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme5R;

public static class Program
{
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL = 900;
    const string OUT_DIR = "/tmp/fb-t5r-frames";
    const string KOALA_PATH = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";

    static readonly int[] FLAPS = new[] { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    // physics
    const float GRAVITY = 0.42f;
    const float FLAP_VEL = -7.6f;
    const float BIRD_X = 220f;

    // pipes
    const float PIPE_SPEED = 2.6f;
    const float PIPE_GAP = 200f;
    const float PIPE_WIDTH = 108f;
    const float PIPE_SPACING = 320f;

    // star particle
    class Star
    {
        public float X, Y, VX, VY, Size, Alpha;
        public SKColor Color;
    }

    // sparkle
    class Sparkle
    {
        public float X, Y, Life, MaxLife, Size;
    }

    // flap glow
    class FlapGlow
    {
        public float X, Y, Age;
    }

    public static void Main()
    {
        Directory.CreateDirectory(OUT_DIR);
        // clean
        foreach (var f in Directory.GetFiles(OUT_DIR, "frame_*.png")) File.Delete(f);

        // koala asset
        SKBitmap? koala = null;
        if (File.Exists(KOALA_PATH))
        {
            using var s = File.OpenRead(KOALA_PATH);
            koala = SKBitmap.Decode(s);
        }

        var rng = new Random(12345);

        // initialize particles
        var stars = new List<Star>();
        for (int i = 0; i < 60; i++)
        {
            stars.Add(new Star
            {
                X = (float)rng.NextDouble() * W,
                Y = (float)rng.NextDouble() * H * 0.85f,
                VX = -0.15f - (float)rng.NextDouble() * 0.4f,
                VY = (float)(rng.NextDouble() - 0.5) * 0.05f,
                Size = 1f + (float)rng.NextDouble() * 2f,
                Alpha = 0.5f + (float)rng.NextDouble() * 0.5f,
                Color = (rng.NextDouble() < 0.3) ? new SKColor(255, 220, 120) : new SKColor(240, 240, 255)
            });
        }
        var sparkles = new List<Sparkle>();
        var flapGlows = new List<FlapGlow>();

        // bird state
        float birdY = 280f;
        float birdVel = 0f;
        int score = 0;

        // pipes - list of (x, gapY)
        var pipes = new List<(float X, float GapY)>();
        float pipeSpawnX = W + 100f;
        // initialize first few pipes
        for (int i = 0; i < 4; i++)
        {
            float gy = 200f + (float)rng.NextDouble() * 200f;
            pipes.Add((pipeSpawnX + i * PIPE_SPACING, gy));
        }
        var passedPipes = new HashSet<int>();
        int pipeIdCounter = pipes.Count;
        var pipeIds = new List<int>();
        for (int i = 0; i < pipes.Count; i++) pipeIds.Add(i);

        bool gameOver = false;
        int gameOverFrame = 840;

        for (int frame = 0; frame < TOTAL; frame++)
        {
            using var surface = SKSurface.Create(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;

            DrawSky(canvas);
            DrawAurora(canvas, frame);
            DrawStarsBg(canvas, stars, frame);
            DrawShootingStar(canvas, frame);

            // parallax landmarks
            DrawUluru(canvas, frame);
            DrawHarbourBridge(canvas, frame);
            DrawOperaHouse(canvas, frame);

            // gameplay region (60-839)
            bool inTitle = frame < 60;
            bool inGameOver = frame >= gameOverFrame;
            bool inGameplay = !inTitle && !inGameOver;

            // physics during gameplay
            if (inGameplay)
            {
                // flap?
                if (Array.IndexOf(FLAPS, frame) >= 0)
                {
                    birdVel = FLAP_VEL;
                    flapGlows.Add(new FlapGlow { X = BIRD_X, Y = birdY, Age = 0 });
                    // sparkles burst
                    for (int i = 0; i < 8; i++)
                    {
                        sparkles.Add(new Sparkle
                        {
                            X = BIRD_X + (float)(rng.NextDouble() - 0.5) * 60,
                            Y = birdY + (float)(rng.NextDouble() - 0.5) * 60,
                            Life = 0,
                            MaxLife = 20 + (float)rng.NextDouble() * 15,
                            Size = 4 + (float)rng.NextDouble() * 4
                        });
                    }
                }
                birdVel += GRAVITY;
                if (birdVel > 11) birdVel = 11;
                birdY += birdVel;
                if (birdY < 60) { birdY = 60; birdVel = 0; }
                if (birdY > H - 60) { birdY = H - 60; birdVel = 0; }

                // move pipes
                for (int i = 0; i < pipes.Count; i++)
                {
                    pipes[i] = (pipes[i].X - PIPE_SPEED, pipes[i].GapY);
                }
                // remove off-screen, add new
                while (pipes.Count > 0 && pipes[0].X < -PIPE_WIDTH)
                {
                    pipes.RemoveAt(0);
                    pipeIds.RemoveAt(0);
                }
                while (pipes.Count < 5)
                {
                    float lastX = pipes.Count > 0 ? pipes[^1].X : W + 100;
                    float gy = 200f + (float)rng.NextDouble() * 200f;
                    pipes.Add((lastX + PIPE_SPACING, gy));
                    pipeIds.Add(pipeIdCounter++);
                }

                // score
                for (int i = 0; i < pipes.Count; i++)
                {
                    int id = pipeIds[i];
                    if (!passedPipes.Contains(id) && pipes[i].X + PIPE_WIDTH / 2 < BIRD_X)
                    {
                        passedPipes.Add(id);
                        score++;
                    }
                }
            }

            // draw pipes
            foreach (var p in pipes)
            {
                DrawPipe(canvas, p.X, p.GapY);
            }

            // draw flap glows
            for (int i = flapGlows.Count - 1; i >= 0; i--)
            {
                var g = flapGlows[i];
                float alpha = Math.Max(0, 1f - g.Age / 18f);
                if (alpha <= 0) { flapGlows.RemoveAt(i); continue; }
                using var pp = new SKPaint { IsAntialias = true, Color = new SKColor(192, 64, 224, (byte)(alpha * 64)), Style = SKPaintStyle.Fill };
                canvas.DrawCircle(g.X, g.Y, 80f * (0.6f + g.Age / 18f * 0.6f), pp);
                g.Age++;
            }

            // draw sparkles
            for (int i = sparkles.Count - 1; i >= 0; i--)
            {
                var sp = sparkles[i];
                if (sp.Life >= sp.MaxLife) { sparkles.RemoveAt(i); continue; }
                float a = 1f - sp.Life / sp.MaxLife;
                using var pp = new SKPaint { IsAntialias = true, Color = new SKColor(255, 208, 64, (byte)(a * 220)), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
                float sz = sp.Size;
                canvas.DrawLine(sp.X - sz, sp.Y, sp.X + sz, sp.Y, pp);
                canvas.DrawLine(sp.X, sp.Y - sz, sp.X, sp.Y + sz, pp);
                sp.Life++;
                sp.Y -= 0.3f;
            }

            // bird
            if (!inTitle)
            {
                DrawKoala(canvas, koala, BIRD_X, birdY, birdVel);
            }

            DrawVignette(canvas);

            // HUD
            if (inGameplay)
            {
                DrawScore(canvas, score);
            }

            if (inTitle) DrawTitleCard(canvas, frame);
            if (inGameOver) DrawGameOver(canvas, frame - gameOverFrame, score);

            // save
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.OpenWrite($"{OUT_DIR}/frame_{frame:D4}.png");
            data.SaveTo(fs);

            if (frame % 60 == 0) Console.WriteLine($"frame {frame}/{TOTAL}");
        }

        Console.WriteLine("frames done");
    }

    static void DrawSky(SKCanvas c)
    {
        // top half: 0A0520 -> 2A1060 -> 6A20B0 -> C040E0 (mid)
        // bottom half: C040E0 -> 0A0520 (fade back)
        var colors = new SKColor[]
        {
            new SKColor(0x0A, 0x05, 0x20),
            new SKColor(0x2A, 0x10, 0x60),
            new SKColor(0x6A, 0x20, 0xB0),
            new SKColor(0xC0, 0x40, 0xE0),
            new SKColor(0x6A, 0x20, 0xB0),
            new SKColor(0x2A, 0x10, 0x60),
            new SKColor(0x0A, 0x05, 0x20),
        };
        var positions = new float[] { 0f, 0.25f, 0.5f, 0.65f, 0.8f, 0.92f, 1f };
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, H), colors, positions, SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, W, H, paint);
    }

    static void DrawAurora(SKCanvas c, int frame)
    {
        // 3 wavy sine bands across upper sky
        var bands = new[]
        {
            new { Y = 70f, Color = new SKColor(64, 255, 128, (byte)(0.15f * 255)), Phase = 0f, Tall = 28f },
            new { Y = 120f, Color = new SKColor(64, 192, 255, (byte)(0.12f * 255)), Phase = 1.2f, Tall = 24f },
            new { Y = 175f, Color = new SKColor(255, 64, 192, (byte)(0.10f * 255)), Phase = 2.4f, Tall = 22f },
        };
        foreach (var b in bands)
        {
            using var path = new SKPath();
            float top0 = b.Y + (float)Math.Sin(b.Phase + frame / 60f) * 6f;
            path.MoveTo(0, top0);
            for (int x = 0; x <= W; x += 8)
            {
                float y = b.Y + (float)Math.Sin(x / 80f + b.Phase + frame / 60f) * 12f;
                path.LineTo(x, y);
            }
            for (int x = W; x >= 0; x -= 8)
            {
                float y = b.Y + b.Tall + (float)Math.Sin(x / 80f + b.Phase + frame / 60f) * 12f;
                path.LineTo(x, y);
            }
            path.Close();
            using var paint = new SKPaint { IsAntialias = true, Color = b.Color, Style = SKPaintStyle.Fill };
            c.DrawPath(path, paint);
        }
    }

    static void DrawStarsBg(SKCanvas c, List<Star> stars, int frame)
    {
        foreach (var s in stars)
        {
            s.X += s.VX;
            s.Y += s.VY;
            if (s.X < -5) s.X = W + 5;
            if (s.Y < 0) s.Y = H * 0.85f;
            if (s.Y > H * 0.85f) s.Y = 0;

            // twinkle
            float tw = 0.7f + 0.3f * (float)Math.Sin(frame * 0.1f + s.X);
            byte a = (byte)Math.Min(255, s.Alpha * tw * 255);
            using var p = new SKPaint { IsAntialias = true, Color = s.Color.WithAlpha(a), Style = SKPaintStyle.Fill };
            c.DrawCircle(s.X, s.Y, s.Size, p);
        }

        // 4-point gold star: occasional
        var rng = new Random(frame / 30);
        if (rng.NextDouble() < 0.3)
        {
            float x = (float)rng.NextDouble() * W;
            float y = (float)rng.NextDouble() * H * 0.7f;
            using var sp = new SKPaint { IsAntialias = true, Color = new SKColor(255, 208, 64, 200), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
            c.DrawLine(x - 8, y, x + 8, y, sp);
            c.DrawLine(x, y - 8, x, y + 8, sp);
        }
    }

    static void DrawShootingStar(SKCanvas c, int frame)
    {
        var triggers = new[] { 240, 480, 720 };
        foreach (var t in triggers)
        {
            int local = frame - t;
            if (local < 0 || local >= 8) continue;
            float prog = local / 8f;
            float x = 760 + (180 - 760) * prog;
            float y = 40 + (290 - 40) * prog;
            // trail
            for (int i = 0; i < 5; i++)
            {
                float tprog = Math.Max(0, prog - i * 0.04f);
                float tx = 760 + (180 - 760) * tprog;
                float ty = 40 + (290 - 40) * tprog;
                byte a = (byte)(255 * (1f - i / 5f) * 0.7f);
                using var p = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, a), StrokeWidth = 2f - i * 0.3f, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
                // small line at this point
                float dx = (760 - 180) / 60f;
                float dy = (40 - 290) / 60f;
                c.DrawLine(tx, ty, tx + dx * 4, ty + dy * 4, p);
            }
            // head
            using var hp = new SKPaint { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Fill };
            c.DrawCircle(x, y, 3, hp);
        }
    }

    static void DrawUluru(SKCanvas c, int frame)
    {
        // parallax 0.08x
        float offset = -frame * 0.08f * PIPE_SPEED;
        float baseX = W * 0.65f + offset % 1200f;
        float y = 370f;
        // glow halo behind
        using var halo = new SKPaint { IsAntialias = true, Color = new SKColor(255, 128, 64, (byte)(0.12f * 255)), Style = SKPaintStyle.Fill };
        c.DrawOval(baseX, y - 30, 420f / 2, 160f / 2, halo);
        // shadow below floating
        using var sh = new SKPaint { IsAntialias = true, Color = new SKColor(26, 10, 0, 64), Style = SKPaintStyle.Fill };
        c.DrawOval(baseX, y + 80, 300f / 2, 20f / 2, sh);
        // main monolith - rounded-top rectangle
        var rect = new SKRect(baseX - 175, y, baseX + 175, y + 120);
        using var fill = new SKPaint { IsAntialias = true, Color = new SKColor(0xC2, 0x40, 0x10), Style = SKPaintStyle.Fill };
        // path with rounded top
        using var path = new SKPath();
        float r = 60f;
        path.MoveTo(rect.Left, rect.Bottom);
        path.LineTo(rect.Left, rect.Top + r);
        path.QuadTo(rect.Left, rect.Top, rect.Left + r, rect.Top);
        path.LineTo(rect.Right - r, rect.Top);
        path.QuadTo(rect.Right, rect.Top, rect.Right, rect.Top + r);
        path.LineTo(rect.Right, rect.Bottom);
        path.Close();
        c.DrawPath(path, fill);
        // striations
        using var stp = new SKPaint { IsAntialias = false, Color = new SKColor(0xA0, 0x30, 0x08), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        c.Save();
        c.ClipPath(path);
        for (float ly = rect.Top + 10; ly < rect.Bottom; ly += 15)
        {
            c.DrawLine(rect.Left, ly, rect.Right, ly, stp);
        }
        c.Restore();
    }

    static void DrawOperaHouse(SKCanvas c, int frame)
    {
        // mid parallax 0.25x
        float offset = -frame * 0.25f * PIPE_SPEED;
        float baseX = (W * 0.2f + offset % 1500f + 1500f) % 1500f - 200f;
        float y = 280f;
        // platform
        using var plat = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x2A, 0x3A), Style = SKPaintStyle.Fill };
        c.DrawRect(baseX - 100, y + 100, 200, 20, plat);
        // 3 sails - tapering arc-triangles
        DrawShellSail(c, baseX - 60, y + 100, 70, 130);
        DrawShellSail(c, baseX - 10, y + 100, 80, 150);
        DrawShellSail(c, baseX + 50, y + 100, 70, 110);
    }

    static void DrawShellSail(SKCanvas c, float bx, float by, float w, float h)
    {
        using var path = new SKPath();
        // base at by, peak at top right
        path.MoveTo(bx - w / 2, by);
        path.LineTo(bx + w / 2, by);
        // curve up to a point
        path.QuadTo(bx + w / 2, by - h * 0.7f, bx + w / 2 - 12, by - h);
        path.QuadTo(bx - w / 2, by - h * 0.6f, bx - w / 2, by);
        path.Close();
        using var p = new SKPaint { IsAntialias = true, Color = new SKColor(0xF0, 0xF0, 0xE8), Style = SKPaintStyle.Fill };
        c.DrawPath(path, p);
        using var st = new SKPaint { IsAntialias = true, Color = new SKColor(0xC0, 0xC0, 0xB8), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        c.DrawPath(path, st);
    }

    static void DrawHarbourBridge(SKCanvas c, int frame)
    {
        // mid 0.2x
        float offset = -frame * 0.2f * PIPE_SPEED;
        float baseX = (W * 0.85f + offset % 1700f + 1700f) % 1700f - 100f;
        float deckY = 320f;
        float archTop = deckY - 130f;
        // arch
        using var ap = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x4A), StrokeWidth = 6, Style = SKPaintStyle.Stroke };
        var archRect = new SKRect(baseX - 150, archTop, baseX + 150, deckY + 130);
        c.DrawArc(archRect, 180, 180, false, ap);
        // pylons
        using var pf = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x4A), Style = SKPaintStyle.Fill };
        c.DrawRect(baseX - 160, archTop, 20, 80, pf);
        c.DrawRect(baseX + 140, archTop, 20, 80, pf);
        // deck
        using var df = new SKPaint { IsAntialias = true, Color = new SKColor(0x5A, 0x5A, 0x5A), Style = SKPaintStyle.Fill };
        c.DrawRect(baseX - 160, deckY, 320, 12, df);
        // suspension cables
        using var cp = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x4A, 0x4A, 200), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        for (int x = -150; x <= 150; x += 25)
        {
            float dx = baseX + x;
            // arch height at this x
            float t = (x + 150) / 300f;
            float ay = deckY - (float)Math.Sin(t * Math.PI) * 130f;
            c.DrawLine(dx, deckY, dx, ay, cp);
        }
    }

    static void DrawPipe(SKCanvas c, float x, float gapY)
    {
        // top column (above gap)
        DrawPipeColumn(c, x, 0, gapY - PIPE_GAP / 2, true);
        // bottom column (below gap)
        DrawPipeColumn(c, x, gapY + PIPE_GAP / 2, H, false);
    }

    static void DrawPipeColumn(SKCanvas c, float cx, float top, float bottom, bool isTop)
    {
        if (bottom - top < 10) return;
        float bodyW = PIPE_WIDTH - 12;
        float bodyLeft = cx - bodyW / 2;
        // cap
        float capH = 34;
        float capY = isTop ? bottom - capH : top;
        float bodyTop = isTop ? top : top + capH;
        float bodyBot = isTop ? bottom - capH : bottom;

        // body alternating blocks
        using var blockA = new SKPaint { IsAntialias = true, Color = new SKColor(0x8B, 0x80, 0x70), Style = SKPaintStyle.Fill };
        using var blockB = new SKPaint { IsAntialias = true, Color = new SKColor(0xA0, 0x90, 0x80), Style = SKPaintStyle.Fill };
        using var mortar = new SKPaint { IsAntialias = false, Color = new SKColor(0x50, 0x50, 0x40), Style = SKPaintStyle.Fill };

        int idx = 0;
        for (float by = bodyTop; by < bodyBot; by += 28)
        {
            float h = Math.Min(28, bodyBot - by);
            var paint = (idx % 2 == 0) ? blockA : blockB;
            c.DrawRect(bodyLeft, by, bodyW, h - 1, paint);
            // mortar gap
            if (by + h < bodyBot)
                c.DrawRect(bodyLeft, by + h - 1, bodyW, 1, mortar);
            // moss patches
            if ((int)(by + cx) % 7 == 0)
            {
                using var moss = new SKPaint { IsAntialias = true, Color = new SKColor(0x4A, 0x6A, 0x20, 80), Style = SKPaintStyle.Fill };
                c.DrawOval(bodyLeft + (idx * 13) % bodyW, by + 8, 8, 4, moss);
            }
            idx++;
        }

        // cap (keystone block)
        using var capPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0x70, 0x60, 0x50), Style = SKPaintStyle.Fill };
        c.DrawRect(cx - PIPE_WIDTH / 2, capY, PIPE_WIDTH, capH, capPaint);
        // cap edge highlight
        using var edge = new SKPaint { IsAntialias = false, Color = new SKColor(0x90, 0x80, 0x70), Style = SKPaintStyle.Fill };
        c.DrawRect(cx - PIPE_WIDTH / 2, capY, PIPE_WIDTH, 3, edge);
        using var dark = new SKPaint { IsAntialias = false, Color = new SKColor(0x40, 0x35, 0x28), Style = SKPaintStyle.Fill };
        c.DrawRect(cx - PIPE_WIDTH / 2, capY + capH - 3, PIPE_WIDTH, 3, dark);
    }

    static void DrawKoala(SKCanvas c, SKBitmap? koala, float cx, float cy, float vel)
    {
        float angle = Math.Clamp(vel * 3.5f, -25f, 60f);
        c.Save();
        c.Translate(cx, cy);
        c.RotateDegrees(angle);
        if (koala != null)
        {
            float w = 160, h = 130;
            var dest = new SKRect(-w / 2, -h / 2, w / 2, h / 2);
            using var p = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            c.DrawBitmap(koala, dest, p);
        }
        else
        {
            // fallback ellipse
            using var p = new SKPaint { IsAntialias = true, Color = new SKColor(120, 120, 130), Style = SKPaintStyle.Fill };
            c.DrawOval(0, 0, 70, 50, p);
        }
        c.Restore();
    }

    static void DrawScore(SKCanvas c, int score)
    {
        using var tf = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var paint = new SKPaint { IsAntialias = true, TextSize = 28, Typeface = tf };
        var txt = $"SCORE: {score}";
        // shadow
        paint.Color = new SKColor(60, 20, 100, 180);
        c.DrawText(txt, 22, 42, paint);
        paint.Color = new SKColor(0xE0, 0xC0, 0xFF);
        c.DrawText(txt, 20, 40, paint);
    }

    static void DrawTitleCard(SKCanvas c, int frame)
    {
        using var bg = new SKPaint { Color = new SKColor(20, 5, 50, (byte)(0.6f * 255)) };
        c.DrawRect(0, 0, W, H, bg);
        using var tf = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var t1 = new SKPaint { IsAntialias = true, TextSize = 52, Typeface = tf, Color = new SKColor(0xC0, 0x40, 0xE0), TextAlign = SKTextAlign.Center };
        // shadow
        using var t1s = new SKPaint { IsAntialias = true, TextSize = 52, Typeface = tf, Color = new SKColor(0, 0, 0, 180), TextAlign = SKTextAlign.Center };
        c.DrawText("DISPLACED LANDMARKS", W / 2 + 3, H / 2 - 17, t1s);
        c.DrawText("DISPLACED LANDMARKS", W / 2, H / 2 - 20, t1);
        using var t2 = new SKPaint { IsAntialias = true, TextSize = 30, Typeface = tf, Color = new SKColor(0x80, 0x60, 0xFF), TextAlign = SKTextAlign.Center };
        c.DrawText("FlappyBrain 🧠", W / 2, H / 2 + 30, t2);
    }

    static void DrawGameOver(SKCanvas c, int local, int score)
    {
        using var bg = new SKPaint { Color = new SKColor(20, 5, 50, 180) };
        c.DrawRect(0, 0, W, H, bg);
        using var tf = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var t = new SKPaint { IsAntialias = true, TextSize = 80, Typeface = tf, Color = new SKColor(0xC0, 0x40, 0xE0), TextAlign = SKTextAlign.Center };
        using var ts = new SKPaint { IsAntialias = true, TextSize = 80, Typeface = tf, Color = new SKColor(0, 0, 0, 200), TextAlign = SKTextAlign.Center };
        c.DrawText("GAME OVER", W / 2 + 4, H / 2 + 4, ts);
        c.DrawText("GAME OVER", W / 2, H / 2, t);
        using var t2 = new SKPaint { IsAntialias = true, TextSize = 36, Typeface = tf, Color = new SKColor(0xE0, 0xC0, 0xFF), TextAlign = SKTextAlign.Center };
        c.DrawText($"SCORE: {score}", W / 2, H / 2 + 60, t2);
    }

    static void DrawVignette(SKCanvas c)
    {
        // radial dark purple corners
        using var p = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(W / 2, H / 2),
                Math.Max(W, H) * 0.7f,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(20, 5, 40, 160) },
                new[] { 0.6f, 1f },
                SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, W, H, p);
    }
}
