using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlappyBrainTheme5;

internal static class Program
{
    private const int Width = 800;
    private const int Height = 600;
    private const int TotalFrames = 900;
    private const int Fps = 30;
    private const string OutDir = "/tmp/fb-t5-frames";

    private static readonly Random Rng = new(42);

    private record StarParticle(float X, float Y, float Speed, float Size, bool IsBig, SKColor Color);
    private record AuroraBand(float Y, float Amplitude, float Wavelength, float Speed, float Phase, SKColor Color, float Thickness);
    private record PipePair(float X, float GapY, float GapSize);

    private static List<StarParticle> _stars = new();
    private static List<AuroraBand> _auroras = new();
    private static List<PipePair> _pipes = new();

    private static void Main()
    {
        Directory.CreateDirectory(OutDir);
        InitParticles();
        InitAuroras();
        InitPipes();

        for (int frame = 0; frame < TotalFrames; frame++)
        {
            using var bitmap = new SKBitmap(Width, Height);
            using var canvas = new SKCanvas(bitmap);
            DrawFrame(canvas, frame);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.OpenWrite(Path.Combine(OutDir, $"frame_{frame:D4}.png"));
            data.SaveTo(fs);
            if (frame % 60 == 0) Console.WriteLine($"Frame {frame}/{TotalFrames}");
        }
        Console.WriteLine("Frames done.");
    }

    private static void InitParticles()
    {
        for (int i = 0; i < 60; i++)
        {
            bool big = Rng.NextDouble() < 0.15;
            _stars.Add(new StarParticle(
                (float)(Rng.NextDouble() * Width),
                (float)(Rng.NextDouble() * Height),
                0.3f + (float)Rng.NextDouble() * 0.8f,
                big ? 8f + (float)Rng.NextDouble() * 3f : 1f + (float)Rng.NextDouble() * 2f,
                big,
                big ? new SKColor(0xFF, 0xD0, 0x40) : (Rng.NextDouble() < 0.5 ? SKColors.White : new SKColor(0xFF, 0xF0, 0xC0))
            ));
        }
    }

    private static void InitAuroras()
    {
        _auroras.Add(new AuroraBand(80, 25, 320, 0.4f, 0f, new SKColor(0x40, 0xFF, 0x80, (byte)(255 * 0.15)), 24));
        _auroras.Add(new AuroraBand(130, 30, 380, 0.55f, 1.2f, new SKColor(0x40, 0xC0, 0xFF, (byte)(255 * 0.12)), 28));
        _auroras.Add(new AuroraBand(175, 22, 280, 0.7f, 2.4f, new SKColor(0xFF, 0x40, 0xC0, (byte)(255 * 0.10)), 22));
    }

    private static void InitPipes()
    {
        // Pipes scrolling right-to-left
        for (int i = 0; i < 8; i++)
        {
            float x = 900 + i * 280;
            float gap = 180 + (float)(Rng.NextDouble() * 80 - 40);
            float gapSize = 170;
            _pipes.Add(new PipePair(x, gap + 200, gapSize));
        }
    }

    private static void DrawFrame(SKCanvas canvas, int frame)
    {
        DrawSky(canvas);
        DrawAuroras(canvas, frame);
        DrawStars(canvas, frame);
        DrawShootingStar(canvas, frame);
        DrawUluru(canvas, frame);
        DrawHarbourBridge(canvas, frame);
        DrawOperaHouse(canvas, frame);
        DrawPipes(canvas, frame);
        DrawBird(canvas, frame);
        DrawVignette(canvas);
        DrawHud(canvas, frame);
        DrawTitleCard(canvas, frame);
        DrawGameOver(canvas, frame);
    }

