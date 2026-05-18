using System;
using System.Collections.Generic;
using System.Linq;

// FlappyBrain Autopilot Simulation Test
bool timingMode = args.Contains("--timing");
var flapTimestamps = new List<float>();
float globalTime = 0f;
// Pure-physics simulation of FlappyBrainGameV2 — no MonoGame dependency.
// Matches game constants and pipe pre-generation exactly.

const float GRAVITY      = 0.40f;
const float FLAP         = -8.5f;
const float TERMINAL     = 14f;
const float BIRD_W       = 40f;
const float BIRD_H       = 36f;
const float HITBOX_INSET = 4f;
const float PIPE_W       = 90f;
const float LOG_W        = 800f;
const float LOG_H        = 600f;

const int   TOTAL_SECTIONS      = 20;
const float SECTION_DURATION    = 10f;
const int   SECTION_BONUS       = 5;
const int   SEED                = 20260509;

const int   MAX_RETRIES_PER_SECTION = 3;

const float DT = 1f / 60f;

static float SectionSpeed(int s)    => 160f + s * 16f;
static float SectionGap(int s)      => MathF.Max(100f, 210f - s * 5.5f);
static float SectionSpawnInt(int s) => MathF.Max(0.8f, 2.3f - s * 0.075f);

// ----- Pre-generate pipe layout (matches GameV2.PreGenerateSectionLayout) -----
var rng = new Random(SEED);
var sectionPipes = new float[TOTAL_SECTIONS][];
for (int s = 0; s < TOTAL_SECTIONS; s++)
{
    float spawnInt = SectionSpawnInt(s);
    int count = (int)MathF.Ceiling(SECTION_DURATION / spawnInt) + 2;
    var arr = new float[count];
    float gap = SectionGap(s);
    float minY = gap / 2f + 60f;
    float maxY = LOG_H - gap / 2f - 60f;
    for (int i = 0; i < count; i++)
    {
        arr[i] = minY + (float)rng.NextDouble() * (maxY - minY);
    }
    sectionPipes[s] = arr;
}

// ----- Per-section results -----
var results = new List<SectionResult>();
int totalScore = 0;
int totalCollisions = 0;
int totalObstaclesPassed = 0;

Console.WriteLine("FlappyBrain Autopilot Simulation Test");
Console.WriteLine("======================================");
Console.WriteLine($"Seed: {SEED} | Sections: {TOTAL_SECTIONS} | Timestep: 60fps");
Console.WriteLine();

for (int section = 0; section < TOTAL_SECTIONS; section++)
{
    float sectionStart = globalTime;
    var result = SimulateSection(section, sectionPipes[section], flapTimestamps, sectionStart);
    globalTime += SECTION_DURATION + 3f; // 3s transition
    results.Add(result);
    totalScore += result.ScoreEarned;
    totalCollisions += result.Collisions;
    totalObstaclesPassed += result.ObstaclesPassed;

    string status = result.Cleared ? "✅ CLEAR " : "❌ FAILED";
    string coll = result.Collisions == 1 ? "collision, " : "collisions,";
    Console.WriteLine(
        $"Section {section + 1,2}: {status} — {result.ObstaclesPassed,2} obstacles, " +
        $"{result.Collisions} {coll} score +{result.ScoreEarned,-2} (total: {totalScore})");
}

int sectionsCleared = results.Count(r => r.Cleared);
Console.WriteLine();
Console.WriteLine("======================================");
File.WriteAllLines("/tmp/flap-times.txt", flapTimestamps.Select(t => t.ToString("F3")));
Console.WriteLine($"RESULT: {sectionsCleared}/{TOTAL_SECTIONS} sections cleared");
Console.WriteLine($"Total score: {totalScore}");
Console.WriteLine($"Total collisions: {totalCollisions}");
Console.WriteLine($"Obstacles passed: {totalObstaclesPassed}");
Console.WriteLine();

if (sectionsCleared >= 15)
{
    Console.WriteLine("✅ PASS — autopilot navigated ≥15 sections successfully");
    return 0;
}
else
{
    Console.WriteLine("❌ FAIL — autopilot navigated <15 sections");
    return 1;
}

// ===== Simulation =====

