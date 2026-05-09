using SkiaSharp;
using System;
using System.IO;

namespace FlappyBrainRendererB2;

internal static class Program
{
    const int Width = 1280;
    const int Height = 720;
    const int Fps = 30;
    const int TotalFrames = 900;
    const string OutDir = "/tmp/fb-b2-frames";

    // Koala state
    static float koalaY = Height / 2f;
    static float koalaVy = 0f;
    const float Gravity = 0.45f;
    const float FlapImpulse = -8.5f;
    const float KoalaX = 280f;

    // Flap event frames -> trigger flap and create animation frames
    static readonly int[] FlapFrames = { 60, 120, 180, 250, 310, 380, 450, 510, 580, 650, 720, 790, 850 };

    // Pipe state - simulated pipes
    class Pipe
    {
        public float X;
        public float GapY;     // center of gap
        public float GapHeight = 200f;
        public bool HasZombies;
        public int Index;
        public bool Passed;
    }

    static List<Pipe> pipes = new();
    static List<Billboard> billboards = new();
    static int score = 0;

    class Billboard
    {
        public float X;
        public float Y;
        public string Text = "";
        public int Index;
    }

    static int Main()
    {
        Directory.CreateDirectory(OutDir);

        // Initialize pipes for gameplay sequence (frames 30-870)
        // Pipes scroll from right to left at PipeSpeed px/frame
        // At gameplay start, seed first pipe ahead of koala
        var rand = new Random(42);
        float pipeSpeed = 3.5f;
        float pipeSpacing = 320f;

        // We'll simulate full sequence and emit frames
        // Reset state
        koalaY = Height / 2f;
        koalaVy = 0f;
        pipes.Clear();
        billboards.Clear();
        score = 0;

        // Pre-seed pipes
        int pipeIndex = 0;
        for (int i = 0; i < 6; i++)
        {
            float gx = Width + 100 + i * pipeSpacing;
            float gy = 200 + (float)rand.NextDouble() * 320;
            pipes.Add(new Pipe { X = gx, GapY = gy, HasZombies = (pipeIndex % 3 == 2), Index = pipeIndex });
            pipeIndex++;
        }

        // Pre-seed billboards (between pipes)
        string[] billboardTexts = { "PATIENT ZERO", "10,000 SPOONS" };
        for (int i = 0; i < 4; i++)
        {
            float bx = Width + 250 + i * (pipeSpacing * 2);
            float by = 130 + (float)rand.NextDouble() * 60;
            billboards.Add(new Billboard { X = bx, Y = by, Text = billboardTexts[i % 2], Index = i });
        }

        // Parallax layer scroll offsets
        float farScroll = 0f, midScroll = 0f, nearScroll = 0f;

        // Dust particles
        var dust = new List<(float x, float y, float vx, float r, float a)>();
        for (int i = 0; i < 60; i++)
        {
            dust.Add((
                (float)rand.NextDouble() * Width,
                (float)rand.NextDouble() * Height,
                -1f - (float)rand.NextDouble() * 2f,
                1f + (float)rand.NextDouble() * 2f,
                0.2f + (float)rand.NextDouble() * 0.4f
            ));
        }

        // Track flap animation: when triggered, animate for 6 frames
        int flapAnimFrames = 0;

        for (int frame = 0; frame < TotalFrames; frame++)
        {
            using var bmp = new SKBitmap(Width, Height);
            using var canvas = new SKCanvas(bmp);

            DrawSky(canvas);
            DrawFarLayer(canvas, farScroll);
            DrawMidLayer(canvas, midScroll);

            if (frame < 30)
            {
                // Title card
                DrawTitleCard(canvas, frame);
            }
            else if (frame < 871)
            {
                // Gameplay
                int gp = frame - 30;

                // Update parallax
                farScroll = (farScroll + 0.3f) % Width;
                midScroll = (midScroll + 1.2f) % Width;
                nearScroll = (nearScroll + 2.5f) % Width;

                // Flap event?
                bool flapNow = Array.IndexOf(FlapFrames, frame) >= 0;
                if (flapNow)
                {
                    koalaVy = FlapImpulse;
                    flapAnimFrames = 6;
                }

                // Update physics
                koalaVy += Gravity;
                koalaY += koalaVy;
                if (koalaY < 60) { koalaY = 60; koalaVy = 0; }
                if (koalaY > Height - 100) { koalaY = Height - 100; koalaVy = 0; }

                // Near-miss at frame ~600
                if (frame == 600)
                {
                    koalaY = Math.Max(koalaY, 180); // force close to top of gap
                }

                // Update pipes
                foreach (var p in pipes)
                {
                    p.X -= pipeSpeed;
                    if (!p.Passed && p.X + 50 < KoalaX)
                    {
                        p.Passed = true;
                        score++;
                    }
                }
                // Recycle off-screen pipes
                for (int i = 0; i < pipes.Count; i++)
                {
                    if (pipes[i].X < -100)
                    {
                        float maxX = 0;
                        foreach (var pp in pipes) if (pp.X > maxX) maxX = pp.X;
                        pipes[i].X = maxX + pipeSpacing;
                        pipes[i].GapY = 200 + (float)rand.NextDouble() * 320;
                        pipeIndex++;
                        pipes[i].HasZombies = (pipeIndex % 3 == 2);
                        pipes[i].Index = pipeIndex;
                        pipes[i].Passed = false;
                    }
                }

                // Update billboards
                foreach (var b in billboards) b.X -= pipeSpeed * 0.85f;
                for (int i = 0; i < billboards.Count; i++)
                {
                    if (billboards[i].X < -300)
                    {
                        float maxX = 0;
                        foreach (var bb in billboards) if (bb.X > maxX) maxX = bb.X;
                        billboards[i].X = maxX + pipeSpacing * 2;
                        billboards[i].Y = 130 + (float)rand.NextDouble() * 60;
                        billboards[i].Text = billboardTexts[(billboards[i].Index + 1) % 2];
                        billboards[i].Index++;
                    }
                }

                // Update dust
                for (int i = 0; i < dust.Count; i++)
                {
                    var d = dust[i];
                    d.x += d.vx;
                    if (d.x < -5) d.x = Width + 5;
                    dust[i] = d;
                }

                // Draw billboards (mid layer)
                foreach (var b in billboards) DrawBillboard(canvas, b);

                // Draw pipes
                foreach (var p in pipes) DrawPipe(canvas, p);

                // Near layer (foreground debris on top of pipes? actually behind)
                DrawNearLayer(canvas, nearScroll);

                // Dust on top
                DrawDust(canvas, dust);

                // Koala
                if (flapAnimFrames > 0) flapAnimFrames--;
                DrawKoala(canvas, KoalaX, koalaY, flapAnimFrames > 0, flapAnimFrames);

                // Score UI
                DrawScore(canvas, score);
            }
            else
            {
                // Game Over screen, with last frame of gameplay frozen behind
                DrawNearLayer(canvas, nearScroll);
                DrawDust(canvas, dust);
                foreach (var b in billboards) DrawBillboard(canvas, b);
                foreach (var p in pipes) DrawPipe(canvas, p);
                DrawKoala(canvas, KoalaX, koalaY, false, 0);
                DrawScore(canvas, score);

                int gFrame = frame - 871;
                float alpha = Math.Min(1f, gFrame / 15f);
                DrawGameOver(canvas, score, alpha);
            }

            // Save frame
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            var path = Path.Combine(OutDir, $"frame_{frame:D4}.png");
            using var fs = File.OpenWrite(path);
            data.SaveTo(fs);

            if (frame % 60 == 0) Console.WriteLine($"Frame {frame}/{TotalFrames}");
        }

        Console.WriteLine("Frames done.");
        return 0;
    }

