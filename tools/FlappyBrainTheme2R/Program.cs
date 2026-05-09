using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme2R;

class Program
{
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL_FRAMES = 900;
    const int TITLE_END = 60;
    const int GAMEOVER_START = 840;

    const float GROUND_HEIGHT = 90f;
    const float PIPE_WIDTH = 80f;
    const float GAP = 180f;
    const float SCROLL = 2.5f;

    static readonly int[] FLAP_FRAMES = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    static SKBitmap? koalaBmp;

    static void Main()
    {
        string outDir = "/tmp/fb-t2r-frames";
        Directory.CreateDirectory(outDir);

        string assetPath = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
        if (File.Exists(assetPath))
        {
            using var s = File.OpenRead(assetPath);
            koalaBmp = SKBitmap.Decode(s);
        }

        // Physics
        float birdX = 220f;
        float birdY = 300f;
        float birdVy = 0f;
        const float gravity = 0.55f;
        const float flapImpulse = -8.5f;
        float birdRotation = 0f;
        int lastFlapFrame = -100;

        // World scroll
        float worldX = 0f;

        // Pipes — initial pipes seeded
        var pipes = new List<Pipe>();
        var rand = new Random(7);
        float pipeSpawnX = 900f;
        for (int i = 0; i < 4; i++)
        {
            float gapY = 180 + rand.Next(0, 180);
            pipes.Add(new Pipe { X = pipeSpawnX + i * 280f, GapY = gapY, Scored = false });
        }

        // Clouds
        var clouds = new List<Cloud>();
        for (int i = 0; i < 5; i++)
        {
            clouds.Add(new Cloud
            {
                X = rand.Next(0, W),
                Y = 40 + rand.Next(0, 180),
                Scale = 0.7f + (float)rand.NextDouble() * 0.6f,
                Seed = rand.Next()
            });
        }

        // Far hills (parallax 0.1x) — generated as sine bumps
        var hills = new List<Hill>();
        for (int i = 0; i < 8; i++)
        {
            hills.Add(new Hill
            {
                X = i * 180f,
                Width = 220f + rand.Next(0, 80),
                Height = 80f + rand.Next(0, 50)
            });
        }

        // Mid trees (0.2x parallax)
        var trees = new List<Tree>();
        for (int i = 0; i < 6; i++)
        {
            trees.Add(new Tree
            {
                X = i * 160f + rand.Next(-30, 30),
                Height = 90f + rand.Next(0, 60),
                Seed = rand.Next()
            });
        }

        // Ambient birds (V-shapes wheeling)
        var ambBirds = new List<AmbientBird>();
        for (int i = 0; i < 6; i++)
        {
            ambBirds.Add(new AmbientBird
            {
                X = rand.Next(0, W),
                Y = 80 + rand.Next(0, 220),
                Phase = (float)rand.NextDouble() * MathF.PI * 2,
                Speed = 0.3f + (float)rand.NextDouble() * 0.4f
            });
        }

        // Particles (leaves and petals)
        var particles = new List<Particle>();

        int score = 0;

        for (int frame = 0; frame < TOTAL_FRAMES; frame++)
        {
            bool isTitle = frame < TITLE_END;
            bool isGameOver = frame >= GAMEOVER_START;
            bool isGameplay = !isTitle && !isGameOver;

            // Update bird
            if (isGameplay)
            {
                if (Array.IndexOf(FLAP_FRAMES, frame) >= 0)
                {
                    birdVy = flapImpulse;
                    lastFlapFrame = frame;
                }
                birdVy += gravity;
                birdY += birdVy;
                if (birdY < 60) { birdY = 60; birdVy = 0; }
                if (birdY > H - GROUND_HEIGHT - 60) { birdY = H - GROUND_HEIGHT - 60; birdVy = 0; }
                birdRotation = Math.Clamp(birdVy * 3f, -25f, 55f);

                worldX += SCROLL;

                // Move pipes
                foreach (var p in pipes) p.X -= SCROLL;
                // Recycle
                for (int i = 0; i < pipes.Count; i++)
                {
                    if (pipes[i].X < -PIPE_WIDTH - 20)
                    {
                        float maxX = 0;
                        foreach (var pp in pipes) if (pp.X > maxX) maxX = pp.X;
                        pipes[i].X = maxX + 280f;
                        pipes[i].GapY = 180 + rand.Next(0, 180);
                        pipes[i].Scored = false;
                    }
                    if (!pipes[i].Scored && pipes[i].X + PIPE_WIDTH < birdX)
                    {
                        pipes[i].Scored = true;
                        score++;
                    }
                }
            }
            else if (isTitle)
            {
                // Gentle hover during title
                birdY = 300 + MathF.Sin(frame * 0.1f) * 6f;
                birdRotation = MathF.Sin(frame * 0.1f) * 4f;
                worldX += SCROLL * 0.5f;
                foreach (var p in pipes) p.X -= SCROLL * 0.5f;
            }
            else // game over — koala falls
            {
                birdVy += gravity * 0.8f;
                birdY += birdVy;
                if (birdY > H - GROUND_HEIGHT - 60) { birdY = H - GROUND_HEIGHT - 60; birdVy *= -0.3f; }
                birdRotation = Math.Min(birdRotation + 4f, 90f);
            }

            // Move clouds
            foreach (var c in clouds)
            {
                c.X -= 0.4f;
                if (c.X < -150) c.X = W + 50;
            }

            // Ambient birds
            foreach (var ab in ambBirds)
            {
                ab.X -= ab.Speed;
                ab.Phase += 0.05f;
                if (ab.X < -20) { ab.X = W + 20; ab.Y = 80 + rand.Next(0, 220); }
            }

            // Particles spawn
            if (frame % 4 == 0)
            {
                particles.Add(new Particle
                {
                    X = W + 10,
                    Y = rand.Next(40, H - 110),
                    Vx = -1.2f - (float)rand.NextDouble() * 0.8f,
                    Vy = 0.3f + (float)rand.NextDouble() * 0.5f,
                    Size = 3f + (float)rand.NextDouble() * 3f,
                    IsLeaf = rand.NextDouble() > 0.4,
                    Rot = (float)rand.NextDouble() * MathF.PI * 2,
                    Spin = ((float)rand.NextDouble() - 0.5f) * 0.1f
                });
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var pt = particles[i];
                pt.X += pt.Vx;
                pt.Y += pt.Vy;
                pt.Rot += pt.Spin;
                if (pt.X < -20 || pt.Y > H - GROUND_HEIGHT) particles.RemoveAt(i);
            }

            // Render
            var info = new SKImageInfo(W, H);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            DrawSky(canvas);
            DrawHills(canvas, hills, worldX);
            DrawTrees(canvas, trees, worldX);
            DrawAmbientBirds(canvas, ambBirds);
            DrawClouds(canvas, clouds);
            DrawPipes(canvas, pipes);
            DrawGround(canvas, worldX);
            DrawParticles(canvas, particles);
            DrawKoala(canvas, birdX, birdY, birdRotation, frame, lastFlapFrame);

            if (isTitle)
                DrawTitle(canvas, frame);
            else if (isGameOver)
                DrawGameOver(canvas, frame, score);
            else
                DrawScoreHud(canvas, score);

            DrawVignette(canvas);

            // Save
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 95);
            using var fs = File.OpenWrite(Path.Combine(outDir, $"frame_{frame:D4}.png"));
            data.SaveTo(fs);

            if (frame % 60 == 0) Console.WriteLine($"frame {frame}/{TOTAL_FRAMES}");
        }