SectionResult SimulateSection(int section, float[] pipeGapYs, List<float> flapLog, float sectionStart)
{
    int collisions = 0;
    int bestObstacles = 0;

    // Up to MAX_RETRIES_PER_SECTION + 1 attempts (initial + N retries)
    for (int attempt = 0; attempt <= MAX_RETRIES_PER_SECTION; attempt++)
    {
        var result = RunAttempt(section, pipeGapYs, flapLog, sectionStart);
        if (result.Survived)
        {
            return new SectionResult
            {
                Section = section,
                Cleared = true,
                ObstaclesPassed = result.ObstaclesPassed,
                Collisions = collisions,
                ScoreEarned = result.ObstaclesPassed + SECTION_BONUS,
            };
        }
        collisions++;
        if (result.ObstaclesPassed > bestObstacles) bestObstacles = result.ObstaclesPassed;
    }

    // Section failed after all retries — no section bonus, partial credit only.
    return new SectionResult
    {
        Section = section,
        Cleared = false,
        ObstaclesPassed = bestObstacles,
        Collisions = collisions,
        ScoreEarned = bestObstacles,
    };
}

AttemptResult RunAttempt(int section, float[] pipeGapYs, List<float> flapLog, float sectionStart)
{
    float speed = SectionSpeed(section);
    float spawnInt = SectionSpawnInt(section);
    float gapH = SectionGap(section);

    float birdX = 200f;
    float birdY = LOG_H / 2f;
    float birdVY = 0f;

    float sectionTimer = 0f;
    float spawnTimer = 0f;
    int pipeIndex = 0;

    var pipes = new List<SimPipe>();
    int obstaclesPassed = 0;

    // Run until: section_timer >= SECTION_DURATION AND no pipes remain on screen
    // (matches game's section-complete condition)
    float maxSimTime = SECTION_DURATION + 30f; // safety cap
    float t = 0f;

    while (t < maxSimTime)
    {
        // 1. Advance timers
        if (sectionTimer < SECTION_DURATION + 1f)
        {
            sectionTimer += DT;
            spawnTimer += DT;
        }

        // 2. Spawn pipes
        if (sectionTimer < SECTION_DURATION && spawnTimer >= spawnInt && pipeIndex < pipeGapYs.Length)
        {
            spawnTimer = 0f;
            pipes.Add(new SimPipe { X = LOG_W + 20f, GapY = pipeGapYs[pipeIndex], GapH = gapH });
            pipeIndex++;
        }

        // 3. Autopilot decision (BEFORE physics, like the game's input handling)
        bool flap = AutopilotShouldFlap(birdX, birdY, birdVY, pipes, gapH);

        // 4. Apply flap
        if (flap)
        {
            birdVY = FLAP;
            flapLog.Add(sectionStart + t);
        }

        // 5. Physics (matches game exactly: per-frame at fixed 60Hz)
        birdVY += GRAVITY;
        if (birdVY > TERMINAL) birdVY = TERMINAL;
        birdY += birdVY;

        // 6. World bounds death
        if (birdY < 0 || birdY > LOG_H)
        {
            return new AttemptResult { Survived = false, ObstaclesPassed = obstaclesPassed };
        }

        // 7. Move pipes
        foreach (var p in pipes) p.X -= speed * DT;

        // 8. Score & remove
        for (int i = pipes.Count - 1; i >= 0; i--)
        {
            var p = pipes[i];
            if (!p.Scored && p.X + PIPE_W < birdX)
            {
                p.Scored = true;
                obstaclesPassed++;
            }
            if (p.X + PIPE_W < -10) pipes.RemoveAt(i);
        }

        // 9. Collision check
        if (CheckCollision(birdX, birdY, pipes))
        {
            return new AttemptResult { Survived = false, ObstaclesPassed = obstaclesPassed };
        }

        // 10. Section complete?
        if (sectionTimer >= SECTION_DURATION && pipes.Count == 0 && pipeIndex >= pipeGapYs.Length)
        {
            return new AttemptResult { Survived = true, ObstaclesPassed = obstaclesPassed };
        }

        // Also: if all pipes are spawned and have all scrolled off, we're done early
        if (sectionTimer >= SECTION_DURATION && pipes.Count == 0)
        {
            return new AttemptResult { Survived = true, ObstaclesPassed = obstaclesPassed };
        }

        t += DT;
    }

    return new AttemptResult { Survived = true, ObstaclesPassed = obstaclesPassed };
}

