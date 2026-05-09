using SkiaSharp;
using System.Globalization;

const int W = 800;
const int H = 600;
const int TOTAL_FRAMES = 900;
const int FPS = 30;
const string FRAMES_DIR = "/tmp/demo-t1-frames";
const string BIRD_PATH = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";

// Physics
const float GRAVITY = 0.40f;
const float FLAP = -8.5f;
const float TERMINAL = 14f;
const float BIRD_W = 40f;
const float BIRD_H = 36f;
const float HITBOX_INSET = 4f;
const float PIPE_W = 90f;

const float GROUND_Y = 520f;

// Section configs
const float S1_SCROLL = 2.8f;
const int S1_GAP = 210;
const float S2_SCROLL = 3.4f;
const int S2_GAP = 185;

// Frame ranges
const int TITLE_END = 60;          // 0-59
const int S1_START = 60;
const int S1_END = 119;            // brief S1 before crash
const int S1_BANNER_END = 120;     // no banner, go straight to crash
const int S2_START = 120;
const int COLLISION_FRAME = 120;
const int GORE_END = 195;          // 120-194 (75 frames)
const int RETRY_END = 225;         // 195-224 (30 frames)
const int S2_RESUME_END = 900;     // 675-899

Directory.CreateDirectory(FRAMES_DIR);

SKBitmap? birdBitmap = null;
if (File.Exists(BIRD_PATH))
{
    birdBitmap = SKBitmap.Decode(BIRD_PATH);
    Console.WriteLine($"Bird loaded: {birdBitmap?.Width}x{birdBitmap?.Height}");
}
else
{
    Console.WriteLine($"Bird asset missing at {BIRD_PATH}");
}

// Pre-seeded RNG values (seed 20260509). Generate GapY list upfront — never call rng during render.
var rng = new Random(20260509);
var gapYList = new List<float>();
for (int i = 0; i < 50; i++)
    gapYList.Add(150f + (float)rng.NextDouble() * 280f); // 150-430

// Section 1 pipes spawn at frames 80, 160, 240
var section1Pipes = new List<Pipe>
{
    new Pipe { SpawnFrame = 80,  GapY = gapYList[0] },
    new Pipe { SpawnFrame = 160, GapY = gapYList[1] },
    new Pipe { SpawnFrame = 240, GapY = gapYList[2] },
    new Pipe { SpawnFrame = -94, GapY = 150f }, // collision pipe: at X=200 frame 120
};

// Section 2 pipes spawn at frames 460, 528. Second pipe at X≈280 when frame=555 ensures collision target.
// Pipe X position at frame f = W - (f - spawnFrame) * S2_SCROLL
// At frame 555 with spawnFrame 528: X = 800 - 27*3.4 = 800 - 91.8 = 708.2  -> too far right
// We want X ≈ 280 at frame 555 for spawnFrame 528: 800 - X = 27 * scroll => scroll = (800-280)/27 = 19.26 (too high)
// Reinterpret: pipe progressing from right; the renderer X for pipe = W - (currentFrame - spawnFrame) * SCROLL.
// For pipe to be at X=280 at frame=555: (555-spawnFrame)*3.4 = 800-280 = 520 => 555-spawnFrame ≈ 153 => spawnFrame ≈ 402
// But spec says spawn at 460, 528. We honor spec — collision target is the SECOND pipe.
// At frame 555: pipe2 X = 800 - (555-528)*3.4 = 800 - 91.8 = 708.2. The bird (drifting right? no it's at fixed X=200).
// Bird is at fixed X. The pipes scroll past. Collision happens when pipe X aligns with bird X (200).
// pipe2 at X=200: 800 - (f-528)*3.4 = 200 => f-528 = 600/3.4 ≈ 176.5 => f ≈ 704. That's after collision.
// Reinterpret again: "second pipe at X≈280 when frame=555" — maybe collision is forced by override anyway.
// At frame 570 collision: pipe1 X = 800 - (570-460)*3.4 = 800 - 374 = 426. pipe2 X = 800 - (570-528)*3.4 = 800 - 142.8 = 657.2.
// Neither aligned with bird X=200. So the OVERRIDE drives the bird DOWN to crash into ground/pipe.
// Actually the override drives bird down — so collision is with GROUND or top/bottom pipe segment.
// We'll let the override naturally cause a ground collision around frame 570.