        Console.WriteLine("Render complete.");
    }

    static void DrawSky(SKCanvas c)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, H),
                new[] {
                    new SKColor(0x4A, 0x90, 0xD9),
                    new SKColor(0x87, 0xCE, 0xEB),
                    new SKColor(0xE8, 0xF4, 0xFF)
                },
                new float[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, W, H, paint);
    }

    static void DrawHills(SKCanvas c, List<Hill> hills, float worldX)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0x3A, 0x8A, 0x25),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        float scroll = worldX * 0.1f;
        float baseY = H - GROUND_HEIGHT;
        foreach (var h in hills)
        {
            float x = ((h.X - scroll) % (8 * 180f) + 8 * 180f) % (8 * 180f) - 200f;
            using var path = new SKPath();
            path.MoveTo(x, baseY);
            path.QuadTo(x + h.Width / 2, baseY - h.Height, x + h.Width, baseY);
            path.Close();
            c.DrawPath(path, paint);
        }
    }

    static void DrawTrees(SKCanvas c, List<Tree> trees, float worldX)
    {
        float scroll = worldX * 0.2f;
        float baseY = H - GROUND_HEIGHT;
        foreach (var t in trees)
        {
            float x = ((t.X - scroll) % (6 * 160f) + 6 * 160f) % (6 * 160f) - 80f;
            // Trunk
            using (var p = new SKPaint { Color = new SKColor(0x7A, 0x90, 0x60), IsAntialias = true })
                c.DrawRect(x - 4, baseY - t.Height, 8, t.Height, p);
            // Drooping leaf clusters
            var rng = new Random(t.Seed);
            using (var p = new SKPaint { Color = new SKColor(0x2D, 0x7A, 0x1F, 220), IsAntialias = true })
            {
                for (int i = 0; i < 5; i++)
                {
                    float lx = x + rng.Next(-20, 20);
                    float ly = baseY - t.Height + rng.Next(-10, 30);
                    c.DrawOval(lx, ly, 18 + rng.Next(0, 12), 26 + rng.Next(0, 14), p);
                }
            }
        }
    }

    static void DrawAmbientBirds(SKCanvas c, List<AmbientBird> birds)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0x1A, 0x3A, 0x0A),
            IsAntialias = true,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round
        };
        foreach (var b in birds)
        {
            float wing = MathF.Sin(b.Phase) * 4f;
            float y = b.Y + MathF.Sin(b.Phase * 0.5f) * 2f;
            using var path = new SKPath();
            path.MoveTo(b.X - 7, y + wing);
            path.LineTo(b.X, y - 2);
            path.LineTo(b.X + 7, y + wing);
            c.DrawPath(path, paint);
        }
    }

    static void DrawClouds(SKCanvas c, List<Cloud> clouds)
    {
        foreach (var cl in clouds)
        {
            var rng = new Random(cl.Seed);
            using var paint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 230),
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateBlur(2, 2)
            };
            float s = cl.Scale;
            // Cluster of overlapping circles
            c.DrawCircle(cl.X, cl.Y, 35 * s, paint);
            c.DrawCircle(cl.X + 30 * s, cl.Y - 8, 28 * s, paint);
            c.DrawCircle(cl.X - 28 * s, cl.Y - 4, 30 * s, paint);
            c.DrawCircle(cl.X + 55 * s, cl.Y + 4, 22 * s, paint);
            c.DrawCircle(cl.X + 12 * s, cl.Y - 16, 24 * s, paint);
            c.DrawCircle(cl.X - 50 * s, cl.Y + 6, 20 * s, paint);
        }
    }

    static void DrawPipes(SKCanvas c, List<Pipe> pipes)
    {
        foreach (var p in pipes)
        {
            // Top eucalyptus (hanging down)
            float topH = p.GapY - GAP / 2;
            float botY = p.GapY + GAP / 2;
            float botH = H - GROUND_HEIGHT - botY;

            if (topH > 0) DrawEucalyptusPipe(c, p.X, 0, PIPE_WIDTH, topH, true, (int)(p.X * 13));
            if (botH > 0) DrawEucalyptusPipe(c, p.X, botY, PIPE_WIDTH, botH, false, (int)(p.X * 17));
        }
    }

    static void DrawEucalyptusPipe(SKCanvas c, float x, float y, float w, float h, bool isTop, int seed)
    {
        // Trunk base
        using (var paint = new SKPaint { Color = new SKColor(0x6B, 0x8C, 0x5A), IsAntialias = true })
            c.DrawRect(x, y, w, h, paint);

        // Bark mottles
        var rng = new Random(seed);
        int mottleCount = (int)(h / 25f) + 4;
        for (int i = 0; i < mottleCount; i++)
        {
            byte r, g, b;
            if (rng.NextDouble() > 0.5)
            { r = 0x4A; g = 0x60; b = 0x40; }
            else
            { r = 0x8A; g = 0xAA; b = 0x70; }
            using var paint = new SKPaint { Color = new SKColor(r, g, b, 200), IsAntialias = true };
            float mx = x + (float)rng.NextDouble() * w;
            float my = y + (float)rng.NextDouble() * h;
            float mw = 15 + (float)rng.NextDouble() * 20;
            float mh = 10 + (float)rng.NextDouble() * 14;
            c.DrawOval(mx, my, mw / 2, mh / 2, paint);
        }

        // Edge shading
        using (var edge = new SKPaint { Color = new SKColor(0x3A, 0x50, 0x30, 140), IsAntialias = true })
        {
            c.DrawRect(x, y, 6, h, edge);
            c.DrawRect(x + w - 6, y, 6, h, edge);
        }

        // Leaf cluster cap — at the gap-facing end
        float capY = isTop ? y + h : y;
        DrawLeafCluster(c, x + w / 2, capY, w, isTop, seed + 99);
    }

    static void DrawLeafCluster(SKCanvas c, float cx, float cy, float w, bool isTop, int seed)
    {
        var rng = new Random(seed);
        int leaves = 12;
        for (int i = 0; i < leaves; i++)
        {
            byte g = (byte)(rng.NextDouble() > 0.5 ? 0x7A : 0x50);
            byte gg = (byte)(rng.NextDouble() > 0.5 ? 0x2D : 0x1A);
            using var paint = new SKPaint
            {
                Color = new SKColor(gg, g, gg == 0x2D ? (byte)0x1F : (byte)0x10, 240),
                IsAntialias = true
            };
            float lx = cx + ((float)rng.NextDouble() - 0.5f) * w * 1.4f;
            float ly = cy + (isTop ? 1 : -1) * ((float)rng.NextDouble() * 18f - 4f);
            float lw = 20 + (float)rng.NextDouble() * 30;
            float lh = 14 + (float)rng.NextDouble() * 20;
            c.Save();
            c.Translate(lx, ly);
            c.RotateDegrees(((float)rng.NextDouble() - 0.5f) * 60f);
            c.DrawOval(0, 0, lw / 2, lh / 2, paint);
            c.Restore();
        }
    }

    static void DrawGround(SKCanvas c, float worldX)
    {
        float gy = H - GROUND_HEIGHT;
        // Canopy gradient
        using (var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, gy),
                new SKPoint(0, H),
                new[] { new SKColor(0x2D, 0x7A, 0x1F), new SKColor(0x1A, 0x50, 0x10) },
                SKShaderTileMode.Clamp)
        })
        {
            c.DrawRect(0, gy, W, GROUND_HEIGHT, paint);
        }

        // Canopy texture — overlapping leaf bumps
        var rng = new Random(42);
        using (var paint = new SKPaint { Color = new SKColor(0x3A, 0x8F, 0x28, 220), IsAntialias = true })
        {
            for (int i = 0; i < 30; i++)
            {
                float bx = (i * 31f - worldX * 0.6f) % (W + 60);
                if (bx < -30) bx += W + 60;
                float by = gy + 4 + (i % 3) * 4;
                c.DrawOval(bx, by, 22, 14, paint);
            }
        }
        using (var paint = new SKPaint { Color = new SKColor(0x1A, 0x50, 0x10, 200), IsAntialias = true })
        {
            for (int i = 0; i < 20; i++)
            {
                float bx = (i * 47f - worldX * 0.6f) % (W + 60);
                if (bx < -30) bx += W + 60;
                float by = gy + 30 + (i % 4) * 6;
                c.DrawOval(bx, by, 26, 16, paint);
            }
        }
    }

    static void DrawParticles(SKCanvas c, List<Particle> ps)
    {
        foreach (var p in ps)
        {
            using var paint = new SKPaint
            {
                Color = p.IsLeaf
                    ? new SKColor(0x90, 0xC0, 0x60, 220)
                    : new SKColor(0xFF, 0xFF, 0xFF, 230),
                IsAntialias = true
            };
            c.Save();
            c.Translate(p.X, p.Y);
            c.RotateRadians(p.Rot);
            if (p.IsLeaf)
                c.DrawOval(0, 0, p.Size, p.Size * 0.5f, paint);
            else
                c.DrawCircle(0, 0, p.Size * 0.6f, paint);
            c.Restore();
        }
    }

    static void DrawKoala(SKCanvas c, float x, float y, float rot, int frame, int lastFlapFrame)
    {
        // Flap glow ring
        int sinceFlap = frame - lastFlapFrame;
        if (sinceFlap >= 0 && sinceFlap < 12)
        {
            float t = sinceFlap / 12f;
            byte alpha = (byte)(255 * 0.30f * (1f - t));
            using var glow = new SKPaint
            {
                Color = new SKColor(0xF0, 0xA8, 0x32, alpha),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            c.DrawCircle(x, y, 80, glow);
        }

        c.Save();
        c.Translate(x, y);
        c.RotateDegrees(rot);
        if (koalaBmp != null)
        {
            var dst = new SKRect(-80, -65, 80, 65);
            c.DrawBitmap(koalaBmp, dst, new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true });
        }
        else
        {
            // Fallback
            using var p = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
            c.DrawOval(0, 0, 80, 65, p);
        }
        c.Restore();
    }

    static void DrawScoreHud(SKCanvas c, int score)
    {
        using var shadow = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = 28f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Left
        };
        using var fg = new SKPaint
        {
            Color = new SKColor(0x1A, 0x50, 0x10),
            IsAntialias = true,
            TextSize = 28f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Left
        };
        string txt = $"SCORE: {score}";
        c.DrawText(txt, 22, 44, shadow);
        c.DrawText(txt, 20, 42, fg);
    }

    static void DrawTitle(SKCanvas c, int frame)
    {
        // Light overlay
        byte ov = (byte)(140 - Math.Min(frame * 2, 60));
        using (var overlay = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, ov) })
            c.DrawRect(0, 0, W, H, overlay);

        float t = Math.Min(frame / 20f, 1f);
        byte alpha = (byte)(255 * t);

        using var title = new SKPaint
        {
            Color = new SKColor(0x2D, 0x7A, 0x1F, alpha),
            IsAntialias = true,
            TextSize = 64f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, alpha),
            IsAntialias = true,
            TextSize = 64f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        c.DrawText("SKY SAFARI", W / 2 + 3, 200 + 3, titleShadow);
        c.DrawText("SKY SAFARI", W / 2, 200, title);

        using var sub = new SKPaint
        {
            Color = new SKColor(0x4A, 0x90, 0xD9, alpha),
            IsAntialias = true,
            TextSize = 32f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        c.DrawText("FlappyBrain 🧠", W / 2, 250, sub);
    }

    static void DrawGameOver(SKCanvas c, int frame, int score)
    {
        int t = frame - GAMEOVER_START;
        byte ov = (byte)Math.Min(t * 5, 130);
        using (var overlay = new SKPaint { Color = new SKColor(0x1A, 0x50, 0x10, ov) })
            c.DrawRect(0, 0, W, H, overlay);

        byte alpha = (byte)Math.Min(t * 8, 255);
        using var go = new SKPaint
        {
            Color = new SKColor(0x1A, 0x50, 0x10, alpha),
            IsAntialias = true,
            TextSize = 80f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        using var goShadow = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, alpha),
            IsAntialias = true,
            TextSize = 80f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        c.DrawText("GAME OVER", W / 2 + 3, 250 + 3, goShadow);
        c.DrawText("GAME OVER", W / 2, 250, go);

        using var sc = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, alpha),
            IsAntialias = true,
            TextSize = 36f,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        c.DrawText($"Score: {score}", W / 2, 320, sc);
    }

    static void DrawVignette(SKCanvas c)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(W / 2f, H / 2f),
                MathF.Sqrt(W * W + H * H) / 2f,
                new[] {
                    new SKColor(0, 0, 0, 0),
                    new SKColor(0, 0, 0, 0),
                    new SKColor(0, 0, 0, 70)
                },
                new float[] { 0f, 0.7f, 1f },
                SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, W, H, paint);
    }

    class Pipe
    {
        public float X;
        public float GapY;
        public bool Scored;
    }

    class Cloud
    {
        public float X, Y, Scale;
        public int Seed;
    }

    class Hill
    {
        public float X, Width, Height;
    }

    class Tree
    {
        public float X, Height;
        public int Seed;
    }

    class AmbientBird
    {
        public float X, Y, Phase, Speed;
    }

    class Particle
    {
        public float X, Y, Vx, Vy, Size, Rot, Spin;
        public bool IsLeaf;
    }
}
