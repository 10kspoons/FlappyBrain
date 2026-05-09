using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainRendererB3;

internal static class Program
{
    private const int Width = 800;
    private const int Height = 600;
    private const int TotalFrames = 900;
    private const string FrameDir = "/tmp/fb-b3-frames";
    private const string KoalaPath = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    private const string BgPath = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";

    // Sky palette
    private static readonly SKColor SkyTop = new(0x1A, 0x0F, 0x08);
    private static readonly SKColor SkyMid = new(0x6B, 0x30, 0x20);
    private static readonly SKColor SkyBottom = new(0xC4, 0x62, 0x2D);
    private static readonly SKColor GroundTop = new(0x7A, 0x35, 0x20);
    private static readonly SKColor GroundBottom = new(0xB5, 0x45, 0x1B);

    // Pipe palette
    private static readonly SKColor PipeBody = new(0x6B, 0x40, 0x20);
    private static readonly SKColor PipeBand = new(0x7A, 0x4A, 0x28);
    private static readonly SKColor PipeCap = new(0x5A, 0x35, 0x18);

    private static readonly SKColor TextCream = new(0xF2, 0xD5, 0xA0);

    private static readonly int[] FlapFrames = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };

    private sealed class Pipe
    {
        public float X;
        public float GapY;
        public float GapHeight;
        public bool Scored;
        public int Index;
    }

    private sealed class Billboard
    {
        public float X;
        public string Text = "";
        public float TiltDeg;
        public float TextOffsetX;
        public float TextOffsetY;
        public float[] CrackSeed = Array.Empty<float>();
        public float[] GrimeSeed = Array.Empty<float>();
    }

    private sealed class Particle
    {
        public float X, Y, Speed, Size;
        public SKColor Color;
    }

    private sealed class SpeedLine
    {
        public float Y, Length, Width, Opacity;
    }

    private static int Main()
    {
        Directory.CreateDirectory(FrameDir);

        SKBitmap? koala = SKBitmap.Decode(KoalaPath);
        SKBitmap? bg = SKBitmap.Decode(BgPath);
        if (koala == null) { Console.Error.WriteLine($"Failed to load koala: {KoalaPath}"); return 1; }
        if (bg == null) { Console.Error.WriteLine($"Failed to load bg: {BgPath}"); return 1; }

        var rng = new Random(20260509);

        // Particles
        var particles = new List<Particle>();
        for (int i = 0; i < 80; i++)
        {
            particles.Add(new Particle
            {
                X = (float)rng.NextDouble() * Width,
                Y = (float)rng.NextDouble() * Height,
                Speed = 0.6f + (float)rng.NextDouble() * 2.4f,
                Size = 2f + (float)rng.NextDouble() * 3f,
                Color = new SKColor(
                    (byte)(180 + rng.Next(60)),
                    (byte)(110 + rng.Next(50)),
                    (byte)(60 + rng.Next(40)),
                    (byte)(120 + rng.Next(80)))
            });
        }

        // Speed lines pool
        var speedLines = new List<SpeedLine>();
        for (int i = 0; i < 16; i++)
        {
            speedLines.Add(new SpeedLine
            {
                Y = (float)rng.NextDouble() * Height,
                Length = 60f + (float)rng.NextDouble() * 120f,
                Width = 1f + (float)rng.NextDouble(),
                Opacity = 0.25f + (float)rng.NextDouble() * 0.25f
            });
        }

        // Bird state
        float birdX = 220f;
        float birdY = Height / 2f;
        float birdVy = 0f;
        const float Gravity = 0.45f;
        const float FlapImpulse = -8.5f;
        float birdRotation = 0f;
        int flapGlowFramesLeft = 0;

        // Pipes
        var pipes = new List<Pipe>();
        const float PipeSpacing = 260f;
        const float PipeSpeed = 3.2f;
        int pipeIndexCounter = 0;
        // Seed pipes off-screen to the right
        for (int i = 0; i < 6; i++)
        {
            pipes.Add(new Pipe
            {
                X = 900f + i * PipeSpacing,
                GapY = 200f + (float)rng.NextDouble() * 200f,
                GapHeight = 200f,
                Index = pipeIndexCounter++
            });
        }

        // Billboards: spawn every 3rd pipe gap, mid-ground (parallax 0.5x)
        var billboards = new List<Billboard>();
        int score = 0;

        // Title font
        var titlePaint = new SKPaint { IsAntialias = true, Color = TextCream, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 56 };
        var subPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0xE0, 0xC0, 0x80), Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic), TextSize = 22 };
        var hudPaint = new SKPaint { IsAntialias = true, Color = TextCream, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 28 };
        var hudShadow = new SKPaint { IsAntialias = true, Color = SKColors.Black, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 28 };
        var gameOverPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0xCC, 0x20, 0x10), Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 64 };

        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        float bgScrollX = 0f;
        float midScrollX = 0f;

        for (int frame = 0; frame < TotalFrames; frame++)
        {
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SkyTop);

            // 1. Background image scrolling at 0.3x
            bgScrollX -= PipeSpeed * 0.3f;
            float bgW = Width;
            float scaledBgW = bgW;
            float modX = bgScrollX % scaledBgW;
            if (modX > 0) modX -= scaledBgW;
            // Draw bg twice for seamless wrap
            using (var bgPaint = new SKPaint { FilterQuality = SKFilterQuality.Medium })
            {
                var dst1 = new SKRect(modX, 0, modX + scaledBgW, Height);
                var dst2 = new SKRect(modX + scaledBgW, 0, modX + 2 * scaledBgW, Height);
                canvas.DrawBitmap(bg, dst1, bgPaint);
                canvas.DrawBitmap(bg, dst2, bgPaint);
            }

            // 2. Sky gradient overlay at ~60% opacity
            using (var skyPaint = new SKPaint())
            {
                skyPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(0, Height),
                    new[] { SkyTop, SkyMid, SkyBottom },
                    new[] { 0f, 0.55f, 1f },
                    SKShaderTileMode.Clamp);
                skyPaint.Color = skyPaint.Color.WithAlpha(153); // ~60%
                // Use blend by applying alpha via separate paint
                using var alphaPaint = new SKPaint
                {
                    Shader = skyPaint.Shader,
                    Color = SKColors.White.WithAlpha(153)
                };
                canvas.DrawRect(new SKRect(0, 0, Width, Height), alphaPaint);
            }

            // 3. Mid-ground dust haze strips (procedural)
            for (int i = 0; i < 4; i++)
            {
                float y = 120f + i * 80f + (float)Math.Sin((frame + i * 30) * 0.02) * 8f;
                using var hazePaint = new SKPaint
                {
                    Color = new SKColor(0xC4, 0x62, 0x2D, (byte)(40 + i * 8)),
                };
                canvas.DrawRect(new SKRect(0, y, Width, y + 30), hazePaint);
            }

            // 4. Update billboards & spawn (mid parallax)
            midScrollX -= PipeSpeed * 0.5f;
            // Spawn billboards aligned with every 3rd pipe
            // We'll spawn whenever the rightmost billboard is < some threshold
            float rightmostBillboardX = -10000f;
            foreach (var b in billboards) if (b.X > rightmostBillboardX) rightmostBillboardX = b.X;
            while (rightmostBillboardX < Width + 600f)
            {
                float newX = rightmostBillboardX < -9000f ? Width + 200f : rightmostBillboardX + PipeSpacing * 3f * 0.5f + 200f;
                // After first spawn, ensure minimum spacing
                if (rightmostBillboardX < -9000f)
                {
                    newX = Width + 100f;
                }
                else
                {
                    newX = rightmostBillboardX + 520f;
                }
                var bb = new Billboard
                {
                    X = newX,
                    Text = (billboards.Count % 2 == 0) ? "PATIENT ZERO" : "10,000 SPOONS",
                    TiltDeg = (float)(rng.NextDouble() * 8 - 4),
                    TextOffsetX = (float)(rng.NextDouble() * 6 - 3),
                    TextOffsetY = (float)(rng.NextDouble() * 6 - 3),
                };
                bb.CrackSeed = new float[12];
                for (int c = 0; c < 12; c++) bb.CrackSeed[c] = (float)rng.NextDouble();
                bb.GrimeSeed = new float[6];
                for (int g = 0; g < 6; g++) bb.GrimeSeed[g] = (float)rng.NextDouble();
                billboards.Add(bb);
                rightmostBillboardX = newX;
            }
            foreach (var b in billboards) b.X -= PipeSpeed * 0.5f;
            billboards.RemoveAll(b => b.X < -400f);

            // Phase: title 0-59, gameplay 60-839, gameover 840-899
            bool inTitle = frame < 60;
            bool inGameOver = frame >= 840;
            bool gameplay = !inTitle && !inGameOver;

            // Update pipes
            if (!inTitle)
            {
                foreach (var p in pipes) p.X -= PipeSpeed;
                pipes.RemoveAll(p => p.X < -150);
                while (pipes.Count < 6)
                {
                    float lastX = 0;
                    foreach (var p in pipes) if (p.X > lastX) lastX = p.X;
                    pipes.Add(new Pipe
                    {
                        X = lastX + PipeSpacing,
                        GapY = 180f + (float)rng.NextDouble() * 220f,
                        GapHeight = 200f,
                        Index = pipeIndexCounter++
                    });
                }
            }

            // 5. Draw billboards (BEFORE pipes — mid-ground)
            DrawBillboards(canvas, billboards);

            // 6. Update bird physics + flaps
            if (gameplay || inGameOver)
            {
                if (Array.IndexOf(FlapFrames, frame) >= 0)
                {
                    birdVy = FlapImpulse;
                    flapGlowFramesLeft = 8;
                }
                birdVy += Gravity;
                birdY += birdVy;
                if (birdY < 60) { birdY = 60; birdVy = 0; }
                if (birdY > Height - 100) { birdY = Height - 100; birdVy = 0; }
                birdRotation = Math.Clamp(birdVy * 0.04f, -0.4f, 0.6f);

                // Score
                foreach (var p in pipes)
                {
                    if (!p.Scored && p.X + 47 < birdX)
                    {
                        p.Scored = true;
                        if (gameplay) score++;
                    }
                }
            }

            // 7. Draw pipes
            DrawPipes(canvas, pipes);

            // 8. Draw ground band
            using (var groundPaint = new SKPaint())
            {
                groundPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, Height - 80),
                    new SKPoint(0, Height),
                    new[] { GroundTop, GroundBottom },
                    null,
                    SKShaderTileMode.Clamp);
                canvas.DrawRect(new SKRect(0, Height - 80, Width, Height), groundPaint);
            }

            // 9. Speed lines
            foreach (var sl in speedLines)
            {
                using var slPaint = new SKPaint
                {
                    Color = new SKColor(0xC4, 0x62, 0x2D, (byte)(sl.Opacity * 255)),
                    StrokeWidth = sl.Width,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawLine(0, sl.Y, sl.Length, sl.Y, slPaint);
                sl.Y += 0.5f;
                if (sl.Y > Height) sl.Y = 0;
            }
            // Periodically reshuffle
            if (frame % 4 == 0)
            {
                foreach (var sl in speedLines)
                {
                    if (rng.NextDouble() < 0.2)
                    {
                        sl.Y = (float)rng.NextDouble() * Height;
                        sl.Length = 60f + (float)rng.NextDouble() * 120f;
                        sl.Opacity = 0.25f + (float)rng.NextDouble() * 0.25f;
                    }
                }
            }

            // 10. Dust particles
            foreach (var pt in particles)
            {
                pt.X -= pt.Speed;
                if (pt.X < -10)
                {
                    pt.X = Width + 10;
                    pt.Y = (float)rng.NextDouble() * Height;
                }
                using var pPaint = new SKPaint { Color = pt.Color };
                canvas.DrawCircle(pt.X, pt.Y, pt.Size, pPaint);
            }

            // 11. Bird (koala) — only after title
            if (!inTitle)
            {
                DrawKoala(canvas, koala, birdX, birdY, birdRotation, flapGlowFramesLeft);
                if (flapGlowFramesLeft > 0) flapGlowFramesLeft--;
            }

            // 12. HUD score
            if (gameplay || inGameOver)
            {
                string hud = $"SCORE: {score}";
                canvas.DrawText(hud, 22, 42, hudShadow);
                canvas.DrawText(hud, 20, 40, hudPaint);
            }

            // 13. Vignette
            DrawVignette(canvas);

            // 14. Title overlay
            if (inTitle)
            {
                using var dim = new SKPaint { Color = new SKColor(0, 0, 0, 140) };
                canvas.DrawRect(new SKRect(0, 0, Width, Height), dim);
                float t = frame / 60f;
                byte alpha = (byte)Math.Clamp(t * 255 * 1.4f, 0, 255);
                titlePaint.Color = TextCream.WithAlpha(alpha);
                subPaint.Color = new SKColor(0xE0, 0xC0, 0x80, alpha);
                var bounds = new SKRect();
                titlePaint.MeasureText("FlappyBrain", ref bounds);
                float titleX = (Width - bounds.Width) / 2f;
                canvas.DrawText("FlappyBrain", titleX, 280, titlePaint);
                subPaint.MeasureText("Control with your mind", ref bounds);
                canvas.DrawText("Control with your mind", (Width - bounds.Width) / 2f, 320, subPaint);
                // Logo flash at frame 30
                if (frame >= 28 && frame <= 36)
                {
                    using var flash = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(120 - Math.Abs(32 - frame) * 15)) };
                    canvas.DrawRect(new SKRect(0, 0, Width, Height), flash);
                }
            }

            // 15. Game over overlay
            if (inGameOver)
            {
                float t = (frame - 840) / 60f;
                byte alpha = (byte)Math.Clamp(t * 220, 0, 220);
                using var dim = new SKPaint { Color = new SKColor(0, 0, 0, alpha) };
                canvas.DrawRect(new SKRect(0, 0, Width, Height), dim);
                gameOverPaint.Color = new SKColor(0xCC, 0x20, 0x10, alpha);
                var bounds = new SKRect();
                gameOverPaint.MeasureText("GAME OVER", ref bounds);
                canvas.DrawText("GAME OVER", (Width - bounds.Width) / 2f, 270, gameOverPaint);
                hudPaint.Color = TextCream.WithAlpha(alpha);
                string finalScore = $"FINAL SCORE: {score}";
                hudPaint.MeasureText(finalScore, ref bounds);
                canvas.DrawText(finalScore, (Width - bounds.Width) / 2f, 320, hudPaint);
                hudPaint.Color = TextCream;
            }

            // Save frame
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            string outPath = Path.Combine(FrameDir, $"frame_{frame:0000}.png");
            using var fs = File.OpenWrite(outPath);
            data.SaveTo(fs);

            if (frame % 60 == 0) Console.WriteLine($"Frame {frame}/{TotalFrames}");
        }

        Console.WriteLine("All frames written.");
        return 0;
    }

    private static void DrawPipes(SKCanvas canvas, List<Pipe> pipes)
    {
        const float pipeWidth = 95f;
        foreach (var p in pipes)
        {
            float topBottom = p.GapY - p.GapHeight / 2f;
            float bottomTop = p.GapY + p.GapHeight / 2f;
            // Top pipe
            DrawPipeColumn(canvas, p.X, 0, p.X + pipeWidth, topBottom, isTop: true);
            // Bottom pipe
            DrawPipeColumn(canvas, p.X, bottomTop, p.X + pipeWidth, Height - 80, isTop: false);
        }
    }

    private static void DrawPipeColumn(SKCanvas canvas, float x, float y, float x2, float y2, bool isTop)
    {
        if (y2 <= y) return;
        var rect = new SKRect(x, y, x2, y2);
        using (var body = new SKPaint { Color = PipeBody }) canvas.DrawRect(rect, body);

        // Horizontal rust bands every 30px
        using (var band = new SKPaint { Color = PipeBand })
        {
            for (float by = y + 15; by < y2; by += 30)
            {
                canvas.DrawRect(new SKRect(x, by, x2, by + 4), band);
            }
        }

        // Corrugated vertical stripes every 12px
        using (var stripe = new SKPaint { Color = PipeBand.WithAlpha(160) })
        {
            for (float sx = x + 4; sx < x2; sx += 12)
            {
                canvas.DrawRect(new SKRect(sx, y, sx + 2, y2), stripe);
            }
        }

        // Cap (extra wide) at appropriate end
        using (var cap = new SKPaint { Color = PipeCap })
        {
            float capX1 = x - 6;
            float capX2 = x2 + 6;
            if (isTop)
            {
                canvas.DrawRect(new SKRect(capX1, y2 - 20, capX2, y2), cap);
            }
            else
            {
                canvas.DrawRect(new SKRect(capX1, y, capX2, y + 20), cap);
            }
        }
    }

    private static void DrawBillboards(SKCanvas canvas, List<Billboard> billboards)
    {
        foreach (var b in billboards)
        {
            if (b.X < -300 || b.X > Width + 300) continue;
            float panelW = 260f;
            float panelH = 90f;
            float panelY = 200f; // mid-ground
            float panelCx = b.X;
            float panelCy = panelY + panelH / 2f;

            // Two rusted poles (drawn UNDER the panel, going past bottom of canvas)
            using (var pole = new SKPaint { Color = PipeCap })
            {
                float pole1X = panelCx - panelW / 2f + 30;
                float pole2X = panelCx + panelW / 2f - 30;
                canvas.DrawRect(new SKRect(pole1X - 4, panelCy + panelH / 2f - 5, pole1X + 4, Height), pole);
                canvas.DrawRect(new SKRect(pole2X - 4, panelCy + panelH / 2f - 5, pole2X + 4, Height), pole);
            }

            canvas.Save();
            canvas.RotateDegrees(b.TiltDeg, panelCx, panelCy);

            // Panel background
            var panelRect = new SKRect(panelCx - panelW / 2f, panelCy - panelH / 2f, panelCx + panelW / 2f, panelCy + panelH / 2f);
            using (var bgPaint = new SKPaint { Color = new SKColor(0x1A, 0x1A, 0x1A, 217) })
                canvas.DrawRect(panelRect, bgPaint);
            using (var border = new SKPaint { Color = new SKColor(0x8B, 0x45, 0x13), Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
                canvas.DrawRect(panelRect, border);

            // Grime strips
            using (var grime = new SKPaint { Color = new SKColor(0, 0, 0, 50) })
            {
                for (int g = 0; g < 3; g++)
                {
                    float gy = panelRect.Top + 10 + b.GrimeSeed[g] * (panelH - 20);
                    float gh = 4 + b.GrimeSeed[g + 3] * 8;
                    canvas.DrawRect(new SKRect(panelRect.Left + 4, gy, panelRect.Right - 4, gy + gh), grime);
                }
            }

            // Text
            bool isPatient = b.Text == "PATIENT ZERO";
            float textSize = isPatient ? 36f : 32f;
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = isPatient ? new SKColor(0xCC, 0x20, 0x10) : new SKColor(0xE8, 0xC4, 0x40),
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                TextSize = textSize
            };
            using var shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, 102),
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                TextSize = textSize
            };
            var tb = new SKRect();
            textPaint.MeasureText(b.Text, ref tb);
            float tx = panelCx - tb.Width / 2f + b.TextOffsetX;
            float ty = panelCy + tb.Height / 2f - 4 + b.TextOffsetY;
            canvas.DrawText(b.Text, tx + 2, ty + 2, shadowPaint);
            canvas.DrawText(b.Text, tx, ty, textPaint);

            // Crack overlay
            using (var crack = new SKPaint { Color = new SKColor(255, 255, 255, 64), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            {
                for (int c = 0; c < 3; c++)
                {
                    float x1 = panelRect.Left + b.CrackSeed[c * 4] * panelW;
                    float y1 = panelRect.Top + b.CrackSeed[c * 4 + 1] * panelH;
                    float x2 = panelRect.Left + b.CrackSeed[c * 4 + 2] * panelW;
                    float y2 = panelRect.Top + b.CrackSeed[c * 4 + 3] * panelH;
                    canvas.DrawLine(x1, y1, x2, y2, crack);
                }
            }

            canvas.Restore();
        }
    }

    private static void DrawKoala(SKCanvas canvas, SKBitmap koala, float cx, float cy, float rotation, int glowFrames)
    {
        // Glow ring on flap
        if (glowFrames > 0)
        {
            float glowAlpha = glowFrames / 8f;
            using var glow = new SKPaint
            {
                Color = new SKColor(0xFF, 0xD7, 0x40, (byte)(glowAlpha * 130)),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12)
            };
            canvas.DrawCircle(cx, cy, 80, glow);
        }

        const float spriteW = 160f;
        const float spriteH = 130f;
        canvas.Save();
        canvas.RotateRadians(rotation, cx, cy);
        var dst = new SKRect(cx - spriteW / 2f, cy - spriteH / 2f, cx + spriteW / 2f, cy + spriteH / 2f);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawBitmap(koala, dst, paint);
        canvas.Restore();
    }

    private static void DrawVignette(SKCanvas canvas)
    {
        float cx = Width / 2f;
        float cy = Height / 2f;
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                Math.Max(Width, Height) * 0.7f,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 140) },
                new[] { 0.55f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
    }
}