    private static void DrawSky(SKCanvas canvas)
    {
        using var paint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, Height),
            new[]
            {
                new SKColor(0x0A, 0x05, 0x20),
                new SKColor(0x2A, 0x10, 0x60),
                new SKColor(0x6A, 0x20, 0xB0),
                new SKColor(0xC0, 0x40, 0xE0),
                new SKColor(0x0A, 0x05, 0x20)
            },
            new float[] { 0f, 0.35f, 0.65f, 0.85f, 1f },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        canvas.DrawRect(0, 0, Width, Height, paint);
    }

    private static void DrawAuroras(SKCanvas canvas, int frame)
    {
        foreach (var band in _auroras)
        {
            using var path = new SKPath();
            float scroll = frame * band.Speed;
            int steps = 60;
            float startX = -100;
            float endX = Width + 100;
            float stepW = (endX - startX) / steps;

            // Top edge
            path.MoveTo(startX, band.Y);
            for (int i = 0; i <= steps; i++)
            {
                float x = startX + i * stepW;
                float t = (x + scroll) / band.Wavelength + band.Phase;
                float y = band.Y + (float)Math.Sin(t) * band.Amplitude;
                path.LineTo(x, y);
            }
            // Bottom edge
            for (int i = steps; i >= 0; i--)
            {
                float x = startX + i * stepW;
                float t = (x + scroll) / band.Wavelength + band.Phase;
                float y = band.Y + (float)Math.Sin(t) * band.Amplitude + band.Thickness;
                path.LineTo(x, y);
            }
            path.Close();

            using var paint = new SKPaint
            {
                Color = band.Color,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
            };
            canvas.DrawPath(path, paint);
        }
    }

    private static void DrawStars(SKCanvas canvas, int frame)
    {
        for (int i = 0; i < _stars.Count; i++)
        {
            var s = _stars[i];
            float x = (s.X - frame * s.Speed * 0.3f) % Width;
            if (x < 0) x += Width;
            float y = s.Y;

            // Twinkle
            float twinkle = 0.6f + 0.4f * (float)Math.Sin(frame * 0.1f + i);
            byte alpha = (byte)(s.Color.Alpha * twinkle);
            var color = s.Color.WithAlpha(alpha);

            if (s.IsBig)
            {
                Draw4PointStar(canvas, x, y, s.Size, color);
            }
            else
            {
                using var paint = new SKPaint { Color = color, IsAntialias = true };
                canvas.DrawCircle(x, y, s.Size, paint);
            }
        }
    }

    private static void Draw4PointStar(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        using var paint = new SKPaint
        {
            Color = color,
            StrokeWidth = 1.2f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawLine(cx - r, cy, cx + r, cy, paint);
        canvas.DrawLine(cx, cy - r, cx, cy + r, paint);
        // small diagonal accents
        float r2 = r * 0.4f;
        canvas.DrawLine(cx - r2, cy - r2, cx + r2, cy + r2, paint);
        canvas.DrawLine(cx - r2, cy + r2, cx + r2, cy - r2, paint);
    }

    private static void DrawShootingStar(SKCanvas canvas, int frame)
    {
        int[] triggers = { 240, 480, 720 };
        foreach (var t in triggers)
        {
            int delta = frame - t;
            if (delta < 0 || delta >= 8) continue;
            float progress = delta / 8f;
            float startX = 750, startY = 50;
            float endX = 200, endY = 300;
            float curX = startX + (endX - startX) * progress;
            float curY = startY + (endY - startY) * progress;

            for (int trail = 5; trail >= 0; trail--)
            {
                float trailProg = progress - trail * 0.06f;
                if (trailProg < 0) continue;
                float tx = startX + (endX - startX) * trailProg;
                float ty = startY + (endY - startY) * trailProg;
                float ahead = trailProg + 0.04f;
                float ax = startX + (endX - startX) * ahead;
                float ay = startY + (endY - startY) * ahead;
                byte alpha = (byte)(255 * (1 - trail * 0.18f));
                using var paint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha(alpha),
                    StrokeWidth = 2,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawLine(tx, ty, ax, ay, paint);
            }

            using var head = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawCircle(curX, curY, 3, head);
        }
    }

    private static void DrawUluru(SKCanvas canvas, int frame)
    {
        float scrollX = -(frame * 0.08f * 4) % 1200;
        float baseX = 200 + scrollX;
        float y = 380;
        float w = 350, h = 120;

        // For wrap, draw twice
        for (int rep = 0; rep < 2; rep++)
        {
            float cx = baseX + rep * 1200;
            DrawUluruInstance(canvas, cx, y, w, h);
        }
    }

    private static void DrawUluruInstance(SKCanvas canvas, float cx, float topY, float w, float h)
    {
        // Halo
        using (var halo = new SKPaint
        {
            Color = new SKColor(0xFF, 0x80, 0x40, (byte)(255 * 0.12)),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 25)
        })
        {
            canvas.DrawOval(cx, topY + h / 2, 210, 80, halo);
        }

        // Body: rounded-top rectangle
        var rect = new SKRect(cx - w / 2, topY, cx + w / 2, topY + h);
        using (var body = new SKPaint
        {
            Color = new SKColor(0xC2, 0x40, 0x10),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        })
        {
            using var path = new SKPath();
            path.MoveTo(rect.Left, rect.Bottom);
            path.LineTo(rect.Left, rect.Top + 60);
            path.ArcTo(60, 60, 0, SKPathArcSize.Small, SKPathDirection.Clockwise, rect.Left + 60, rect.Top);
            path.LineTo(rect.Right - 60, rect.Top);
            path.ArcTo(60, 60, 0, SKPathArcSize.Small, SKPathDirection.Clockwise, rect.Right, rect.Top + 60);
            path.LineTo(rect.Right, rect.Bottom);
            path.Close();
            canvas.DrawPath(path, body);

            // Striations - clipped to body
            canvas.Save();
            canvas.ClipPath(path, antialias: true);
            using var stria = new SKPaint
            {
                Color = new SKColor(0xA0, 0x30, 0x08),
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            for (float yy = rect.Top + 15; yy < rect.Bottom; yy += 15)
            {
                canvas.DrawLine(rect.Left, yy, rect.Right, yy, stria);
            }
            canvas.Restore();
        }

        // Shadow ellipse
        using var shadow = new SKPaint
        {
            Color = new SKColor(0x1A, 0x0A, 0x00, (byte)(255 * 0.30)),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
        };
        canvas.DrawOval(cx, topY + h + 20, 150, 10, shadow);
    }

    private static void DrawHarbourBridge(SKCanvas canvas, int frame)
    {
        float scrollX = -(frame * 0.2f * 4) % 1400;
        for (int rep = 0; rep < 2; rep++)
        {
            float baseX = 600 + scrollX + rep * 1400;
            DrawBridgeInstance(canvas, baseX);
        }
    }

    private static void DrawBridgeInstance(SKCanvas canvas, float cx)
    {
        float archW = 300, archH = 120;
        float deckY = 350;
        float archTop = deckY - archH;

        // Pylons
        using var pylonPaint = new SKPaint { Color = new SKColor(0x4A, 0x4A, 0x4A), IsAntialias = true };
        canvas.DrawRect(cx - archW / 2 - 10, deckY - 80, 20, 80, pylonPaint);
        canvas.DrawRect(cx + archW / 2 - 10, deckY - 80, 20, 80, pylonPaint);

        // Arch
        using (var arch = new SKPaint
        {
            Color = new SKColor(0x4A, 0x4A, 0x4A),
            StrokeWidth = 6,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        })
        {
            using var path = new SKPath();
            path.MoveTo(cx - archW / 2, deckY);
            path.QuadTo(cx, archTop, cx + archW / 2, deckY);
            canvas.DrawPath(path, arch);
        }

        // Suspenders
        using (var cable = new SKPaint
        {
            Color = new SKColor(0x6A, 0x6A, 0x6A),
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        })
        {
            for (float xOff = -archW / 2 + 20; xOff <= archW / 2 - 20; xOff += 20)
            {
                float t = (xOff + archW / 2) / archW;
                // arch quadratic Y at x: B(t) = (1-t)^2*deckY + 2(1-t)t*archTop + t^2*deckY
                float ay = (1 - t) * (1 - t) * deckY + 2 * (1 - t) * t * archTop + t * t * deckY;
                canvas.DrawLine(cx + xOff, deckY, cx + xOff, ay, cable);
            }
        }

        // Deck
        using var deck = new SKPaint { Color = new SKColor(0x5A, 0x5A, 0x5A), IsAntialias = true };
        canvas.DrawRect(cx - archW / 2, deckY, archW, 12, deck);
    }

    private static void DrawOperaHouse(SKCanvas canvas, int frame)
    {
        float scrollX = -(frame * 0.25f * 4) % 1100;
        for (int rep = 0; rep < 2; rep++)
        {
            float baseX = 350 + scrollX + rep * 1100;
            DrawOperaInstance(canvas, baseX, 300);
        }
    }

    private static void DrawOperaInstance(SKCanvas canvas, float cx, float baseY)
    {
        // Harbour platform
        using var platform = new SKPaint { Color = new SKColor(0x2A, 0x2A, 0x3A), IsAntialias = true };
        canvas.DrawRect(cx - 110, baseY + 5, 220, 18, platform);

        using var sail = new SKPaint
        {
            Color = new SKColor(0xF0, 0xF0, 0xE8),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var sailEdge = new SKPaint
        {
            Color = new SKColor(0xC0, 0xC0, 0xB8),
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        // 3 sails
        DrawSail(canvas, cx - 60, baseY, 70, 100, sail, sailEdge);
        DrawSail(canvas, cx, baseY - 20, 80, 150, sail, sailEdge);
        DrawSail(canvas, cx + 55, baseY - 5, 65, 120, sail, sailEdge);
    }

    private static void DrawSail(SKCanvas canvas, float cx, float baseY, float w, float h, SKPaint fill, SKPaint edge)
    {
        using var path = new SKPath();
        path.MoveTo(cx - w / 2, baseY);
        path.QuadTo(cx - w / 4, baseY - h, cx, baseY - h + 5);
        path.QuadTo(cx + w / 4, baseY - h * 0.7f, cx + w / 2, baseY);
        path.Close();
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, edge);
    }

    private static void DrawPipes(SKCanvas canvas, int frame)
    {
        float speed = 3.0f;
        for (int i = 0; i < _pipes.Count; i++)
        {
            var p = _pipes[i];
            float x = p.X - frame * speed;
            // wrap
            float totalSpan = _pipes.Count * 280f;
            float wrapped = ((x % totalSpan) + totalSpan) % totalSpan - 280;
            // Draw if visible
            float screenX = wrapped;
            if (screenX < -150 || screenX > Width + 150) continue;

            float gapTop = p.GapY - p.GapSize / 2;
            float gapBottom = p.GapY + p.GapSize / 2;

            DrawStoneColumn(canvas, screenX, 0, gapTop, isTop: true);
            DrawStoneColumn(canvas, screenX, gapBottom, Height, isTop: false);
        }
    }

    private static void DrawStoneColumn(SKCanvas canvas, float cx, float yStart, float yEnd, bool isTop)
    {
        float blockW = 95, blockH = 30;
        float capW = 110, capH = 35;

        using var mortar = new SKPaint { Color = new SKColor(0x50, 0x50, 0x40), IsAntialias = false };
        canvas.DrawRect(cx - blockW / 2 - 2, yStart, blockW + 4, yEnd - yStart, mortar);

        // Where the cap goes (at the gap-facing end)
        float capY = isTop ? yEnd - capH : yStart;
        float bodyStart = isTop ? yStart : yStart + capH;
        float bodyEnd = isTop ? yEnd - capH : yEnd;

        // Stone blocks
        int blockIdx = 0;
        for (float y = bodyStart; y < bodyEnd; y += blockH + 1)
        {
            float h = Math.Min(blockH, bodyEnd - y);
            if (h <= 0) break;
            var color = blockIdx % 2 == 0 ? new SKColor(0x8B, 0x80, 0x70) : new SKColor(0xA0, 0x90, 0x80);
            using var block = new SKPaint { Color = color, IsAntialias = false };
            canvas.DrawRect(cx - blockW / 2, y, blockW, h, block);

            // moss patch occasionally
            if ((blockIdx * 7 + (int)cx) % 5 == 0)
            {
                using var moss = new SKPaint
                {
                    Color = new SKColor(0x4A, 0x6A, 0x20, (byte)(255 * 0.3)),
                    IsAntialias = true
                };
                canvas.DrawOval(cx - blockW / 2 + 15, y + h / 2, 14, 6, moss);
            }
            blockIdx++;
        }

        // Cap (keystone)
        using var cap = new SKPaint { Color = new SKColor(0x70, 0x60, 0x50), IsAntialias = false };
        canvas.DrawRect(cx - capW / 2, capY, capW, capH, cap);
        using var capEdge = new SKPaint
        {
            Color = new SKColor(0x50, 0x40, 0x30),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(cx - capW / 2, capY, capW, capH, capEdge);
    }

    private static void DrawBird(SKCanvas canvas, int frame)
    {
        // position
        float birdX = 200;
        float birdY = ComputeBirdY(frame);
        float rockDeg = (float)Math.Sin(frame * 0.08) * 5f;

        canvas.Save();
        canvas.Translate(birdX, birdY);
        canvas.RotateDegrees(rockDeg);

        // Star trail behind boat (drawn before boat so trail emerges from rear)
        for (int i = 0; i < 7; i++)
        {
            float trailX = -50 - i * 14;
            float trailY = (float)Math.Sin((frame + i * 8) * 0.1) * 4 + i * 1.5f;
            byte alpha = (byte)(220 - i * 28);
            Draw4PointStar(canvas, trailX, trailY + 20, 5, new SKColor(0xFF, 0xD0, 0x40, alpha));
        }

        // Oars
        using (var oarPaint = new SKPaint { Color = new SKColor(0x6A, 0x40, 0x20), StrokeWidth = 4, IsAntialias = true, StrokeCap = SKStrokeCap.Round })
        {
            float oarSwing = (float)Math.Sin(frame * 0.18) * 8;
            canvas.DrawLine(-40, 25, -65, 50 + oarSwing, oarPaint);
            canvas.DrawLine(40, 25, 65, 50 - oarSwing, oarPaint);
        }

        // Boat hull (trapezoid: wider at top)
        using (var hullPath = new SKPath())
        {
            hullPath.MoveTo(-60, 5);   // top-left
            hullPath.LineTo(60, 5);    // top-right
            hullPath.LineTo(50, 50);   // bottom-right
            hullPath.LineTo(-50, 50);  // bottom-left
            hullPath.Close();
            using var hull = new SKPaint { Color = new SKColor(0x8B, 0x5E, 0x3C), IsAntialias = true };
            canvas.DrawPath(hullPath, hull);
            using var hullEdge = new SKPaint
            {
                Color = new SKColor(0x4A, 0x2A, 0x10),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };
            canvas.DrawPath(hullPath, hullEdge);
        }
        // plank lines
        using (var plank = new SKPaint { Color = new SKColor(0x6A, 0x40, 0x20), StrokeWidth = 1, IsAntialias = true })
        {
            canvas.DrawLine(-55, 18, 55, 18, plank);
            canvas.DrawLine(-53, 30, 53, 30, plank);
            canvas.DrawLine(-51, 42, 51, 42, plank);
        }

        // Wombat body
        using (var body = new SKPaint { Color = new SKColor(0x88, 0x88, 0x80), IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRect(-35, -35, 35, 15), 22, 22, body);
        }
        // ears
        using (var ear = new SKPaint { Color = new SKColor(0x70, 0x70, 0x68), IsAntialias = true })
        {
            canvas.DrawCircle(-22, -32, 7, ear);
            canvas.DrawCircle(22, -32, 7, ear);
        }
        // inner ears
        using (var innerEar = new SKPaint { Color = new SKColor(0x55, 0x40, 0x40), IsAntialias = true })
        {
            canvas.DrawCircle(-22, -32, 3, innerEar);
            canvas.DrawCircle(22, -32, 3, innerEar);
        }
        // eyes
        using (var eye = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.DrawCircle(-10, -15, 2.5f, eye);
            canvas.DrawCircle(10, -15, 2.5f, eye);
        }
        // eye shine
        using (var shine = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawCircle(-9, -16, 0.8f, shine);
            canvas.DrawCircle(11, -16, 0.8f, shine);
        }
        // nose (flat)
        using (var nose = new SKPaint { Color = new SKColor(0x33, 0x22, 0x22), IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRect(-6, -7, 6, -1), 3, 3, nose);
        }

        canvas.Restore();
    }

    private static float ComputeBirdY(int frame)
    {
        // Title card hover
        if (frame < 60) return 300 + (float)Math.Sin(frame * 0.1) * 8;

        // Game over fall
        if (frame >= 840)
        {
            int dt = frame - 840;
            return 300 + dt * dt * 0.3f;
        }

        // Gameplay: rise on flap, fall by gravity
        int[] flapFrames = { 80, 140, 190, 260, 310, 380, 440, 510, 570, 640, 700, 760, 820 };
        float vel = 0;
        float y = 300;
        float gravity = 0.55f;
        float flapImpulse = -8.5f;
        for (int f = 60; f <= frame; f++)
        {
            bool isFlap = false;
            foreach (var ff in flapFrames) if (ff == f) { isFlap = true; break; }
            if (isFlap) vel = flapImpulse;
            vel += gravity;
            y += vel;
            if (y < 50) { y = 50; vel = 0; }
            if (y > 520) { y = 520; vel = 0; }
        }
        return y;
    }

    private static void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint();
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(Width / 2f, Height / 2f),
            Width * 0.7f,
            new[] { new SKColor(0, 0, 0, 0), new SKColor(0x0A, 0x05, 0x20, 180) },
            new float[] { 0.55f, 1f },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        canvas.DrawRect(0, 0, Width, Height, paint);
    }

    private static void DrawHud(SKCanvas canvas, int frame)
    {
        if (frame < 60) return;
        int score = Math.Min((frame - 60) / 60, 12);
        string text = $"SCORE: {score}";
        using var shadow = new SKPaint
        {
            Color = new SKColor(0x40, 0x10, 0x60),
            IsAntialias = true,
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        using var fg = new SKPaint
        {
            Color = new SKColor(0xE0, 0xC0, 0xFF),
            IsAntialias = true,
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold)
        };
        canvas.DrawText(text, 22, 42, shadow);
        canvas.DrawText(text, 20, 40, fg);
    }

    private static void DrawTitleCard(SKCanvas canvas, int frame)
    {
        if (frame >= 60) return;
        using var overlay = new SKPaint { Color = new SKColor(0x40, 0x10, 0x60, (byte)(255 * 0.6)) };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        using var title = new SKPaint
        {
            Color = new SKColor(0xC0, 0x40, 0xE0),
            IsAntialias = true,
            TextSize = 52,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        using var titleShadow = new SKPaint
        {
            Color = new SKColor(0x20, 0x00, 0x40),
            IsAntialias = true,
            TextSize = 52,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("DISPLACED LANDMARKS", Width / 2 + 3, 273, titleShadow);
        canvas.DrawText("DISPLACED LANDMARKS", Width / 2, 270, title);

        using var sub = new SKPaint
        {
            Color = new SKColor(0x80, 0x60, 0xFF),
            IsAntialias = true,
            TextSize = 30,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("FlappyBrain 🧠", Width / 2, 330, sub);
    }

    private static void DrawGameOver(SKCanvas canvas, int frame)
    {
        if (frame < 840) return;
        float prog = (frame - 840) / 60f;
        byte alpha = (byte)Math.Min(220, 220 * prog * 2);
        using var overlay = new SKPaint { Color = new SKColor(0x20, 0x00, 0x40, alpha) };
        canvas.DrawRect(0, 0, Width, Height, overlay);

        using var go = new SKPaint
        {
            Color = new SKColor(0xC0, 0x40, 0xE0),
            IsAntialias = true,
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        using var goShadow = new SKPaint
        {
            Color = new SKColor(0x10, 0x00, 0x20),
            IsAntialias = true,
            TextSize = 80,
            Typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("GAME OVER", Width / 2 + 4, 320, goShadow);
        canvas.DrawText("GAME OVER", Width / 2, 316, go);
    }
}