var section2Pipes = new List<Pipe>
{
    new Pipe { SpawnFrame = 460, GapY = gapYList[3] },
    new Pipe { SpawnFrame = 528, GapY = 380f }, // lower gap so override-down hits bottom pipe / ground area
    
};

// Bird state
float birdX = 200f;
float birdY = 280f;
float birdVY = 0f;
float lastBirdX = birdX;
float lastBirdY = birdY;
int score = 0;
bool collided = false;

// Trees: 4 gum tree silhouettes at parallax 0.2x
var trees = new List<Tree>();
for (int i = 0; i < 6; i++)
{
    trees.Add(new Tree
    {
        X = i * 220f - 50f,
        Lean = (float)(rng.NextDouble() * 0.15 - 0.075),
        Heights = new[] { 38f + (float)rng.NextDouble() * 10, 32f + (float)rng.NextDouble() * 12, 42f + (float)rng.NextDouble() * 8 },
    });
}

// Dust particles
var dust = new List<Dust>();
for (int i = 0; i < 80; i++)
{
    dust.Add(new Dust
    {
        X = (float)rng.NextDouble() * W,
        Y = 100f + (float)rng.NextDouble() * 380f,
        Speed = 1f + (float)rng.NextDouble() * 2f,
        Size = 2f + (float)rng.NextDouble() * 2f,
    });
}

// Gore particles (initialised at collision)
var gore = new List<GoreParticle>();
var stains = new List<(float X, float Size)>();

// Background scroll offset (for ground/trees)
float bgScroll = 0f;
float groundScroll = 0f;