bool AutopilotShouldFlap(float birdX, float birdY, float birdVY, List<SimPipe> pipes, float gapH)
{
    // Find the pipe currently overlapping the bird (or the next ahead if none).
    // While inside a pipe, the target MUST be that pipe's gap — switching to the
    // next pipe early can push us into the current pipe's wall.
    float birdLeft = birdX - BIRD_W / 2f + HITBOX_INSET;
    float birdRight = birdX + BIRD_W / 2f - HITBOX_INSET;

    // 1) Pipe currently overlapping bird's hitbox (X-axis)
    SimPipe? inside = null;
    foreach (var p in pipes)
    {
        float pL = p.X, pR = p.X + PIPE_W;
        if (pR > birdLeft && pL < birdRight)
        {
            if (inside == null || p.X < inside.X) inside = p;
        }
    }

    // 2) Otherwise, next pipe whose left edge is ahead of bird right edge
    SimPipe? next = null;
    if (inside == null)
    {
        float bestX = float.MaxValue;
        foreach (var p in pipes)
        {
            if (p.X >= birdRight && p.X < bestX)
            {
                next = p;
                bestX = p.X;
            }
        }
    }

    var primary = inside ?? next;
    float targetY = primary?.GapY ?? (LOG_H / 2f);
    float targetGapH = primary?.GapH ?? gapH;

    // Safe corridor: bird center must fit within gap minus its half-height.
    float corridor = targetGapH / 2f - BIRD_H / 2f - 4f;  // shrink slightly for margin
    float floorY = targetY + corridor;     // must stay ABOVE this
    float ceilY = targetY - corridor;      // must stay BELOW this (numerically)

    // Simulate "do nothing" for next 8 frames — would we breach the floor?
    float predY = birdY;
    float predV = birdVY;
    bool wouldFall = false;
    for (int i = 0; i < 8; i++)
    {
        predV += GRAVITY;
        if (predV > TERMINAL) predV = TERMINAL;
        predY += predV;
        if (predY > floorY) { wouldFall = true; break; }
    }

    // Simulate "flap now" — would we breach the ceiling within the next ~25 frames?
    // Flap power propels bird up ~85 px before momentum reverses.
    float flapPredY = birdY;
    float flapPredV = FLAP;
    float flapMinY = birdY;
    for (int i = 0; i < 25; i++)
    {
        flapPredV += GRAVITY;
        if (flapPredV > TERMINAL) flapPredV = TERMINAL;
        flapPredY += flapPredV;
        if (flapPredY < flapMinY) flapMinY = flapPredY;
    }
    bool flapWouldHitCeiling = flapMinY < ceilY;

    if (wouldFall && !flapWouldHitCeiling) return true;

    // Emergency override: if we're already below floor AND falling, flap regardless of ceiling.
    if (birdY > floorY && birdVY > -1f) return true;

    return false;
}

bool CheckCollision(float birdX, float birdY, List<SimPipe> pipes)
{
    float birdLeft   = birdX - BIRD_W / 2f + HITBOX_INSET;
    float birdRight  = birdX + BIRD_W / 2f - HITBOX_INSET;
    float birdTop    = birdY - BIRD_H / 2f + HITBOX_INSET;
    float birdBottom = birdY + BIRD_H / 2f - HITBOX_INSET;

    foreach (var p in pipes)
    {
        float pipeLeft  = p.X;
        float pipeRight = p.X + PIPE_W;

        // X overlap?
        if (birdRight <= pipeLeft || birdLeft >= pipeRight) continue;

        float topH = MathF.Max(0f, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;

        // Top pipe: rect (pipeLeft, 0, PIPE_W, topH)
        if (topH > 0 && birdTop < topH && birdBottom > 0)
            return true;

        // Bottom pipe: rect (pipeLeft, botY, PIPE_W, LOG_H - botY)
        if (birdBottom > botY && birdTop < LOG_H)
            return true;
    }
    return false;
}

// ===== Records =====

record struct AttemptResult
{
    public bool Survived;
    public int ObstaclesPassed;
}

record struct SectionResult
{
    public int Section;
    public bool Cleared;
    public int ObstaclesPassed;
    public int Collisions;
    public int ScoreEarned;
}

class SimPipe
{
    public float X;
    public float GapY;
    public float GapH;
    public bool Scored;
}