    static void DrawSky(SKCanvas canvas)
    {
        // Vertical gradient: dark sky -> orange horizon
        using var paint = new SKPaint();
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, Height),
            new[] {
                new SKColor(0x1A, 0x0A, 0x00),
                new SKColor(0x4A, 0x18, 0x00),
                new SKColor(0xC4, 0x62, 0x2D)
            },
            new float[] { 0f, 0.55f, 1f },
            SKShaderTileMode.Clamp
        );
        canvas.DrawRect(0, 0, Width, Height, paint);

        // Hazy sun
        using var sunPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0x8C, 0x42, 90),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 40)
        };
        canvas.DrawCircle(Width * 0.72f, Height * 0.55f, 110, sunPaint);
        using var sunCore = new SKPaint
        {
            Color = new SKColor(0xFF, 0xB8, 0x6B, 200),
            IsAntialias = true
        };
        canvas.DrawCircle(Width * 0.72f, Height * 0.55f, 50, sunCore);
    }

    static void DrawFarLayer(SKCanvas canvas, float scroll)
    {
        // Dead gum tree silhouettes far away
        using var paint = new SKPaint
        {
            Color = new SKColor(0x20, 0x10, 0x08, 200),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Distant horizon haze line
        using var hazePaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, Height * 0.65f),
                new SKPoint(0, Height * 0.78f),
                new[] {
                    new SKColor(0x6A, 0x2A, 0x10, 120),
                    new SKColor(0x2A, 0x14, 0x08, 0)
                },
                SKShaderTileMode.Clamp
            )
        };
        canvas.DrawRect(0, Height * 0.65f, Width, Height * 0.13f, hazePaint);

        // Trees
        for (int i = -1; i < 8; i++)
        {
            float x = ((i * 240f) - scroll * 0.2f) % (Width + 240) ;
            if (x < -100) x += Width + 240;
            float baseY = Height * 0.72f;
            DrawDeadTree(canvas, x, baseY, 0.7f, paint);
        }
    }

    static void DrawDeadTree(SKCanvas canvas, float x, float baseY, float scale, SKPaint paint)
    {
        // Trunk
        canvas.DrawRect(x - 4 * scale, baseY - 80 * scale, 8 * scale, 80 * scale, paint);
        // Branches
        using var path = new SKPath();
        path.MoveTo(x, baseY - 60 * scale);
        path.LineTo(x - 35 * scale, baseY - 95 * scale);
        path.LineTo(x - 50 * scale, baseY - 88 * scale);
        path.MoveTo(x, baseY - 70 * scale);
        path.LineTo(x + 30 * scale, baseY - 100 * scale);
        path.LineTo(x + 44 * scale, baseY - 92 * scale);
        path.MoveTo(x, baseY - 78 * scale);
        path.LineTo(x - 20 * scale, baseY - 110 * scale);
        path.LineTo(x + 10 * scale, baseY - 120 * scale);
        using var stroke = new SKPaint
        {
            Color = paint.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3 * scale,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        canvas.DrawPath(path, stroke);
    }

    static void DrawMidLayer(SKCanvas canvas, float scroll)
    {
        // Ruined urban silhouettes - boxy buildings, cracks
        using var paint = new SKPaint
        {
            Color = new SKColor(0x3A, 0x1A, 0x10),
            IsAntialias = true
        };

        float baseY = Height * 0.78f;

        // Repeating building pattern
        for (int i = -1; i < 12; i++)
        {
            float x = (i * 180f - scroll * 0.5f);
            x = ((x % (Width + 180)) + Width + 180) % (Width + 180) - 180;

            float h = 70 + (i % 3) * 30 + ((i * 17) % 50);
            canvas.DrawRect(x, baseY - h, 100, h, paint);

            // Damaged top
            using var path = new SKPath();
            path.MoveTo(x, baseY - h);
            path.LineTo(x + 20, baseY - h - 8);
            path.LineTo(x + 40, baseY - h);
            path.LineTo(x + 65, baseY - h - 12);
            path.LineTo(x + 100, baseY - h);
            path.LineTo(x + 100, baseY - h + 5);
            path.LineTo(x, baseY - h + 5);
            path.Close();
            canvas.DrawPath(path, paint);

            // Windows (dark with occasional faint orange glow)
            using var winPaint = new SKPaint { Color = new SKColor(0x10, 0x06, 0x02), IsAntialias = false };
            using var glowPaint = new SKPaint { Color = new SKColor(0xC4, 0x62, 0x2D, 180), IsAntialias = false };
            for (int wy = 0; wy < h / 25; wy++)
            {
                for (int wx = 0; wx < 4; wx++)
                {
                    bool glow = ((i * 7 + wx * 3 + wy) % 9) == 0;
                    canvas.DrawRect(x + 12 + wx * 22, baseY - h + 12 + wy * 25, 10, 14, glow ? glowPaint : winPaint);
                }
            }
        }

        // Cracked road surface
        using var roadPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, baseY),
                new SKPoint(0, Height),
                new[] {
                    new SKColor(0x2A, 0x14, 0x0A),
                    new SKColor(0x10, 0x06, 0x02)
                },
                SKShaderTileMode.Clamp
            )
        };
        canvas.DrawRect(0, baseY, Width, Height - baseY, roadPaint);

        // Cracks in road
        using var crackPaint = new SKPaint
        {
            Color = new SKColor(0x05, 0x02, 0x00),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        var crackRand = new Random(7);
        for (int i = 0; i < 12; i++)
        {
            float cx = ((i * 140 - scroll) % Width + Width) % Width;
            float cy = baseY + 10 + crackRand.Next(0, 50);
            using var p = new SKPath();
            p.MoveTo(cx, cy);
            p.LineTo(cx + 30 + crackRand.Next(-10, 20), cy + 8);
            p.LineTo(cx + 60 + crackRand.Next(-10, 20), cy + 4);
            p.LineTo(cx + 90, cy + 12);
            canvas.DrawPath(p, crackPaint);
        }
    }

    static void DrawNearLayer(SKCanvas canvas, float scroll)
    {
        // Foreground rusted pipes / debris
        using var paint = new SKPaint
        {
            Color = new SKColor(0x1A, 0x08, 0x04),
            IsAntialias = true
        };

        for (int i = -1; i < 10; i++)
        {
            float x = (i * 250f - scroll);
            x = ((x % (Width + 250)) + Width + 250) % (Width + 250) - 250;
            float y = Height - 30;
            // Debris chunk
            using var path = new SKPath();
            path.MoveTo(x, y);
            path.LineTo(x + 20, y - 20);
            path.LineTo(x + 50, y - 18);
            path.LineTo(x + 70, y - 25);
            path.LineTo(x + 100, y);
            path.Close();
            canvas.DrawPath(path, paint);
        }
    }

    static void DrawDust(SKCanvas canvas, List<(float x, float y, float vx, float r, float a)> dust)
    {
        foreach (var d in dust)
        {
            using var p = new SKPaint
            {
                Color = new SKColor(0xC4, 0x8B, 0x60, (byte)(d.a * 180)),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };
            canvas.DrawCircle(d.x, d.y, d.r, p);
        }
    }

    static void DrawPipe(SKCanvas canvas, Pipe p)
    {
        float pipeWidth = 80;
        float topPipeBottom = p.GapY - p.GapHeight / 2;
        float bottomPipeTop = p.GapY + p.GapHeight / 2;

        // Top pipe
        DrawRustedPipe(canvas, p.X, 0, pipeWidth, topPipeBottom);
        // Bottom pipe
        DrawRustedPipe(canvas, p.X, bottomPipeTop, pipeWidth, Height - bottomPipeTop);

        // Zombies on every 3rd pipe
        if (p.HasZombies)
        {
            DrawZombie(canvas, p.X + 10, topPipeBottom - 50);
            DrawZombie(canvas, p.X + 50, bottomPipeTop + 30);
            DrawZombie(canvas, p.X - 8, bottomPipeTop + 80);
        }
    }

    static void DrawRustedPipe(SKCanvas canvas, float x, float y, float w, float h)
    {
        if (h <= 0) return;
        // Body gradient
        using var bodyPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(x, 0),
                new SKPoint(x + w, 0),
                new[] {
                    new SKColor(0x5C, 0x2E, 0x00),
                    new SKColor(0x8B, 0x45, 0x13),
                    new SKColor(0x6B, 0x35, 0x08)
                },
                new float[] { 0f, 0.4f, 1f },
                SKShaderTileMode.Clamp
            )
        };
        canvas.DrawRect(x, y, w, h, bodyPaint);

        // Corrugated horizontal lines
        using var linePaint = new SKPaint
        {
            Color = new SKColor(0x3A, 0x1C, 0x04, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = false
        };
        for (float ly = y + 6; ly < y + h; ly += 12)
        {
            canvas.DrawLine(x + 2, ly, x + w - 2, ly, linePaint);
        }

        // Rust streaks
        using var rustPaint = new SKPaint
        {
            Color = new SKColor(0x2A, 0x0A, 0x00, 160),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        for (int i = 0; i < 3; i++)
        {
            float sx = x + 12 + i * 22;
            using var path = new SKPath();
            path.MoveTo(sx, y);
            path.LineTo(sx - 1, y + h * 0.4f);
            path.LineTo(sx + 1, y + h * 0.7f);
            path.LineTo(sx, y + h);
            canvas.DrawPath(path, rustPaint);
        }

        // Cap (slight flange)
        using var capPaint = new SKPaint
        {
            Color = new SKColor(0x4A, 0x22, 0x04),
            IsAntialias = false
        };
        // Cap on the closer end
        if (y == 0)
        {
            canvas.DrawRect(x - 5, y + h - 16, w + 10, 16, capPaint);
        }
        else
        {
            canvas.DrawRect(x - 5, y, w + 10, 16, capPaint);
        }

        // Outline
        using var outline = new SKPaint
        {
            Color = new SKColor(0x20, 0x0A, 0x02),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(x, y, w, h, outline);
    }

    static void DrawZombie(SKCanvas canvas, float x, float y)
    {
        // Dark humanoid silhouette with outstretched arms
        using var bodyPaint = new SKPaint
        {
            Color = new SKColor(0x14, 0x08, 0x04),
            IsAntialias = true
        };
        using var clothPaint = new SKPaint
        {
            Color = new SKColor(0x2A, 0x18, 0x10),
            IsAntialias = true
        };
        using var skinPaint = new SKPaint
        {
            Color = new SKColor(0x4A, 0x3A, 0x2A),
            IsAntialias = true
        };

        // Torso (tattered)
        using var torsoPath = new SKPath();
        torsoPath.MoveTo(x, y);
        torsoPath.LineTo(x + 14, y);
        torsoPath.LineTo(x + 16, y + 28);
        torsoPath.LineTo(x + 12, y + 30);
        torsoPath.LineTo(x + 8, y + 26);
        torsoPath.LineTo(x + 4, y + 30);
        torsoPath.LineTo(x - 2, y + 28);
        torsoPath.Close();
        canvas.DrawPath(torsoPath, clothPaint);

        // Head
        canvas.DrawCircle(x + 7, y - 5, 7, skinPaint);
        // Eye sockets
        using var eyePaint = new SKPaint { Color = new SKColor(0xFF, 0x10, 0x10), IsAntialias = true };
        canvas.DrawCircle(x + 5, y - 5, 1.2f, eyePaint);
        canvas.DrawCircle(x + 9, y - 5, 1.2f, eyePaint);

        // Arms outstretched
        using var armStroke = new SKPaint
        {
            Color = clothPaint.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        canvas.DrawLine(x + 2, y + 4, x - 12, y - 2, armStroke);
        canvas.DrawLine(x + 12, y + 4, x + 26, y - 4, armStroke);

        // Legs
        canvas.DrawLine(x + 4, y + 28, x + 2, y + 42, armStroke);
        canvas.DrawLine(x + 11, y + 28, x + 14, y + 42, armStroke);
    }

    static void DrawBillboard(SKCanvas canvas, Billboard b)
    {
        float bw = 240, bh = 90;
        float bx = b.X, by = b.Y;

        // Posts
        using var postPaint = new SKPaint
        {
            Color = new SKColor(0x4A, 0x2A, 0x10),
            IsAntialias = true
        };
        canvas.DrawRect(bx + 30, by + bh, 8, 180, postPaint);
        canvas.DrawRect(bx + bw - 38, by + bh, 8, 180, postPaint);
        // Post rust
        using var postRust = new SKPaint { Color = new SKColor(0x2A, 0x14, 0x06), IsAntialias = false };
        canvas.DrawRect(bx + 30, by + bh + 40, 8, 6, postRust);
        canvas.DrawRect(bx + bw - 38, by + bh + 90, 8, 6, postRust);

        // Billboard back panel (black weathered)
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0x0A, 0x05, 0x02),
            IsAntialias = true
        };
        canvas.DrawRect(bx, by, bw, bh, bgPaint);

        // Frame
        using var framePaint = new SKPaint
        {
            Color = new SKColor(0x6A, 0x35, 0x10),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true
        };
        canvas.DrawRect(bx, by, bw, bh, framePaint);

        // Text - red stencil style
        using var textPaint = new SKPaint
        {
            Color = new SKColor(0xC8, 0x20, 0x10),
            IsAntialias = true,
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("Impact", SKFontStyle.Bold) ??
                       SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ??
                       SKTypeface.Default,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        canvas.DrawText(b.Text, bx + bw / 2, by + bh / 2 + 10, textPaint);

        // Cracks/weathering
        using var crackPaint = new SKPaint
        {
            Color = new SKColor(0x00, 0x00, 0x00, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            IsAntialias = true
        };
        using var crack = new SKPath();
        crack.MoveTo(bx + 20, by);
        crack.LineTo(bx + 30, by + 25);
        crack.LineTo(bx + 18, by + 50);
        crack.MoveTo(bx + bw - 30, by + 10);
        crack.LineTo(bx + bw - 40, by + 35);
        crack.LineTo(bx + bw - 25, by + 60);
        canvas.DrawPath(crack, crackPaint);

        // Bullet hole
        using var hole = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawCircle(bx + bw - 50, by + 18, 4, hole);
        using var holeRing = new SKPaint
        {
            Color = new SKColor(0x40, 0x10, 0x05),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };
        canvas.DrawCircle(bx + bw - 50, by + 18, 6, holeRing);
    }

    static void DrawKoala(SKCanvas canvas, float x, float y, bool flapping, int flapPhase)
    {
        // Body rotation based on velocity (handled outside via koalaVy)
        float angle = Math.Clamp(koalaVy * 2.5f, -25f, 35f);
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(angle);

        // Shadow
        using var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 80),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
        };
        canvas.DrawOval(0, 38, 28, 8, shadow);

        // Ears (behind head)
        using var earPaint = new SKPaint { Color = new SKColor(0x88, 0x88, 0x90), IsAntialias = true };
        using var earInner = new SKPaint { Color = new SKColor(0xE8, 0xA0, 0xA8), IsAntialias = true };
        canvas.DrawCircle(-22, -20, 14, earPaint);
        canvas.DrawCircle(22, -20, 14, earPaint);
        canvas.DrawCircle(-22, -20, 8, earInner);
        canvas.DrawCircle(22, -20, 8, earInner);

        // Body (round, grey)
        using var bodyPaint = new SKPaint { Color = new SKColor(0x9A, 0x9A, 0xA0), IsAntialias = true };
        canvas.DrawOval(0, 8, 30, 28, bodyPaint);

        // Leather jacket (worn brown)
        using var jacketPaint = new SKPaint { Color = new SKColor(0x5A, 0x35, 0x18), IsAntialias = true };
        using var jacketPath = new SKPath();
        jacketPath.MoveTo(-26, 0);
        jacketPath.LineTo(26, 0);
        jacketPath.LineTo(28, 30);
        jacketPath.LineTo(-28, 30);
        jacketPath.Close();
        canvas.DrawPath(jacketPath, jacketPaint);
        // Jacket collar
        using var collarPaint = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x0A), IsAntialias = true };
        using var collarPath = new SKPath();
        collarPath.MoveTo(-18, 0);
        collarPath.LineTo(0, 8);
        collarPath.LineTo(18, 0);
        collarPath.LineTo(12, -3);
        collarPath.LineTo(0, 4);
        collarPath.LineTo(-12, -3);
        collarPath.Close();
        canvas.DrawPath(collarPath, collarPaint);
        // Worn patches
        using var patchPaint = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x0A, 180), IsAntialias = true };
        canvas.DrawCircle(-12, 18, 4, patchPaint);
        canvas.DrawCircle(8, 22, 3, patchPaint);

        // Head (grey round)
        canvas.DrawCircle(0, -12, 22, bodyPaint);

        // Goggles - cracked aviator
        using var goggleStrap = new SKPaint
        {
            Color = new SKColor(0x2A, 0x18, 0x08),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
        canvas.DrawLine(-22, -16, 22, -16, goggleStrap);

        using var goggleRing = new SKPaint
        {
            Color = new SKColor(0x4A, 0x2A, 0x10),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
        using var goggleLens = new SKPaint { Color = new SKColor(0x10, 0x14, 0x18), IsAntialias = true };
        canvas.DrawCircle(-9, -14, 8, goggleLens);
        canvas.DrawCircle(9, -14, 8, goggleLens);
        canvas.DrawCircle(-9, -14, 8, goggleRing);
        canvas.DrawCircle(9, -14, 8, goggleRing);
        // Lens highlight
        using var lensHL = new SKPaint { Color = new SKColor(0xFF, 0xC8, 0x80, 150), IsAntialias = true };
        canvas.DrawCircle(-11, -16, 2, lensHL);
        canvas.DrawCircle(7, -16, 2, lensHL);
        // Crack in lens
        using var crackPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.8f,
            IsAntialias = true
        };
        canvas.DrawLine(-13, -18, -6, -10, crackPaint);
        canvas.DrawLine(-9, -14, -4, -12, crackPaint);

        // Eyes (small dark dots inside goggles)
        using var eyePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawCircle(-9, -13, 1.8f, eyePaint);
        canvas.DrawCircle(9, -13, 1.8f, eyePaint);

        // Nose (big black koala nose)
        using var nosePaint = new SKPaint { Color = new SKColor(0x10, 0x06, 0x04), IsAntialias = true };
        canvas.DrawOval(0, 0, 8, 6, nosePaint);
        // Nose highlight
        using var noseHL = new SKPaint { Color = new SKColor(0x6A, 0x40, 0x30), IsAntialias = true };
        canvas.DrawOval(-2, -2, 2, 1.5f, noseHL);

        // Mouth
        using var mouthPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        using var mouthPath = new SKPath();
        mouthPath.MoveTo(0, 6);
        mouthPath.LineTo(0, 9);
        mouthPath.MoveTo(0, 9);
        mouthPath.QuadTo(-4, 12, -7, 10);
        mouthPath.MoveTo(0, 9);
        mouthPath.QuadTo(4, 12, 7, 10);
        canvas.DrawPath(mouthPath, mouthPaint);

        // Wings/arms - flap animation
        // 3-frame: spread (phase 6,5,4) -> mid (3,2) -> tucked (1,0 or static)
        float wingSpread;
        if (flapping)
        {
            if (flapPhase >= 4) wingSpread = 1.0f;
            else if (flapPhase >= 2) wingSpread = 0.6f;
            else wingSpread = 0.3f;
        }
        else
        {
            wingSpread = 0.15f;
        }

        using var wingPaint = new SKPaint { Color = new SKColor(0x7A, 0x7A, 0x82), IsAntialias = true };
        using var wingJacket = new SKPaint { Color = new SKColor(0x5A, 0x35, 0x18), IsAntialias = true };

        // Left wing
        canvas.Save();
        canvas.Translate(-22, 8);
        canvas.RotateDegrees(-30 - wingSpread * 60);
        using var lwPath = new SKPath();
        lwPath.MoveTo(0, 0);
        lwPath.LineTo(-5, 2);
        lwPath.LineTo(-22 - wingSpread * 8, 4);
        lwPath.LineTo(-22 - wingSpread * 8, 12);
        lwPath.LineTo(-5, 14);
        lwPath.LineTo(0, 16);
        lwPath.Close();
        canvas.DrawPath(lwPath, wingJacket);
        // Paw at tip
        canvas.DrawCircle(-22 - wingSpread * 8, 8, 5, wingPaint);
        canvas.Restore();

        // Right wing
        canvas.Save();
        canvas.Translate(22, 8);
        canvas.RotateDegrees(30 + wingSpread * 60);
        using var rwPath = new SKPath();
        rwPath.MoveTo(0, 0);
        rwPath.LineTo(5, 2);
        rwPath.LineTo(22 + wingSpread * 8, 4);
        rwPath.LineTo(22 + wingSpread * 8, 12);
        rwPath.LineTo(5, 14);
        rwPath.LineTo(0, 16);
        rwPath.Close();
        canvas.DrawPath(rwPath, wingJacket);
        canvas.DrawCircle(22 + wingSpread * 8, 8, 5, wingPaint);
        canvas.Restore();

        canvas.Restore();
    }

    static void DrawScore(SKCanvas canvas, int score)
    {
        // Drop shadow
        using var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 200),
            TextSize = 56,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Impact", SKFontStyle.Bold) ??
                       SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ??
                       SKTypeface.Default,
            FakeBoldText = true
        };
        using var fg = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 56,
            IsAntialias = true,
            Typeface = shadow.Typeface,
            FakeBoldText = true
        };
        canvas.DrawText($"{score:D2}", 36 + 3, 76 + 3, shadow);
        canvas.DrawText($"{score:D2}", 36, 76, fg);

        // Small label
        using var label = new SKPaint
        {
            Color = new SKColor(0xFF, 0xC8, 0x80, 220),
            TextSize = 16,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default
        };
        canvas.DrawText("SCORE", 38, 30, label);
    }

    static void DrawTitleCard(SKCanvas canvas, int frame)
    {
        // Dark overlay fading
        float fadeIn = Math.Min(1f, frame / 10f);
        float fadeOut = frame > 22 ? Math.Max(0f, 1f - (frame - 22) / 7f) : 1f;
        float alpha = fadeIn * fadeOut;

        using var overlay = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)(180 * alpha))
        };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        // Title
        using var titlePaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xC8, 0x80, (byte)(255 * alpha)),
            TextSize = 110,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Impact", SKFontStyle.Bold) ??
                       SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ??
                       SKTypeface.Default,
            FakeBoldText = true
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)(220 * alpha)),
            TextSize = 110,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = titlePaint.Typeface,
            FakeBoldText = true
        };
        canvas.DrawText("FlappyBrain", Width / 2 + 4, Height / 2 - 16, titleShadow);
        canvas.DrawText("FlappyBrain", Width / 2, Height / 2 - 20, titlePaint);

        // Subtitle
        using var subPaint = new SKPaint
        {
            Color = new SKColor(0xC4, 0x62, 0x2D, (byte)(255 * alpha)),
            TextSize = 32,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default,
            FakeBoldText = true
        };
        canvas.DrawText("Control with your mind", Width / 2, Height / 2 + 40, subPaint);
    }

    static void DrawGameOver(SKCanvas canvas, int score, float alpha)
    {
        using var overlay = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(160 * alpha)) };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        using var titlePaint = new SKPaint
        {
            Color = new SKColor(0xC8, 0x20, 0x10, (byte)(255 * alpha)),
            TextSize = 96,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Impact", SKFontStyle.Bold) ??
                       SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ??
                       SKTypeface.Default,
            FakeBoldText = true
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)(220 * alpha)),
            TextSize = 96,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = titlePaint.Typeface,
            FakeBoldText = true
        };
        canvas.DrawText("GAME OVER", Width / 2 + 4, Height / 2 - 16, titleShadow);
        canvas.DrawText("GAME OVER", Width / 2, Height / 2 - 20, titlePaint);

        using var scorePaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(255 * alpha)),
            TextSize = 44,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default,
            FakeBoldText = true
        };
        canvas.DrawText($"FINAL SCORE: {score:D2}", Width / 2, Height / 2 + 50, scorePaint);
    }
}