for (int frame = 0; frame < TOTAL_FRAMES; frame++)
{
    using var surface = SKSurface.Create(new SKImageInfo(W, H));
    var canvas = surface.Canvas;

    // ── PHYSICS PHASE ──────────────────────────────────────────────
    bool inGameplay = frame >= S1_START && frame < COLLISION_FRAME
                      || frame >= S2_RESUME_START();
    bool inS1Banner = frame >= S1_END && frame < S1_BANNER_END;
    bool isTitle = frame < TITLE_END;
    bool inGore = frame >= COLLISION_FRAME && frame < GORE_END;
    bool inRetry = frame >= GORE_END && frame < RETRY_END;

    // Determine current scroll & active pipes
    float scroll = 0f;
    List<Pipe> activePipes = new();
    if (frame <= COLLISION_FRAME)
    {
        scroll = S1_SCROLL;
        activePipes = section1Pipes;
    }
    else if (inS1Banner)
    {
        scroll = 0f; // pause scrolling (no pipes)
        activePipes = new List<Pipe>();
    }
    else if (frame >= 100 && frame < COLLISION_FRAME)
    {
        scroll = S2_SCROLL;
        activePipes = section2Pipes;
    }
    else if (inGore || inRetry)
    {
        scroll = 0f;
        activePipes = new List<Pipe>();
    }
    else // S2 resume
    {
        scroll = S2_SCROLL;
        // Fresh section 2 pipes for the resume — generate from gapYList offsets
        activePipes = BuildResumePipes(frame, gapYList);
    }

    bgScroll += scroll;
    groundScroll += scroll;

    // Autopilot — only when bird is alive and we're in gameplay (not title, banner, retry)
    if (!collided && (frame < S1_END || (frame >= S2_START && frame < COLLISION_FRAME) || frame >= S2_RESUME_START()))
    {
        // Find next pipe (whose right edge is past birdX)
        Pipe? nextPipe = null;
        float nextPipeDist = float.MaxValue;
        foreach (var p in activePipes)
        {
            float px = ComputePipeX(p, frame, scroll, activePipes == section1Pipes ? S1_START : (activePipes == section2Pipes ? S2_START : 675));
            if (px + PIPE_W > birdX - 20)
            {
                float dist = px - birdX;
                if (dist < nextPipeDist) { nextPipeDist = dist; nextPipe = p; }
            }
        }

        // Override frames 480-509: steer DOWN hard so bird crashes into collision pipe
        if (frame >= 100 && frame < COLLISION_FRAME)
        {
            birdVY = Math.Min(birdVY + 6f, 14f);
        }
        else if (nextPipe != null)
        {
            float gapCenter = nextPipe.GapY;
            // Predict birdY in 12 frames
            float predicted = birdY;
            float vyPred = birdVY;
            for (int k = 0; k < 12; k++)
            {
                vyPred += GRAVITY;
                if (vyPred > TERMINAL) vyPred = TERMINAL;
                predicted += vyPred;
            }
            if (predicted > gapCenter + 40f && birdVY > -2f)
            {
                birdVY = FLAP;
            }
        }
        else if (inS1Banner)
        {
            // No pipes — gentle drift
            // Fly toward middle
            if (birdY > 300f && birdVY > -1f) birdVY = FLAP * 0.5f;
        }
    }

    // Apply physics to bird (alive)
    if (!collided)
    {
        birdVY += GRAVITY;
        if (birdVY > TERMINAL) birdVY = TERMINAL;
        if (birdVY < -TERMINAL) birdVY = -TERMINAL;
        birdY += birdVY;

        // Clamp to top
        if (birdY < 30) { birdY = 30; birdVY = 0; }
        // During override approach, clamp Y so bird stays above ground until pipe-intersection collision
    }

    // Pipe-intersection collision check at COLLISION_FRAME — bird hitbox vs collision pipe top
    if (!collided && frame == COLLISION_FRAME)
    {
        float bx0 = birdX - BIRD_W / 2 + HITBOX_INSET;
        float bx1 = birdX + BIRD_W / 2 - HITBOX_INSET;
        float by0 = birdY - BIRD_H / 2 + HITBOX_INSET;
        float by1 = birdY + BIRD_H / 2 - HITBOX_INSET;
        bool hitPipe = false;
        foreach (var p in activePipes)
        {
            float cScroll = (activePipes == section1Pipes) ? S1_SCROLL : S2_SCROLL;
            float cStart = (activePipes == section1Pipes) ? (float)S1_START : (float)S2_START;
            float px = ComputePipeX(p, frame, cScroll, cStart);
            if (px + PIPE_W < bx0 || px > bx1) continue;
            int curGap = (activePipes == section1Pipes) ? S1_GAP : S2_GAP;
            float halfGap = curGap / 2f;
            float topH = p.GapY - halfGap;
            float botY = p.GapY + halfGap;
            if (by0 < topH || by1 > botY)
            {
                hitPipe = true;
                break;
            }
        }
        // Fire collision either way — pipe intersection confirmed or fallback
        collided = true;
        lastBirdX = birdX;
        lastBirdY = birdY;
        SpawnGore(gore, lastBirdX, lastBirdY, rng);
        // 3 pre-placed impact stains, extra large
        stains.Add((birdX - 20f, 30f + (float)rng.NextDouble() * 20f));
        stains.Add((birdX,        35f + (float)rng.NextDouble() * 15f));
        stains.Add((birdX + 20f, 30f + (float)rng.NextDouble() * 20f));
        Console.WriteLine($"Collision @ frame {frame}: birdY={birdY:F1}, hitPipe={hitPipe}");
    }

    // Update gore particles
    if (inGore)
    {
        foreach (var g in gore)
        {
            // Convert per-second to per-frame: divide by 30
            g.VY += g.Gravity / 30f;
            g.X += g.VX / 30f;
            g.Y += g.VY / 30f;
            g.Rotation += g.RotSpeed / 30f;
            g.Life++;

            // Blood blob hits ground -> stain
            if (g.Type == GoreType.BloodBlob && g.Y > GROUND_Y && !g.Stained)
            {
                g.Stained = true;
                stains.Add((g.X, 15f + (float)rng.NextDouble() * 25f));
            }
        }
    }

    // Section/score progression
    if (frame == S1_END) score = 5;
    if (frame == 460) score = 5; // entering section 2

    // Reset bird at start of S2 attempt
    if (frame == S2_START)
    {
        birdY = 280f;
        birdVY = 0f;
    }

    // Clear stains at frame 675 (per spec)
    if (frame == RETRY_END)
    {
        stains.Clear();
        // Reset bird for resume
        collided = false;
        birdX = 200f;
        birdY = 280f;
        birdVY = 0f;
    }

    // ── RENDER PHASE ───────────────────────────────────────────────
    canvas.Clear(SKColors.Black);

    // Screen shake (gore phase)
    canvas.Save();
    if (inGore)
    {
        int gf = frame - COLLISION_FRAME;
        float amp = 28f * Math.Max(0f, 1f - gf / 75f);
        float sx = (float)Math.Sin(gf * 0.8) * amp;
        float sy = (float)Math.Cos(gf * 1.1) * amp;
        canvas.Translate(sx, sy);
    }

    // Sky gradient (3 stops): #1C3A6E -> #5B9BD5 -> #F0A832
    DrawSkyGradient(canvas);

    // Background trees parallax 0.2x
    DrawTrees(canvas, trees, bgScroll * 0.2f);

    // Dust drifts left
    DrawDust(canvas, dust, scroll);

    // Pipes
    foreach (var p in activePipes)
    {
        float startFrame = activePipes == section1Pipes ? S1_START :
                           activePipes == section2Pipes ? S2_START : 675;
        float pipeX = ComputePipeX(p, frame, scroll, startFrame);
        if (pipeX > -PIPE_W && pipeX < W)
            DrawPipe(canvas, pipeX, p.GapY, S1_GAP_OR_S2(frame));
    }

    // Ground
    DrawGround(canvas, groundScroll);

    // Stains on ground
    DrawStains(canvas, stains);

    // Bird (only if alive OR draw nothing during gore - bird is "splatted")
    if (!collided)
    {
        DrawBird(canvas, birdBitmap, birdX, birdY, birdVY);
    }

    // Gore particles
    if (inGore)
    {
        DrawGore(canvas, gore);
    }

    // Red flash — extended duration, brighter initial alpha
    if (frame >= COLLISION_FRAME && frame < COLLISION_FRAME + 18)
    {
        int gf = frame - COLLISION_FRAME;
        float a = 0.85f * (1f - gf / 18f);
        using var p = new SKPaint { Color = new SKColor(255, 0, 0, (byte)(a * 255)) };
        canvas.DrawRect(0, 0, W, H, p);
    }

    canvas.Restore();

    // ── HUD / OVERLAYS (no shake) ──────────────────────────────────
    if (isTitle)
    {
        DrawTitleCard(canvas);
    }
    else if (inS1Banner)
    {
        DrawSectionCompleteBanner(canvas, frame);
    }
    else if (inRetry)
    {
        DrawRetryScreen(canvas);
    }

    // SPLAT text — appears 10 frames after collision through end of gore
    if (frame >= COLLISION_FRAME + 10 && frame < GORE_END)
    {
        DrawSplatText(canvas, frame);
    }

    // HUD - section/score (visible during gameplay, not title/retry)
    if (!isTitle && !inRetry)
    {
        DrawHud(canvas, frame, score);
    }

    // Save frame
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    using var fs = File.OpenWrite(Path.Combine(FRAMES_DIR, $"frame_{frame:D4}.png"));
    data.SaveTo(fs);

    if (frame % 50 == 0) Console.WriteLine($"frame {frame}/{TOTAL_FRAMES}");
}

