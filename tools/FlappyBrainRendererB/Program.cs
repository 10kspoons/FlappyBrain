using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainRendererB;

internal static class Program
{
    // Canvas
    const int W = 800;
    const int H = 600;
    const int FPS = 30;
    const int TOTAL_SECONDS = 30;
    const int TOTAL_FRAMES = FPS * TOTAL_SECONDS;
    const int TITLE_FRAMES = 60; // 2 seconds title

    // Physics
    const float GRAVITY = 0.45f;
    const float FLAP = -7.5f;
    const float MAX_VY = 11f;
    const float SCROLL_SPEED = 2.6f;

    // World layout
    const float GROUND_Y = 540f;
    const int BIRD_W = 160;
    const int BIRD_H = 130;
    const float BIRD_X = 200f;
    const float DEATH_Y = 480f;

    // Pipes
    const float PIPE_W = 95f;
    const float PIPE_GAP = 260f;
    const float PIPE_SPACING = 320f;

    // Asset paths
    const string ASSET_BIRD = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    const string ASSET_BG   = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";
    const string OUT_DIR    = "/tmp/flappybrain_frames_b";

    // Colors
    static readonly SKColor SKY_TOP    = new(0x1A, 0x0F, 0x08);
    static readonly SKColor SKY_MID    = new(0x6B, 0x30, 0x20);
    static readonly SKColor SKY_BOTTOM = new(0xC4, 0x62, 0x2D);
    static readonly SKColor GROUND_COL = new(0x7A, 0x35, 0x20);
    static readonly SKColor GROUND_HI  = new(0xB5, 0x45, 0x1B);
    static readonly SKColor PIPE_BODY  = new(0x6B, 0x40, 0x20);
    static readonly SKColor PIPE_BAND  = new(0x7A, 0x4A, 0x28);
    static readonly SKColor PIPE_CAP   = new(0x5A, 0x35, 0x18);
    static readonly SKColor SCORE_COL  = new(0xF2, 0xD5, 0xA0);
    static readonly SKColor GOLD       = new(0xF2, 0xC9, 0x6B);
    static readonly SKColor ORANGE     = new(0xE0, 0x7A, 0x32);

    sealed class Pipe
    {
        public float X;
        public float GapY; // center of gap
    }

    sealed class Particle
    {
        public float X, Y, VX, VY;
        public float Size;
        public byte Alpha;
    }

