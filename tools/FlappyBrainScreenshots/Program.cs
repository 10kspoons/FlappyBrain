using SkiaSharp;
using System.Collections.Generic;

namespace FlappyBrainScreenshots;

internal static class Program
{
    private const int W = 800;
    private const int H = 600;

    public static void Main()
    {
        RenderScreenshot("/tmp/training-theme-a.png", Theme.A);
        RenderScreenshot("/tmp/training-theme-b.png", Theme.B);
        Console.WriteLine("Done.");
    }

    private enum Theme { A, B }

    private static void RenderScreenshot(string path, Theme theme)
    {
        using var surface = SKSurface.Create(new SKImageInfo(W, H));
        var canvas = surface.Canvas;

        DrawBackground(canvas, theme);
        DrawParallaxScene(canvas, theme);
        DrawMainPanel(canvas, theme);
        DrawPanelNoise(canvas, theme);
        DrawTitleBar(canvas, theme);
        DrawCountdownRing(canvas, theme);
        DrawPushTrainingHeading(canvas, theme);
        DrawSessionSubtitle(canvas, theme);
        DrawPowerGauge(canvas, theme);
        DrawEegMeter(canvas, theme);
        DrawSessionBolts(canvas, theme);
        DrawRivetDetails(canvas, theme);

        if (theme == Theme.B)
        {
            DrawVignette(canvas);
        }

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 95);
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
        Console.WriteLine($"Wrote {path}");
    }

    // ---------- Palette ----------
    private static SKColor SkyTop(Theme t) => t == Theme.A ? new SKColor(0x4A, 0x2A, 0x1A) : new SKColor(0x1A, 0x0F, 0x08);
    private static SKColor SkyMid(Theme t) => t == Theme.A ? new SKColor(0x8B, 0x44, 0x22) : new SKColor(0x6B, 0x30, 0x20);
    private static SKColor SkyBottom(Theme t) => new SKColor(0xC4, 0x62, 0x2D);
    private static SKColor Ground(Theme t) => t == Theme.A ? new SKColor(0xB5, 0x45, 0x1B) : new SKColor(0x80, 0x30, 0x18);
    private static SKColor PanelFill(Theme t) => t == Theme.A ? new SKColor(0x5A, 0x3A, 0x20) : new SKColor(0x3A, 0x24, 0x14);
    private static SKColor PanelBorder(Theme t) => t == Theme.A ? new SKColor(0x8B, 0x5E, 0x3C) : new SKColor(0x6E, 0x46, 0x28);
    private static SKColor PanelDark(Theme t) => t == Theme.A ? new SKColor(0x2E, 0x1C, 0x10) : new SKColor(0x18, 0x0C, 0x06);
    private static SKColor TitleBar(Theme t) => t == Theme.A ? new SKColor(0x3A, 0x24, 0x14) : new SKColor(0x22, 0x14, 0x0A);
    private static SKColor Gold(Theme t) => t == Theme.A ? new SKColor(0xFF, 0xC9, 0x4F) : new SKColor(0xFF, 0xB8, 0x40);
    private static SKColor GoldDim(Theme t) => t == Theme.A ? new SKColor(0xA0, 0x70, 0x28) : new SKColor(0x80, 0x55, 0x18);
    private static SKColor GreenSig(Theme t) => t == Theme.A ? new SKColor(0x6E, 0xE0, 0x55) : new SKColor(0x52, 0xC0, 0x40);
    private static SKColor TextLight(Theme t) => t == Theme.A ? new SKColor(0xF4, 0xE0, 0xB8) : new SKColor(0xE8, 0xCE, 0x9C);
    private static SKColor TextDim(Theme t) => t == Theme.A ? new SKColor(0xC0, 0x9A, 0x6E) : new SKColor(0x9A, 0x78, 0x52);
    private static SKColor RustStreak(Theme t) => t == Theme.A ? new SKColor(0x6E, 0x36, 0x14) : new SKColor(0x4E, 0x22, 0x0E);

    // ---------- Background sky gradient ----------
    private static void DrawBackground(SKCanvas canvas, Theme theme)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, H),
                new[] { SkyTop(theme), SkyMid(theme), SkyBottom(theme) },
                new[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, W, H, paint);
    }

    // ---------- Parallax outback scene ----------
    private static void DrawParallaxScene(SKCanvas canvas, Theme theme)
    {
        // Distant ridge
        using (var p = new SKPaint { Color = MultiplyColor(SkyMid(theme), 0.85f), IsAntialias = false })
        {
            var path = new SKPath();
            path.MoveTo(0, 380);
            path.LineTo(80, 360); path.LineTo(140, 372); path.LineTo(200, 350);
            path.LineTo(280, 358); path.LineTo(360, 340); path.LineTo(440, 352);
            path.LineTo(520, 342); path.LineTo(600, 358); path.LineTo(680, 348);
            path.LineTo(760, 360); path.LineTo(W, 354); path.LineTo(W, H); path.LineTo(0, H);
            path.Close();
            canvas.DrawPath(path, p);
        }

        // Mid ridge
        using (var p = new SKPaint { Color = MultiplyColor(Ground(theme), 0.75f), IsAntialias = false })
        {
            var path = new SKPath();
            path.MoveTo(0, 430);
            path.LineTo(60, 420); path.LineTo(140, 432); path.LineTo(220, 415);
            path.LineTo(300, 425); path.LineTo(380, 408); path.LineTo(460, 422);
            path.LineTo(540, 414); path.LineTo(620, 426); path.LineTo(700, 418);
            path.LineTo(780, 428); path.LineTo(W, 422); path.LineTo(W, H); path.LineTo(0, H);
            path.Close();
            canvas.DrawPath(path, p);
        }

        // Foreground ground
        using (var p = new SKPaint { Color = Ground(theme), IsAntialias = false })
        {
            canvas.DrawRect(0, 470, W, H - 470, p);
        }

        // Ground texture stripes (cracks)
        using (var p = new SKPaint { Color = MultiplyColor(Ground(theme), 0.65f), IsAntialias = false, StrokeWidth = 1 })
        {
            for (int i = 0; i < 30; i++)
            {
                int x = (i * 71) % W;
                int y = 478 + (i * 13) % 110;
                canvas.DrawLine(x, y, x + 24, y + 2, p);
            }
        }

        // Dead trees silhouettes
        DrawDeadTree(canvas, theme, 90, 470);
        DrawDeadTree(canvas, theme, 720, 470);
        DrawDeadTree(canvas, theme, 30, 470);

        // Distant ruins (broken walls)
        DrawRuin(canvas, theme, 540, 410, 70, 48);
        DrawRuin(canvas, theme, 200, 400, 56, 40);
    }

    private static void DrawDeadTree(SKCanvas canvas, Theme theme, int baseX, int baseY)
    {
        using var p = new SKPaint { Color = new SKColor(0x18, 0x0C, 0x06), IsAntialias = false, StrokeWidth = 3, Style = SKPaintStyle.Stroke };
        canvas.DrawLine(baseX, baseY, baseX, baseY - 60, p);
        canvas.DrawLine(baseX, baseY - 40, baseX - 14, baseY - 56, p);
        canvas.DrawLine(baseX, baseY - 48, baseX + 12, baseY - 64, p);
        canvas.DrawLine(baseX - 14, baseY - 56, baseX - 22, baseY - 50, p);
        canvas.DrawLine(baseX + 12, baseY - 64, baseX + 20, baseY - 60, p);
    }

    private static void DrawRuin(SKCanvas canvas, Theme theme, int x, int y, int w, int h)
    {
        using var p = new SKPaint { Color = new SKColor(0x35, 0x1E, 0x10), IsAntialias = false };
        // Broken wall outline
        canvas.DrawRect(x, y, w, h, p);
        // Notches
        using var bg = new SKPaint { Color = SkyMid(theme), IsAntialias = false };
        canvas.DrawRect(x + 12, y, 8, 14, bg);
        canvas.DrawRect(x + w - 18, y, 6, 8, bg);
        canvas.DrawRect(x + w / 2 - 4, y + 6, 8, 10, bg);
    }

    // ---------- Main metal panel ----------
    private static void DrawMainPanel(SKCanvas canvas, Theme theme)
    {
        var rect = new SKRect(40, 40, W - 40, H - 40);

        // Outer dark frame (shadow)
        using (var p = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = false })
        {
            canvas.DrawRect(rect.Left + 4, rect.Top + 4, rect.Width, rect.Height, p);
        }

        // Panel fill with subtle vertical gradient
        using (var p = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Bottom),
                new[] { LightenColor(PanelFill(theme), 0.10f), PanelFill(theme), MultiplyColor(PanelFill(theme), 0.85f) },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawRect(rect, p);
        }

        // Inner border (lighter)
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = false })
        {
            canvas.DrawRect(rect, p);
        }

        // Outer accent border
        using (var p = new SKPaint { Color = PanelDark(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = false })
        {
            canvas.DrawRect(rect.Left - 4, rect.Top - 4, rect.Width + 8, rect.Height + 8, p);
        }

        // Rust streaks
        using (var p = new SKPaint { Color = RustStreak(theme), IsAntialias = false })
        {
            DrawRustStreak(canvas, p, 110, 90, 6, 32);
            DrawRustStreak(canvas, p, 340, 88, 5, 28);
            DrawRustStreak(canvas, p, 620, 92, 7, 36);
            DrawRustStreak(canvas, p, 70, 480, 5, 24);
            DrawRustStreak(canvas, p, 720, 490, 6, 28);
            DrawRustStreak(canvas, p, 460, 96, 4, 22);
        }

        // Inner divider (between title bar area)
        using (var p = new SKPaint { Color = PanelDark(theme), IsAntialias = false })
        {
            canvas.DrawRect(rect.Left + 6, 96, rect.Width - 12, 2, p);
        }
    }

    private static void DrawPanelNoise(SKCanvas canvas, Theme theme)
    {
        // Speckled metal texture inside the panel
        var rng = new Random(2026);
        var rect = new SKRect(46, 100, W - 46, H - 46);
        using var dark = new SKPaint { Color = new SKColor(0, 0, 0, 30), IsAntialias = false };
        using var light = new SKPaint { Color = new SKColor(255, 220, 160, 18), IsAntialias = false };
        for (int i = 0; i < 9000; i++)
        {
            int x = (int)rect.Left + rng.Next((int)rect.Width);
            int y = (int)rect.Top + rng.Next((int)rect.Height);
            var p = (i % 3 == 0) ? light : dark;
            canvas.DrawRect(x, y, 1, 1, p);
        }
        // Horizontal scanline streaks
        using var scan = new SKPaint { Color = new SKColor(0, 0, 0, 18), IsAntialias = false };
        for (int y = (int)rect.Top; y < rect.Bottom; y += 4)
        {
            canvas.DrawRect(rect.Left, y, rect.Width, 1, scan);
        }
    }

    private static void DrawRustStreak(SKCanvas canvas, SKPaint p, int x, int y, int w, int h)
    {
        canvas.DrawRect(x, y, w, h, p);
        // Drips
        canvas.DrawRect(x + 1, y + h, 2, 4, p);
        canvas.DrawRect(x + w - 2, y + h - 2, 2, 6, p);
    }

    // ---------- Title bar ----------
    private static void DrawTitleBar(SKCanvas canvas, Theme theme)
    {
        // Title bar strip
        using (var p = new SKPaint { Color = TitleBar(theme), IsAntialias = false })
        {
            canvas.DrawRect(46, 48, W - 92, 50, p);
        }
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false })
        {
            canvas.DrawRect(46, 48, W - 92, 50, p);
        }

        // Title: BRAIN CALIBRATION UNIT (centered)
        DrawPixelText(canvas, "BRAIN CALIBRATION UNIT", W / 2, 60, 3, Gold(theme), centered: true);
        // Subtitle: THINK-TO-FLAP TRAINING
        DrawPixelText(canvas, "THINK-TO-FLAP TRAINING", W / 2, 82, 2, TextDim(theme), centered: true);

        // Side decorations - small bracket
        using (var p = new SKPaint { Color = Gold(theme), IsAntialias = false })
        {
            canvas.DrawRect(56, 58, 18, 3, p);
            canvas.DrawRect(56, 58, 3, 14, p);
            canvas.DrawRect(56, 84, 18, 3, p);
            canvas.DrawRect(56, 78, 3, 9, p);

            canvas.DrawRect(W - 74, 58, 18, 3, p);
            canvas.DrawRect(W - 59, 58, 3, 14, p);
            canvas.DrawRect(W - 74, 84, 18, 3, p);
            canvas.DrawRect(W - 59, 78, 3, 9, p);
        }
    }

    // ---------- Countdown ring ----------
    private static void DrawCountdownRing(SKCanvas canvas, Theme theme)
    {
        int cx = W / 2;
        int cy = 320;
        int radius = 90;
        int dotSize = 6;
        int dotCount = 36;
        // Progress ~40% (about 5 seconds of 12 elapsed = 40%? task says 40%, 5 remaining)
        float progress = 0.40f;
        int litCount = (int)(dotCount * progress);

        for (int i = 0; i < dotCount; i++)
        {
            // Start at top (-90 degrees)
            double ang = -Math.PI / 2 + (i / (double)dotCount) * Math.PI * 2;
            int x = (int)(cx + Math.Cos(ang) * radius);
            int y = (int)(cy + Math.Sin(ang) * radius);
            bool lit = i < litCount;
            var color = lit ? Gold(theme) : GoldDim(theme);
            using var p = new SKPaint { Color = MultiplyColor(color, lit ? 1f : 0.4f), IsAntialias = false };
            canvas.DrawRect(x - dotSize / 2, y - dotSize / 2, dotSize, dotSize, p);
        }

        // Inner dark disc
        using (var p = new SKPaint { Color = PanelDark(theme), IsAntialias = false })
        {
            // Filled circle approximated as octagon-ish via drawing
            var path = new SKPath();
            int rIn = radius - 18;
            for (int i = 0; i < 24; i++)
            {
                double a = (i / 24.0) * Math.PI * 2;
                float xx = cx + (float)Math.Cos(a) * rIn;
                float yy = cy + (float)Math.Sin(a) * rIn;
                if (i == 0) path.MoveTo(xx, yy); else path.LineTo(xx, yy);
            }
            path.Close();
            canvas.DrawPath(path, p);
        }

        // Inner highlight ring
        using (var p = new SKPaint { Color = MultiplyColor(Gold(theme), 0.5f), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false })
        {
            var path = new SKPath();
            int rIn = radius - 18;
            for (int i = 0; i < 36; i++)
            {
                double a = (i / 36.0) * Math.PI * 2;
                float xx = cx + (float)Math.Cos(a) * rIn;
                float yy = cy + (float)Math.Sin(a) * rIn;
                if (i == 0) path.MoveTo(xx, yy); else path.LineTo(xx, yy);
            }
            path.Close();
            canvas.DrawPath(path, p);
        }

        // Big "5" digit center
        DrawPixelText(canvas, "5", cx, cy - 20, 8, Gold(theme), centered: true);

        // Small label below the digit
        DrawPixelText(canvas, "SECONDS", cx, cy + 40, 2, TextDim(theme), centered: true);
    }

    // ---------- Push training heading ----------
    private static void DrawPushTrainingHeading(SKCanvas canvas, Theme theme)
    {
        DrawPixelText(canvas, ">>> THINK PUSH <<<", W / 2, 150, 4, Gold(theme), centered: true);
    }

    private static void DrawSessionSubtitle(SKCanvas canvas, Theme theme)
    {
        DrawPixelText(canvas, "SESSION 3 OF 5", W / 2, 200, 2, TextLight(theme), centered: true);
    }

    // ---------- BCI Power Gauge (left) ----------
    private static void DrawPowerGauge(SKCanvas canvas, Theme theme)
    {
        int gx = 80;
        int gy = 240;
        int gw = 60;
        int gh = 220;

        // Trough
        using (var p = new SKPaint { Color = PanelDark(theme), IsAntialias = false })
        {
            canvas.DrawRect(gx, gy, gw, gh, p);
        }
        // Trough border
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = false })
        {
            canvas.DrawRect(gx, gy, gw, gh, p);
        }

        // Filled bar (72%)
        float fillPct = 0.72f;
        int fillH = (int)(gh * fillPct);
        var fillRect = new SKRect(gx + 4, gy + gh - fillH + 2, gx + gw - 4, gy + gh - 2);
        using (var p = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(fillRect.Left, fillRect.Top),
                new SKPoint(fillRect.Left, fillRect.Bottom),
                new[] { LightenColor(Gold(theme), 0.20f), Gold(theme), MultiplyColor(Gold(theme), 0.7f) },
                new[] { 0f, 0.4f, 1f },
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawRect(fillRect, p);
        }

        // Segment lines (every 10%)
        using (var p = new SKPaint { Color = new SKColor(0, 0, 0, 130), IsAntialias = false })
        {
            for (int i = 1; i < 10; i++)
            {
                int yy = gy + (gh * i / 10);
                canvas.DrawRect(gx + 4, yy, gw - 8, 1, p);
            }
        }

        // Threshold line at 50%
        int thresholdY = gy + gh / 2;
        using (var p = new SKPaint { Color = new SKColor(0xFF, 0x40, 0x30), IsAntialias = false })
        {
            canvas.DrawRect(gx - 6, thresholdY - 1, gw + 12, 3, p);
            // small triangles
            canvas.DrawRect(gx - 10, thresholdY - 2, 4, 5, p);
            canvas.DrawRect(gx + gw + 6, thresholdY - 2, 4, 5, p);
        }

        // Label above
        DrawPixelText(canvas, "BCI POWER", gx + gw / 2, gy - 20, 2, TextLight(theme), centered: true);
        // Percent value
        DrawPixelText(canvas, "72%", gx + gw / 2, gy + gh + 14, 2, Gold(theme), centered: true);
        // Threshold label
        DrawPixelText(canvas, "THRESH", gx + gw + 14, thresholdY - 4, 1, new SKColor(0xFF, 0x60, 0x40), centered: false);
    }

    // ---------- EEG Signal meter (top right) ----------
    private static void DrawEegMeter(SKCanvas canvas, Theme theme)
    {
        int ex = 590;
        int ey = 230;
        int ew = 150;
        int eh = 22;

        DrawPixelText(canvas, "EEG SIGNAL", ex, ey - 14, 2, TextLight(theme), centered: false);

        // Trough
        using (var p = new SKPaint { Color = PanelDark(theme), IsAntialias = false })
        {
            canvas.DrawRect(ex, ey, ew, eh, p);
        }
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = false })
        {
            canvas.DrawRect(ex, ey, ew, eh, p);
        }

        // Filled segments (80%) - 10 segments
        int segs = 10;
        int litSegs = 8;
        int segGap = 2;
        int segW = (ew - 6 - segGap * (segs - 1)) / segs;
        for (int i = 0; i < segs; i++)
        {
            int sx = ex + 3 + i * (segW + segGap);
            var color = i < litSegs ? GreenSig(theme) : MultiplyColor(GreenSig(theme), 0.18f);
            using var p = new SKPaint { Color = color, IsAntialias = false };
            canvas.DrawRect(sx, ey + 3, segW, eh - 6, p);
        }

        DrawPixelText(canvas, "80%", ex + ew + 10, ey + 6, 2, GreenSig(theme), centered: false);

        // Below: a tiny waveform readout
        int wx = ex;
        int wy = ey + 38;
        int ww = ew + 40;
        int wh = 28;
        using (var p = new SKPaint { Color = PanelDark(theme), IsAntialias = false })
        {
            canvas.DrawRect(wx, wy, ww, wh, p);
        }
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false })
        {
            canvas.DrawRect(wx, wy, ww, wh, p);
        }
        // Waveform
        using (var p = new SKPaint { Color = GreenSig(theme), IsAntialias = false, StrokeWidth = 1, Style = SKPaintStyle.Stroke })
        {
            int prevY = wy + wh / 2;
            var rng = new Random(42);
            for (int i = 0; i < ww - 2; i += 2)
            {
                int amp = (int)(Math.Sin(i * 0.18) * 6 + Math.Sin(i * 0.42) * 4);
                int yy = wy + wh / 2 + amp + rng.Next(-1, 2);
                canvas.DrawLine(wx + i, prevY, wx + i + 2, yy, p);
                prevY = yy;
            }
        }
    }

    // ---------- Session bolts (bottom) ----------
    private static void DrawSessionBolts(SKCanvas canvas, Theme theme)
    {
        DrawPixelText(canvas, "SESSION PROGRESS", W / 2, 478, 2, TextLight(theme), centered: true);

        int boltCount = 5;
        int spacing = 90;
        int totalW = (boltCount - 1) * spacing;
        int startX = W / 2 - totalW / 2;
        int by = 528;

        for (int i = 0; i < boltCount; i++)
        {
            int bx = startX + i * spacing;
            bool lit = i <= 2; // first 3 lit
            bool pulsing = i == 2;
            DrawBolt(canvas, theme, bx, by, lit, pulsing);
        }
    }

    private static void DrawBolt(SKCanvas canvas, Theme theme, int cx, int cy, bool lit, bool pulsing)
    {
        // Bolt is a lightning shape made from rectangles (pixel art)
        var bgColor = MultiplyColor(PanelDark(theme), 1.0f);
        using (var p = new SKPaint { Color = bgColor, IsAntialias = false })
        {
            canvas.DrawRect(cx - 22, cy - 26, 44, 52, p);
        }
        using (var p = new SKPaint { Color = PanelBorder(theme), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false })
        {
            canvas.DrawRect(cx - 22, cy - 26, 44, 52, p);
        }

        SKColor fill = lit ? Gold(theme) : MultiplyColor(GoldDim(theme), 0.5f);
        if (pulsing) fill = LightenColor(Gold(theme), 0.15f);

        // glow ring around lit bolts
        if (lit)
        {
            using var p = new SKPaint { Color = new SKColor(fill.Red, fill.Green, fill.Blue, 60), IsAntialias = false };
            canvas.DrawRect(cx - 26, cy - 30, 52, 60, p);
        }

        using (var p = new SKPaint { Color = fill, IsAntialias = false })
        {
            // Lightning bolt pixel pattern
            // Top-down zigzag
            canvas.DrawRect(cx - 4, cy - 20, 10, 4, p);
            canvas.DrawRect(cx - 8, cy - 16, 14, 4, p);
            canvas.DrawRect(cx - 10, cy - 12, 14, 4, p);
            canvas.DrawRect(cx - 6, cy - 8, 14, 4, p);
            canvas.DrawRect(cx - 2, cy - 4, 12, 4, p);
            canvas.DrawRect(cx - 8, cy, 16, 4, p);
            canvas.DrawRect(cx - 4, cy + 4, 10, 4, p);
            canvas.DrawRect(cx, cy + 8, 6, 4, p);
            canvas.DrawRect(cx + 2, cy + 12, 4, 4, p);
        }
    }

    // ---------- Rivet details ----------
    private static void DrawRivetDetails(SKCanvas canvas, Theme theme)
    {
        var rect = new SKRect(40, 40, W - 40, H - 40);
        using var rivet = new SKPaint { Color = MultiplyColor(PanelBorder(theme), 0.6f), IsAntialias = false };
        using var rivetHi = new SKPaint { Color = LightenColor(PanelBorder(theme), 0.20f), IsAntialias = false };

        void Rivet(float x, float y)
        {
            canvas.DrawRect(x - 3, y - 3, 6, 6, rivet);
            canvas.DrawRect(x - 2, y - 2, 2, 2, rivetHi);
        }

        // Corners
        Rivet(rect.Left + 12, rect.Top + 12);
        Rivet(rect.Right - 12, rect.Top + 12);
        Rivet(rect.Left + 12, rect.Bottom - 12);
        Rivet(rect.Right - 12, rect.Bottom - 12);

        // Mid edges
        Rivet((rect.Left + rect.Right) / 2, rect.Top + 12);
        Rivet((rect.Left + rect.Right) / 2, rect.Bottom - 12);
        Rivet(rect.Left + 12, (rect.Top + rect.Bottom) / 2);
        Rivet(rect.Right - 12, (rect.Top + rect.Bottom) / 2);

        // Quarters along top/bottom
        Rivet(rect.Left + rect.Width * 0.25f, rect.Top + 12);
        Rivet(rect.Left + rect.Width * 0.75f, rect.Top + 12);
        Rivet(rect.Left + rect.Width * 0.25f, rect.Bottom - 12);
        Rivet(rect.Left + rect.Width * 0.75f, rect.Bottom - 12);

        // Title bar rivets
        Rivet(50, 52);
        Rivet(W - 50, 52);
        Rivet(50, 92);
        Rivet(W - 50, 92);
    }

    // ---------- Vignette overlay (Theme B) ----------
    private static void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(W / 2f, H / 2f),
                Math.Max(W, H) * 0.62f,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 70), new SKColor(0, 0, 0, 180) },
                new[] { 0f, 0.65f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, W, H, paint);
    }

    // ---------- Color helpers ----------
    private static SKColor MultiplyColor(SKColor c, float f)
    {
        return new SKColor(
            (byte)Math.Clamp(c.Red * f, 0, 255),
            (byte)Math.Clamp(c.Green * f, 0, 255),
            (byte)Math.Clamp(c.Blue * f, 0, 255),
            c.Alpha);
    }
    private static SKColor LightenColor(SKColor c, float amount)
    {
        return new SKColor(
            (byte)Math.Clamp(c.Red + 255 * amount, 0, 255),
            (byte)Math.Clamp(c.Green + 255 * amount, 0, 255),
            (byte)Math.Clamp(c.Blue + 255 * amount, 0, 255),
            c.Alpha);
    }

    // ---------- Pixel font ----------
    private static readonly Dictionary<char, string[]> Glyphs = BuildGlyphs();

    private static void DrawPixelText(SKCanvas canvas, string text, int x, int y, int scale, SKColor color, bool centered)
    {
        const int charW = 5;
        const int charH = 7;
        const int spacing = 1;
        int totalW = text.Length * (charW + spacing) * scale - spacing * scale;

        int startX = centered ? x - totalW / 2 : x;
        int curX = startX;

        using var p = new SKPaint { Color = color, IsAntialias = false };

        foreach (char ch in text)
        {
            char up = char.ToUpperInvariant(ch);
            if (!Glyphs.TryGetValue(up, out var rows))
            {
                if (up == ' ') { curX += (charW + spacing) * scale; continue; }
                rows = Glyphs['?'];
            }
            for (int r = 0; r < charH; r++)
            {
                string row = rows[r];
                for (int c = 0; c < charW; c++)
                {
                    if (c < row.Length && row[c] == 'X')
                    {
                        canvas.DrawRect(curX + c * scale, y + r * scale, scale, scale, p);
                    }
                }
            }
            curX += (charW + spacing) * scale;
        }
    }

    private static Dictionary<char, string[]> BuildGlyphs()
    {
        var d = new Dictionary<char, string[]>();
        d['A'] = new[] { ".XXX.", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X" };
        d['B'] = new[] { "XXXX.", "X...X", "X...X", "XXXX.", "X...X", "X...X", "XXXX." };
        d['C'] = new[] { ".XXXX", "X....", "X....", "X....", "X....", "X....", ".XXXX" };
        d['D'] = new[] { "XXXX.", "X...X", "X...X", "X...X", "X...X", "X...X", "XXXX." };
        d['E'] = new[] { "XXXXX", "X....", "X....", "XXXX.", "X....", "X....", "XXXXX" };
        d['F'] = new[] { "XXXXX", "X....", "X....", "XXXX.", "X....", "X....", "X...." };
        d['G'] = new[] { ".XXXX", "X....", "X....", "X..XX", "X...X", "X...X", ".XXXX" };
        d['H'] = new[] { "X...X", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X" };
        d['I'] = new[] { "XXXXX", "..X..", "..X..", "..X..", "..X..", "..X..", "XXXXX" };
        d['J'] = new[] { "XXXXX", "....X", "....X", "....X", "....X", "X...X", ".XXX." };
        d['K'] = new[] { "X...X", "X..X.", "X.X..", "XX...", "X.X..", "X..X.", "X...X" };
        d['L'] = new[] { "X....", "X....", "X....", "X....", "X....", "X....", "XXXXX" };
        d['M'] = new[] { "X...X", "XX.XX", "X.X.X", "X.X.X", "X...X", "X...X", "X...X" };
        d['N'] = new[] { "X...X", "XX..X", "X.X.X", "X.X.X", "X..XX", "X...X", "X...X" };
        d['O'] = new[] { ".XXX.", "X...X", "X...X", "X...X", "X...X", "X...X", ".XXX." };
        d['P'] = new[] { "XXXX.", "X...X", "X...X", "XXXX.", "X....", "X....", "X...." };
        d['Q'] = new[] { ".XXX.", "X...X", "X...X", "X...X", "X.X.X", "X..X.", ".XX.X" };
        d['R'] = new[] { "XXXX.", "X...X", "X...X", "XXXX.", "X.X..", "X..X.", "X...X" };
        d['S'] = new[] { ".XXXX", "X....", "X....", ".XXX.", "....X", "....X", "XXXX." };
        d['T'] = new[] { "XXXXX", "..X..", "..X..", "..X..", "..X..", "..X..", "..X.." };
        d['U'] = new[] { "X...X", "X...X", "X...X", "X...X", "X...X", "X...X", ".XXX." };
        d['V'] = new[] { "X...X", "X...X", "X...X", "X...X", "X...X", ".X.X.", "..X.." };
        d['W'] = new[] { "X...X", "X...X", "X...X", "X.X.X", "X.X.X", "XX.XX", "X...X" };
        d['X'] = new[] { "X...X", "X...X", ".X.X.", "..X..", ".X.X.", "X...X", "X...X" };
        d['Y'] = new[] { "X...X", "X...X", ".X.X.", "..X..", "..X..", "..X..", "..X.." };
        d['Z'] = new[] { "XXXXX", "....X", "...X.", "..X..", ".X...", "X....", "XXXXX" };
        d['0'] = new[] { ".XXX.", "X...X", "X..XX", "X.X.X", "XX..X", "X...X", ".XXX." };
        d['1'] = new[] { "..X..", ".XX..", "..X..", "..X..", "..X..", "..X..", "XXXXX" };
        d['2'] = new[] { ".XXX.", "X...X", "....X", "...X.", "..X..", ".X...", "XXXXX" };
        d['3'] = new[] { "XXXX.", "....X", "....X", ".XXX.", "....X", "....X", "XXXX." };
        d['4'] = new[] { "...X.", "..XX.", ".X.X.", "X..X.", "XXXXX", "...X.", "...X." };
        d['5'] = new[] { "XXXXX", "X....", "X....", "XXXX.", "....X", "....X", "XXXX." };
        d['6'] = new[] { ".XXX.", "X....", "X....", "XXXX.", "X...X", "X...X", ".XXX." };
        d['7'] = new[] { "XXXXX", "....X", "...X.", "..X..", ".X...", ".X...", ".X..." };
        d['8'] = new[] { ".XXX.", "X...X", "X...X", ".XXX.", "X...X", "X...X", ".XXX." };
        d['9'] = new[] { ".XXX.", "X...X", "X...X", ".XXXX", "....X", "....X", ".XXX." };
        d['?'] = new[] { ".XXX.", "X...X", "....X", "...X.", "..X..", ".....", "..X.." };
        d['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..X.." };
        d[','] = new[] { ".....", ".....", ".....", ".....", ".....", "..X..", ".X..." };
        d['!'] = new[] { "..X..", "..X..", "..X..", "..X..", "..X..", ".....", "..X.." };
        d[':'] = new[] { ".....", "..X..", "..X..", ".....", "..X..", "..X..", "....." };
        d['-'] = new[] { ".....", ".....", ".....", "XXXXX", ".....", ".....", "....." };
        d['/'] = new[] { "....X", "....X", "...X.", "..X..", ".X...", "X....", "X...." };
        d['%'] = new[] { "XX..X", "XX.X.", "..X..", ".X...", "X.XX.", "...XX", "...XX" };
        d['<'] = new[] { "...X.", "..X..", ".X...", "X....", ".X...", "..X..", "...X." };
        d['>'] = new[] { ".X...", "..X..", "...X.", "....X", "...X.", "..X..", ".X..." };
        d[' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." };
        return d;
    }
}