Console.WriteLine($"Rendered {TOTAL_FRAMES} frames to {FRAMES_DIR}");

// ──────────────────────────────────────────────────────────────────
// HELPERS
// ──────────────────────────────────────────────────────────────────

static int S2_RESUME_START() => 675;

static int S1_GAP_OR_S2(int frame) => frame < 360 ? 210 : 185;

static float ComputePipeX(Pipe p, int frame, float scroll, float startFrame)
{
    return W - (frame - p.SpawnFrame) * scroll;
}

static List<Pipe> BuildResumePipes(int frame, List<float> gapYList)
{
    // Pipes spawned every 68 frames starting at 690
    var pipes = new List<Pipe>();
    int idx = 5;
    for (int f = 690; f < 900; f += 68)
    {
        pipes.Add(new Pipe { SpawnFrame = f, GapY = gapYList[idx++] });
    }
    return pipes;
}

static void DrawSkyGradient(SKCanvas canvas)
{
    // 3 stops: 0=#1C3A6E, 0.55=#5B9BD5, 1=#F0A832
    SKColor c1 = new SKColor(0x1C, 0x3A, 0x6E);
    SKColor c2 = new SKColor(0x5B, 0x9B, 0xD5);
    SKColor c3 = new SKColor(0xF0, 0xA8, 0x32);
    using var paint = new SKPaint();
    int rows = 600;
    for (int y = 0; y < rows; y++)
    {
        float t = y / (float)(rows - 1);
        SKColor c;
        if (t < 0.55f) c = LerpColor(c1, c2, t / 0.55f);
        else c = LerpColor(c2, c3, (t - 0.55f) / 0.45f);
        paint.Color = c;
        canvas.DrawRect(0, y, W, 1, paint);
    }
}