    static int Main()
    {
        Directory.CreateDirectory(OUT_DIR);
        // Clear stale frames
        foreach (var f in Directory.EnumerateFiles(OUT_DIR, "*.png")) File.Delete(f);

        SKBitmap birdImg = SKBitmap.Decode(ASSET_BIRD);
        SKBitmap bgImg = SKBitmap.Decode(ASSET_BG);
        if (birdImg is null || bgImg is null)
        {
            Console.Error.WriteLine("Failed to load assets.");
            return 1;
        }

        var info = new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Game state
        var rng = new Random(20260508);
        float birdY = 300f, birdVY = 0f, birdRot = 0f;
        var pipes = new List<Pipe>();
        float scroll = 0f;
        int score = 0;
        bool alive = true;
        int deadFrames = 0;
        int flapPulseFrames = 0;

        // Particles (dust)
        var particles = new List<Particle>();
        for (int i = 0; i < 80; i++)
        {
            particles.Add(new Particle
            {
                X = rng.Next(0, W),
                Y = rng.Next(0, H),
                VX = -1.2f - (float)rng.NextDouble() * 1.4f,
                VY = ((float)rng.NextDouble() - 0.5f) * 0.4f,
                Size = 3f + (float)rng.NextDouble() * 2f,
                Alpha = (byte)(80 + rng.Next(0, 100))
            });
        }

        // Initial pipes seeded ahead
        float startX = W + 50;
        for (int i = 0; i < 6; i++)
        {
            pipes.Add(new Pipe
            {
                X = startX + i * PIPE_SPACING,
                GapY = 220 + (float)rng.NextDouble() * 180
            });
        }

        for (int frame = 0; frame < TOTAL_FRAMES; frame++)
        {
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            DrawSky(canvas);
            DrawSpeedLines(canvas, frame);
            DrawBackground(canvas, bgImg, scroll);

            if (frame < TITLE_FRAMES)
            {
                // Title card
                DrawGround(canvas, scroll);
                DrawVignette(canvas);
                DrawTitleCard(canvas, frame);
                DrawHUDTag(canvas);
                SaveFrame(surface, frame);
                scroll += SCROLL_SPEED * 0.3f;
                continue;
            }

            // ---- Game tick ----
            if (alive)
            {
                // Auto-pilot: target the upcoming pipe well before reaching it.
                // We pick the pipe whose center X is ahead of bird, so we adjust altitude in advance.
                (Pipe? cur, Pipe? following) = TwoPipes(pipes, BIRD_X);
                float gapCenter = 300f;
                if (cur is not null)
                {
                    float curCenterX = cur.X + PIPE_W / 2f;
                    float distToCur = curCenterX - BIRD_X;
                    if (distToCur > 0)
                    {
                        // Approaching cur — aim straight at its gap
                        gapCenter = cur.GapY;
                    }
                    else if (following is not null)
                    {
                        // Past cur center — start aiming at following pipe
                        gapCenter = following.GapY;
                    }
                    else
                    {
                        gapCenter = cur.GapY;
                    }
                }

                float predictY = birdY;
                float predictVY = birdVY;
                for (int t = 0; t < 10; t++)
                {
                    predictVY += GRAVITY;
                    if (predictVY > MAX_VY) predictVY = MAX_VY;
                    predictY += predictVY;
                }

                // Bias target downward so bird oscillates around gap center
                // (flap impulse + horizon both push average altitude up by ~60px)
                float flapTarget = gapCenter + 60f;
                if (predictY > flapTarget && birdVY > -3f)
                {
                    birdVY = FLAP;
                    flapPulseFrames = 6;
                }

                birdVY += GRAVITY;
                if (birdVY > MAX_VY) birdVY = MAX_VY;
                birdY += birdVY;

                // Rotation based on velocity
                if (flapPulseFrames > 0)
                {
                    birdRot = -12f; // nose up
                    flapPulseFrames--;
                }
                else if (birdVY < 0)
                {
                    birdRot = -8f;
                }
                else if (birdVY < 4)
                {
                    birdRot = 0f;
                }
                else
                {
                    birdRot = MathF.Min(18f, birdVY * 2f);
                }

                // Move pipes
                foreach (var p in pipes) p.X -= SCROLL_SPEED;
                // Recycle pipes
                if (pipes.Count > 0 && pipes[0].X < -PIPE_W - 20)
                {
                    pipes.RemoveAt(0);
                    score++;
                }
                while (pipes.Count < 6)
                {
                    float lastX = pipes.Count > 0 ? pipes[^1].X : W;
                    pipes.Add(new Pipe
                    {
                        X = lastX + PIPE_SPACING,
                        GapY = 220 + (float)rng.NextDouble() * 180
                    });
                }

                // Death checks
                if (birdY > DEATH_Y) alive = false;
                // Pipe collision (simple bbox vs gap)
                float bx0 = BIRD_X - BIRD_W * 0.22f;
                float bx1 = BIRD_X + BIRD_W * 0.22f;
                float by0 = birdY - BIRD_H * 0.20f;
                float by1 = birdY + BIRD_H * 0.20f;
                foreach (var p in pipes)
                {
                    float px0 = p.X;
                    float px1 = p.X + PIPE_W;
                    if (bx1 < px0 || bx0 > px1) continue;
                    float gapTop = p.GapY - PIPE_GAP / 2f;
                    float gapBot = p.GapY + PIPE_GAP / 2f;
                    if (by0 < gapTop || by1 > gapBot)
                    {
                        alive = false;
                        break;
                    }
                }

                scroll += SCROLL_SPEED;
            }
            else
            {
                // Death animation: keep falling, then restart
                birdVY += GRAVITY;
                if (birdVY > MAX_VY) birdVY = MAX_VY;
                birdY += birdVY;
                birdRot = MathF.Min(45f, birdRot + 2f);
                deadFrames++;
                if (deadFrames > 50)
                {
                    // Reset
                    alive = true;
                    deadFrames = 0;
                    birdY = 300f;
                    birdVY = 0f;
                    birdRot = 0f;
                    score = 0;
                    pipes.Clear();
                    for (int i = 0; i < 6; i++)
                    {
                        pipes.Add(new Pipe
                        {
                            X = W + 50 + i * PIPE_SPACING,
                            GapY = 220 + (float)rng.NextDouble() * 180
                        });
                    }
                }
                scroll += SCROLL_SPEED * 0.4f;
            }

            // Particles
            foreach (var pa in particles)
            {
                pa.X += pa.VX;
                pa.Y += pa.VY;
                pa.VY += ((float)rng.NextDouble() - 0.5f) * 0.05f;
                if (pa.X < -10)
                {
                    pa.X = W + 10;
                    pa.Y = rng.Next(0, H);
                }
                if (pa.Y < 0 || pa.Y > H) pa.Y = rng.Next(0, H);
            }

            // ---- Draw scene ----
            DrawPipes(canvas, pipes);
            DrawGround(canvas, scroll);
            DrawParticles(canvas, particles);
            DrawBird(canvas, birdImg, BIRD_X, birdY, birdRot);
            DrawVignette(canvas);
            DrawScore(canvas, score);
            DrawHUDTag(canvas);

            SaveFrame(surface, frame);
        }

        Console.WriteLine($"Wrote {TOTAL_FRAMES} frames to {OUT_DIR}");
        return 0;
    }