static SKColor LerpColor(SKColor a, SKColor b, float t)
{
    if (t < 0) t = 0; if (t > 1) t = 1;
    return new SKColor(
        (byte)(a.Red + (b.Red - a.Red) * t),
        (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t),
        (byte)(a.Alpha + (b.Alpha - a.Alpha) * t)
    );
}

static void DrawGround(SKCanvas canvas, float scroll)
{
    SKColor top = new SKColor(0xC2, 0x50, 0x1F);
    SKColor bot = new SKColor(0x8B, 0x30, 0x10);
    using var p = new SKPaint();
    int rows = 80;
    for (int y = 0; y < rows; y++)
    {
        float t = y / (float)(rows - 1);
        p.Color = LerpColor(top, bot, t);
        canvas.DrawRect(0, 520 + y, W, 1, p);
    }
    // Texture lines that scroll
    using var line = new SKPaint { Color = new SKColor(0x6A, 0x28, 0x10), StrokeWidth = 1 };
    float off = -(scroll % 20f);
    for (float x = off; x < W; x += 20f)
    {
        canvas.DrawLine(x, 528, x + 8, 532, line);
        canvas.DrawLine(x + 4, 540, x + 14, 545, line);
    }
}

static void DrawTrees(SKCanvas canvas, List<Tree> trees, float scroll)
{
    using var trunk = new SKPaint { Color = new SKColor(0x4A, 0x30, 0x10), IsAntialias = true };
    using var leaf = new SKPaint { Color = new SKColor(0x1A, 0x2A, 0x0A), IsAntialias = true };
    foreach (var t in trees)
    {
        // wrap so trees recycle
        float x = ((t.X - scroll) % (W + 200) + W + 200) % (W + 200) - 100;
        // trunk leans
        canvas.Save();
        canvas.Translate(x, 520);
        canvas.RotateRadians(t.Lean);
        canvas.DrawRect(-4, -120, 8, 120, trunk);
        // 3 leaf clusters at top
        float cy = -120;
        canvas.DrawOval(0, cy - 10, t.Heights[0] / 2 + 6, t.Heights[0] / 2, leaf);
        canvas.DrawOval(-15, cy - 25, t.Heights[1] / 2 + 4, t.Heights[1] / 2 - 2, leaf);
        canvas.DrawOval(15, cy - 20, t.Heights[2] / 2 + 4, t.Heights[2] / 2, leaf);
        canvas.Restore();
    }
}

static void DrawDust(SKCanvas canvas, List<Dust> dust, float scroll)
{
    using var p = new SKPaint { Color = new SKColor(0xD4, 0x95, 0x6A, 180), IsAntialias = true };
    foreach (var d in dust)
    {
        d.X -= d.Speed;
        if (d.X < -10) d.X = W + 10;
        canvas.DrawCircle(d.X, d.Y, d.Size, p);
    }
}

static void DrawPipe(SKCanvas canvas, float x, float gapY, int gap)
{
    SKColor body = new SKColor(0x8B, 0x5E, 0x3C);
    SKColor stripe = new SKColor(0xA0, 0x70, 0x50);
    SKColor cap = new SKColor(0x6A, 0x40, 0x20);
    float halfGap = gap / 2f;
    float topH = gapY - halfGap;
    float botY = gapY + halfGap;
    float botH = 520 - botY;

    using var bp = new SKPaint { Color = body, IsAntialias = false };
    using var sp = new SKPaint { Color = stripe, IsAntialias = false };
    using var cp = new SKPaint { Color = cap, IsAntialias = false };

    // Top pipe
    if (topH > 0)
    {
        canvas.DrawRect(x, 0, PIPE_W, topH, bp);
        for (float yy = 0; yy < topH; yy += 15)
            canvas.DrawRect(x + 4, yy, PIPE_W - 8, 2, sp);
        canvas.DrawRect(x - 10, topH - 20, PIPE_W + 20, 20, cp);
    }
    // Bottom pipe
    if (botH > 0)
    {
        canvas.DrawRect(x, botY, PIPE_W, botH, bp);
        for (float yy = botY; yy < 520; yy += 15)
            canvas.DrawRect(x + 4, yy, PIPE_W - 8, 2, sp);
        canvas.DrawRect(x - 10, botY, PIPE_W + 20, 20, cp);
    }
}

static void DrawBird(SKCanvas canvas, SKBitmap? bird, float x, float y, float vy)
{
    canvas.Save();
    canvas.Translate(x, y);
    float angle = Math.Clamp(vy * 3.5f, -25f, 70f);
    canvas.RotateDegrees(angle);
    if (bird != null)
    {
        var dest = new SKRect(-80, -65, 80, 65);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(bird, dest, paint);
    }
    else
    {
        using var p = new SKPaint { Color = new SKColor(0xE0, 0xC0, 0x80), IsAntialias = true };
        canvas.DrawCircle(0, 0, 24, p);
    }
    canvas.Restore();
}

static void SpawnGore(List<GoreParticle> gore, float x, float y, Random rng)
{
    void add(int count, GoreType type, SKColor color, float minSize, float maxSize, float minSpeed, float maxSpeed, float gravity, bool rotates)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            float speed = minSpeed + (float)rng.NextDouble() * (maxSpeed - minSpeed);
            gore.Add(new GoreParticle
            {
                X = x + (float)(rng.NextDouble() * 16 - 8),
                Y = y + (float)(rng.NextDouble() * 16 - 8),
                VX = (float)(Math.Cos(angle) * speed),
                VY = (float)(Math.Sin(angle) * speed) - speed * 0.3f, // bias upward
                Size = minSize + (float)rng.NextDouble() * (maxSize - minSize),
                Color = color,
                Gravity = gravity,
                Type = type,
                Rotation = (float)(rng.NextDouble() * Math.PI * 2),
                RotSpeed = rotates ? (float)(rng.NextDouble() * 8 - 4) : 0,
            });
        }
    }
    add(70, GoreType.BloodBlob, new SKColor(0xCC, 0x00, 0x10), 10, 28, 120, 400, 280, false);
    add(45, GoreType.FurChunk, new SKColor(0x8B, 0x60, 0x40), 12, 25, 80, 300, 200, true);
    add(30, GoreType.Feather, new SKColor(0xD4, 0xC0, 0x90), 6, 22, 40, 180, 80, true);
    add(20, GoreType.Bone,    new SKColor(0xF0, 0xE8, 0xD0), 5, 18, 200, 500, 350, true);
    add(65, GoreType.BloodSpray, new SKColor(0xFF, 0x10, 0x20, (byte)(0.7f * 255)), 2, 7, 250, 650, 180, false);
    // Visceral chunks — large dark purple-red blobs, slow heavy "guts"
    add(20, GoreType.BloodBlob, new SKColor(0x6A, 0x00, 0x08), 18, 35, 60, 200, 180, true);
}

static void DrawGore(SKCanvas canvas, List<GoreParticle> gore)
{
    foreach (var g in gore)
    {
        if (g.Y > 600) continue;
        using var p = new SKPaint { Color = g.Color, IsAntialias = true };
        canvas.Save();
        canvas.Translate(g.X, g.Y);
        if (g.RotSpeed != 0) canvas.RotateRadians(g.Rotation);
        switch (g.Type)
        {
            case GoreType.BloodBlob:
                canvas.DrawCircle(0, 0, g.Size / 2, p);
                break;
            case GoreType.FurChunk:
                canvas.DrawOval(0, 0, g.Size / 2, g.Size / 3, p);
                break;
            case GoreType.Feather:
                canvas.DrawOval(0, 0, 2.5f, 9f, p);
                break;
            case GoreType.Bone:
                canvas.DrawRect(-2, -7, 4, 14, p);
                break;
            case GoreType.BloodSpray:
                canvas.DrawCircle(0, 0, g.Size / 2, p);
                break;
        }
        canvas.Restore();
    }
}

static void DrawStains(SKCanvas canvas, List<(float X, float Size)> stains)
{
    using var p = new SKPaint { Color = new SKColor(0x88, 0x00, 0x08, (byte)(0.6f * 255)), IsAntialias = true };
    foreach (var s in stains)
    {
        canvas.DrawCircle(s.X, 535, s.Size, p);
    }
}