    static Pipe? NextPipe(List<Pipe> pipes, float birdX)
    {
        Pipe? best = null;
        float bestX = float.MaxValue;
        foreach (var p in pipes)
        {
            float rightEdge = p.X + PIPE_W;
            if (rightEdge > birdX - 10 && p.X < bestX)
            {
                bestX = p.X;
                best = p;
            }
        }
        return best;
    }

    static (Pipe? cur, Pipe? following) TwoPipes(List<Pipe> pipes, float birdX)
    {
        // Sort-by-X candidates ahead of bird's collision exit
        Pipe? cur = null; float curX = float.MaxValue;
        Pipe? following = null; float followingX = float.MaxValue;
        foreach (var p in pipes)
        {
            float rightEdge = p.X + PIPE_W;
            if (rightEdge < birdX - 30) continue;
            if (p.X < curX) { followingX = curX; following = cur; curX = p.X; cur = p; }
            else if (p.X < followingX) { followingX = p.X; following = p; }
        }
        return (cur, following);
    }

    static void SaveFrame(SKSurface surface, int frame)
    {
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        string path = Path.Combine(OUT_DIR, $"frame_{frame:D5}.png");
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
    }

    static void DrawSky(SKCanvas canvas)
    {
        using var paint = new SKPaint();
        var colors = new[] { SKY_TOP, SKY_MID, SKY_BOTTOM };
        var positions = new[] { 0f, 0.55f, 1f };
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, H),
            colors, positions, SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, W, H, paint);
    }

    static void DrawSpeedLines(SKCanvas canvas, int frame)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xFF, 0xC4, 0x80, 30)
        };
        var rng = new Random(99);
        float offset = (frame * 6f) % 200f;
        for (int i = 0; i < 12; i++)
        {
            float baseY = 60 + i * 38 + (float)rng.NextDouble() * 10;
            float angle = ((float)rng.NextDouble() - 0.5f) * 4f;
            float x0 = -50 + (i % 3) * 120 - offset;
            float length = 80 + (float)rng.NextDouble() * 180;
            canvas.DrawLine(x0, baseY, x0 + length, baseY + angle, paint);
        }
    }

    static void DrawBackground(SKCanvas canvas, SKBitmap bgImg, float scroll)
    {
        // Background tile area: y=280..540 (above ground)
        const int destTop = 280;
        const int destBottom = 540;
        int destH = destBottom - destTop;

        // Source crop: keep only lower ~55% of bg image (terrain), skip white sky
        int srcTop = (int)(bgImg.Height * 0.45f);
        int srcH = bgImg.Height - srcTop;
        int srcW = bgImg.Width;
        var src = new SKRect(0, srcTop, srcW, bgImg.Height);

        int destW = (int)(srcW * (destH / (float)srcH));
        float scrollX = (scroll * 0.45f) % destW;

        using var paint = new SKPaint
        {
            IsAntialias = false,
            FilterQuality = SKFilterQuality.Medium,
            ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
            {
                0.70f, 0.05f, 0f,    0f, 0f,
                0.05f, 0.55f, 0f,    0f, 0f,
                0f,    0.05f, 0.45f, 0f, 0f,
                0f,    0f,    0f,    0.85f, 0f
            })
        };

        for (float x = -scrollX - destW; x < W + destW; x += destW)
        {
            var dst = new SKRect(x, destTop, x + destW, destBottom);
            canvas.DrawBitmap(bgImg, src, dst, paint);
        }
    }

    static void DrawGround(SKCanvas canvas, float scroll)
    {
        using (var p = new SKPaint { Color = GROUND_COL, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(0, 540, W, 60, p);
        }
        using (var p = new SKPaint { Color = GROUND_HI, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = false })
        {
            var rng = new Random(7);
            float offset = scroll % 80f;
            for (int i = 0; i < 30; i++)
            {
                float y = 545 + rng.Next(0, 50);
                float x0 = -offset + i * 30 + rng.Next(-10, 10);
                float len = 12 + rng.Next(0, 30);
                canvas.DrawLine(x0, y, x0 + len, y, p);
            }
        }
        // Top edge highlight
        using (var p = new SKPaint { Color = new SKColor(0x9A, 0x4A, 0x25), Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(0, 540, W, 2, p);
        }
    }

    static void DrawPipes(SKCanvas canvas, List<Pipe> pipes)
    {
        foreach (var p in pipes)
        {
            float gapTop = p.GapY - PIPE_GAP / 2f;
            float gapBot = p.GapY + PIPE_GAP / 2f;
            DrawPipeShaft(canvas, p.X, 0, p.X + PIPE_W, gapTop, capBottom: true);
            DrawPipeShaft(canvas, p.X, gapBot, p.X + PIPE_W, GROUND_Y, capBottom: false);
        }
    }

    static void DrawPipeShaft(SKCanvas canvas, float x0, float y0, float x1, float y1, bool capBottom)
    {
        // Body
        using (var bg = new SKPaint { Color = PIPE_BODY, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(x0, y0, x1 - x0, y1 - y0, bg);
        }
        // Corrugation bands every 12px
        using (var band = new SKPaint { Color = PIPE_BAND, Style = SKPaintStyle.Fill })
        {
            for (float yy = y0 + 6; yy < y1 - 4; yy += 12)
            {
                canvas.DrawRect(x0 + 2, yy, x1 - x0 - 4, 3, band);
            }
        }
        // Rust streak (vertical gradient, semi-transparent)
        using (var streak = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false })
        {
            streak.Shader = SKShader.CreateLinearGradient(
                new SKPoint(x0, y0), new SKPoint(x0, y1),
                new[] {
                    new SKColor(0xC4, 0x62, 0x2D, 0),
                    new SKColor(0xC4, 0x62, 0x2D, 60),
                    new SKColor(0x6B, 0x30, 0x10, 80),
                    new SKColor(0xC4, 0x62, 0x2D, 30)
                },
                new[] { 0f, 0.3f, 0.65f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(x1 - 12, y0, 8, y1 - y0, streak);
        }
        // Cap
        float capX = x0 - (110 - PIPE_W) / 2f;
        float capH = 30f;
        float capY = capBottom ? y1 - capH : y0;
        using (var cap = new SKPaint { Color = PIPE_CAP, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(capX, capY, 110, capH, cap);
        }
        using (var rim = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x10), Style = SKPaintStyle.Stroke, StrokeWidth = 4 })
        {
            canvas.DrawRect(capX, capY, 110, capH, rim);
        }
        // Rivets on cap
        using (var riv = new SKPaint { Color = new SKColor(0x3A, 0x20, 0x10), IsAntialias = true, Style = SKPaintStyle.Fill })
        {
            for (int i = 0; i < 5; i++)
            {
                float rx = capX + 12 + i * 22;
                canvas.DrawCircle(rx, capY + 6, 2.2f, riv);
                canvas.DrawCircle(rx, capY + capH - 6, 2.2f, riv);
            }
        }
    }

    static void DrawParticles(SKCanvas canvas, List<Particle> ps)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var pa in ps)
        {
            paint.Color = new SKColor(0xE8, 0xB0, 0x70, pa.Alpha);
            canvas.DrawCircle(pa.X, pa.Y, pa.Size, paint);
        }
    }

    static void DrawBird(SKCanvas canvas, SKBitmap bird, float cx, float cy, float rotDeg)
    {
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees(rotDeg);
        var dst = new SKRect(-BIRD_W / 2f, -BIRD_H / 2f, BIRD_W / 2f, BIRD_H / 2f);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        // Subtle drop shadow
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 90), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f) })
        {
            var sd = new SKRect(dst.Left + 6, dst.Top + 8, dst.Right + 6, dst.Bottom + 8);
            canvas.DrawBitmap(bird, sd, shadow);
        }
        canvas.DrawBitmap(bird, dst, paint);
        canvas.Restore();
    }

    static void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Fill };
        paint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(W / 2f, H / 2f),
            MathF.Max(W, H) * 0.65f,
            new[] {
                new SKColor(0, 0, 0, 0),
                new SKColor(0, 0, 0, 30),
                new SKColor(0, 0, 0, 140)
            },
            new[] { 0f, 0.6f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, W, H, paint);
    }

    // Score rendered as pixel-art dot-matrix digits (top-center)
    static void DrawScore(SKCanvas canvas, int score)
    {
        string s = score.ToString();
        int scale = 6;
        int digitW = 3 * scale + scale; // 3 cols + spacing
        int totalW = s.Length * digitW;
        int x = W / 2 - totalW / 2;
        int y = 30;
        // Drop shadow first
        DrawDigits(canvas, s, x + 2, y + 3, scale, new SKColor(0, 0, 0, 160));
        DrawDigits(canvas, s, x, y, scale, SCORE_COL);
    }

    // 3x5 pixel digit font
    static readonly Dictionary<char, string[]> DIGITS = new()
    {
        ['0'] = new[] { "111","101","101","101","111" },
        ['1'] = new[] { "010","110","010","010","111" },
        ['2'] = new[] { "111","001","111","100","111" },
        ['3'] = new[] { "111","001","111","001","111" },
        ['4'] = new[] { "101","101","111","001","001" },
        ['5'] = new[] { "111","100","111","001","111" },
        ['6'] = new[] { "111","100","111","101","111" },
        ['7'] = new[] { "111","001","010","100","100" },
        ['8'] = new[] { "111","101","111","101","111" },
        ['9'] = new[] { "111","101","111","001","111" },
        ['F'] = new[] { "111","100","111","100","100" },
        ['L'] = new[] { "100","100","100","100","111" },
        ['A'] = new[] { "111","101","111","101","101" },
        ['P'] = new[] { "111","101","111","100","100" },
        ['Y'] = new[] { "101","101","010","010","010" },
        ['B'] = new[] { "110","101","110","101","110" },
        ['R'] = new[] { "110","101","110","110","101" },
        ['I'] = new[] { "111","010","010","010","111" },
        ['N'] = new[] { "101","111","111","111","101" },
        ['C'] = new[] { "111","100","100","100","111" },
        ['O'] = new[] { "111","101","101","101","111" },
        ['T'] = new[] { "111","010","010","010","010" },
        ['W'] = new[] { "101","101","101","111","101" },
        ['H'] = new[] { "101","101","111","101","101" },
        ['M'] = new[] { "101","111","111","101","101" },
        ['D'] = new[] { "110","101","101","101","110" },
        ['U'] = new[] { "101","101","101","101","111" },
        ['E'] = new[] { "111","100","111","100","111" },
        ['K'] = new[] { "101","110","100","110","101" },
        ['V'] = new[] { "101","101","101","101","010" },
        [' '] = new[] { "000","000","000","000","000" },
    };

    static void DrawDigits(SKCanvas canvas, string s, int x, int y, int scale, SKColor color)
    {
        using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = false };
        int cx = x;
        foreach (char ch in s)
        {
            char up = char.ToUpper(ch);
            if (!DIGITS.TryGetValue(up, out var pat))
            {
                cx += 3 * scale + scale;
                continue;
            }
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (pat[row][col] == '1')
                    {
                        canvas.DrawRect(cx + col * scale, y + row * scale, scale, scale, paint);
                    }
                }
            }
            cx += 3 * scale + scale;
        }
    }

    static int MeasureText(string s, int scale) => s.Length * (3 * scale + scale) - scale;

    static void DrawTitleCard(SKCanvas canvas, int frame)
    {
        // Fade-in factor
        float t = MathF.Min(1f, frame / 18f);
        byte alpha = (byte)(255 * t);

        // FLAPPYBRAIN big
        const string title = "FLAPPYBRAIN";
        int scale = 10;
        int tw = MeasureText(title, scale);
        int tx = W / 2 - tw / 2;
        int ty = 200;
        DrawDigits(canvas, title, tx + 4, ty + 5, scale, new SKColor(0, 0, 0, (byte)(alpha * 0.7f)));
        DrawDigits(canvas, title, tx, ty, scale, GOLD.WithAlpha(alpha));

        // Subtitle
        const string sub = "CONTROL WITH YOUR MIND";
        int s2 = 5;
        int sw = MeasureText(sub, s2);
        int sx = W / 2 - sw / 2;
        int sy = ty + 5 * scale + 30;
        DrawDigits(canvas, sub, sx + 2, sy + 2, s2, new SKColor(0, 0, 0, (byte)(alpha * 0.6f)));
        DrawDigits(canvas, sub, sx, sy, s2, ORANGE.WithAlpha(alpha));

        // tag
        const string tag = "KEYBOARD MODE";
        int s3 = 3;
        int kw = MeasureText(tag, s3);
        int kx = W / 2 - kw / 2;
        int ky = sy + 5 * s2 + 30;
        DrawDigits(canvas, tag, kx, ky, s3, new SKColor(0xC0, 0xA0, 0x88, alpha));
    }

    static void DrawHUDTag(SKCanvas canvas)
    {
        // small top-right
        const string tag = "KEYBOARD MODE";
        int s = 3;
        int w = MeasureText(tag, s);
        int x = W - w - 16;
        int y = 16;
        // background pill
        using (var bg = new SKPaint { Color = new SKColor(0, 0, 0, 110), IsAntialias = true })
        {
            canvas.DrawRoundRect(x - 8, y - 6, w + 16, 5 * s + 12, 6, 6, bg);
        }
        DrawDigits(canvas, tag, x, y, s, new SKColor(0xCF, 0xB0, 0x90, 220));
    }
}