static void DrawTitleCard(SKCanvas canvas)
{
    using var overlay = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(0.6f * 255)) };
    canvas.DrawRect(0, 0, W, H, overlay);

    using var titleFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 64);
    using var subFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans"), 32);

    using var titlePaint = new SKPaint { Color = new SKColor(0xF0, 0xA8, 0x32), IsAntialias = true };
    using var subPaint = new SKPaint { Color = new SKColor(0xF2, 0xD5, 0xA0), IsAntialias = true };
    using var shadow = new SKPaint { Color = SKColors.Black, IsAntialias = true };

    DrawCenteredText(canvas, "OUTBACK CLASSIC", W / 2 + 3, 250 + 3, titleFont, shadow);
    DrawCenteredText(canvas, "OUTBACK CLASSIC", W / 2, 250, titleFont, titlePaint);
    DrawCenteredText(canvas, "FlappyBrain", W / 2, 330, subFont, subPaint);
}

static void DrawSectionCompleteBanner(SKCanvas canvas, int frame)
{
    using var box = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(0.7f * 255)) };
    canvas.DrawRoundRect(W / 2 - 250, 230, 500, 110, 12, 12, box);

    using var titleFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 40);
    using var subFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 28);
    using var titlePaint = new SKPaint { Color = new SKColor(0x40, 0xFF, 0x80), IsAntialias = true };
    using var subPaint = new SKPaint { Color = new SKColor(0xFF, 0xD0, 0x40), IsAntialias = true };

    DrawCenteredText(canvas, "✅ SECTION 1 COMPLETE", W / 2, 280, titleFont, titlePaint);
    DrawCenteredText(canvas, "+5 POINTS", W / 2, 320, subFont, subPaint);
}

static void DrawRetryScreen(SKCanvas canvas)
{
    using var overlay = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(0.8f * 255)) };
    canvas.DrawRect(0, 0, W, H, overlay);

    using var titleFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 36);
    using var subFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans"), 24);
    using var titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
    using var subPaint = new SKPaint { Color = new SKColor(0x88, 0x88, 0x88), IsAntialias = true };

    DrawCenteredText(canvas, "RETRYING SECTION 2", W / 2, 290, titleFont, titlePaint);
    DrawCenteredText(canvas, "Attempt 2", W / 2, 330, subFont, subPaint);
}

static void DrawSplatText(SKCanvas canvas, int frame)
{
    using var font = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 96);
    using var outline = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 7 };
    using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
    using var fill = new SKPaint { Color = new SKColor(0xFF, 0x20, 0x20), IsAntialias = true };

    string text = "💀 SPLAT!";
    // White outline 4px offset — drawn first behind everything
    DrawCenteredText(canvas, text, W / 2 + 4, 270 + 4, font, white);
    DrawCenteredText(canvas, text, W / 2 - 4, 270 - 4, font, white);
    DrawCenteredText(canvas, text, W / 2, 270, font, outline);
    DrawCenteredText(canvas, text, W / 2, 270, font, fill);
}

static void DrawHud(SKCanvas canvas, int frame, int score)
{
    int section = frame < 450 ? 1 : 2;

    using var sectionFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 20);
    using var scoreFont = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), 26);
    using var shadow = new SKPaint { Color = SKColors.Black, IsAntialias = true };
    using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
    using var amber = new SKPaint { Color = new SKColor(0xF2, 0xD5, 0xA0), IsAntialias = true };

    string secText = $"SECTION: {section}/20";
    canvas.DrawText(secText, 22, 32, sectionFont, shadow);
    canvas.DrawText(secText, 20, 30, sectionFont, white);

    string scText = $"SCORE: {score}";
    canvas.DrawText(scText, 22, 64, scoreFont, shadow);
    canvas.DrawText(scText, 20, 62, scoreFont, amber);
}

static void DrawCenteredText(SKCanvas canvas, string text, float cx, float cy, SKFont font, SKPaint paint)
{
    using var measurePaint = new SKPaint(font);
    var width = measurePaint.MeasureText(text);
    canvas.DrawText(text, cx - width / 2, cy, font, paint);
}

class Pipe
{
    public int SpawnFrame;
    public float GapY;
}

class Tree
{
    public float X;
    public float Lean;
    public float[] Heights = new float[3];
}

class Dust
{
    public float X;
    public float Y;
    public float Speed;
    public float Size;
}

enum GoreType { BloodBlob, FurChunk, Feather, Bone, BloodSpray }

class GoreParticle
{
    public float X, Y, VX, VY, Size, Gravity, Rotation, RotSpeed;
    public SKColor Color;
    public GoreType Type;
    public int Life;
    public bool Stained;
}
