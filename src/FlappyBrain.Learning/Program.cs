using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ===== Game constants (must match FlappyBrainGameV2.cs exactly) =====
const float GRAVITY      = 0.40f;
const float FLAP         = -8.5f;
const float TERMINAL     = 14f;
const float BIRD_W       = 40f;
const float BIRD_H       = 36f;
const float HITBOX_INSET = 4f;
const float PIPE_W       = 90f;
const float LOG_W        = 800f;
const float LOG_H        = 600f;

const int   TOTAL_SECTIONS   = 20;
const float SECTION_DURATION = 10f;
const int   SECTION_BONUS    = 5;
const int   SEED             = 20260509;
const float DT = 1f / 60f;

static float SectionSpeed(int s)    => 160f + s * 16f;
static float SectionGap(int s)      => MathF.Max(100f, 210f - s * 5.5f);
static float SectionSpawnInt(int s) => MathF.Max(0.8f, 2.3f - s * 0.075f);

// ===== State discretization =====
const int BIRD_Y_BINS    = 20; // 30px each
const int BIRD_VEL_BINS  = 16;
const int GAP_CTR_BINS   = 15; // 40px each
const int DIST_BINS      = 10; // 80px each

const int STATE_COUNT  = BIRD_Y_BINS * BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS; // 48000
const int ACTION_COUNT = 2;

static int BinBirdY(float y)     => Math.Clamp((int)(y / 30f), 0, BIRD_Y_BINS - 1);
static int BinBirdVel(float v)   => Math.Clamp((int)((v + 9f) / 1.5f), 0, BIRD_VEL_BINS - 1);
static int BinGapCenter(float g) => Math.Clamp((int)(g / 40f), 0, GAP_CTR_BINS - 1);
static int BinDist(float d)      => Math.Clamp((int)(d / 80f), 0, DIST_BINS - 1);

static int StateIndex(float y, float vel, float gapC, float dist) =>
    BinBirdY(y) * (BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS) +
    BinBirdVel(vel) * (GAP_CTR_BINS * DIST_BINS) +
    BinGapCenter(gapC) * DIST_BINS +
    BinDist(dist);

// ===== Q-table & hyperparameters =====
var Q = new float[STATE_COUNT, ACTION_COUNT];

const float ALPHA         = 0.2f;
const float GAMMA          = 0.95f;
const float EPSILON_START  = 0.5f;
const float EPSILON_END    = 0.01f;
const int   EPISODES       = 300_000;
const float EPSILON_DECAY  = (EPSILON_START - EPSILON_END) / (EPISODES * 0.7f);

const float REWARD_PIPE_CLEAR    =  20f;
const float REWARD_SECTION_CLEAR = 100f;
const float REWARD_DEATH         = -50f;
const float REWARD_ALIVE         =  0.2f;

// ===== Pre-generate pipe layout (matches the game and SimTest) =====
var layoutRng = new Random(SEED);
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
        arr[i] = minY + (float)layoutRng.NextDouble() * (maxY - minY);
    }
    sectionPipes[s] = arr;
}

// ===== Visit counter for top-state inspection =====
var visitCounts = new int[STATE_COUNT];

var rng = new Random(42);
float epsilon = EPSILON_START;
var recentScores = new Queue<int>();
int bestScore = 0;

Console.WriteLine($"Training {EPISODES} episodes...");
Console.WriteLine($"State space: {STATE_COUNT} states");
Console.WriteLine();

for (int ep = 0; ep < EPISODES; ep++)
{
    // Curriculum: gradually expand start-section range so the agent encounters
    // every section with non-trivial frequency, not just section 0.
    int startSection;
    if (ep < 30_000)
        startSection = 0;
    else if (ep < 80_000)
        startSection = rng.Next(0, 5);
    else if (ep < 160_000)
        startSection = rng.Next(0, 12);
    else
        startSection = rng.Next(0, TOTAL_SECTIONS);

    int score = RunEpisode(startSection, training: true);

    if (score > bestScore) bestScore = score;
    recentScores.Enqueue(score);
    if (recentScores.Count > 1000) recentScores.Dequeue();

    if (epsilon > EPSILON_END) epsilon -= EPSILON_DECAY;
    if (epsilon < EPSILON_END) epsilon = EPSILON_END;

    if ((ep + 1) % 5000 == 0)
    {
        double avg = recentScores.Average();
        Console.WriteLine($"Ep {ep + 1,6}: avg_score={avg:F1} best={bestScore} epsilon={epsilon:F2}");
    }
}

Console.WriteLine();
Console.WriteLine("Testing (deterministic, 1 run from section 0)...");
int detScore = RunEpisode(0, training: false);
Console.WriteLine($"Deterministic full-game score: {detScore}");

Console.WriteLine();
Console.WriteLine("Testing per-section (deterministic, 1 attempt each)...");
for (int s = 0; s < TOTAL_SECTIONS; s++)
{
    // Quick check: can the policy survive section s starting from default position?
    var (survived, scored, _, _) = RunSection(s, 200f, LOG_H / 2f, 0f, training: false);
    string status = survived ? "CLEAR" : "FAIL ";
    Console.WriteLine($"  Section {s + 1,2}: {status}  pipes_passed={scored}");
}

// ===== Save Q-table =====
string qPath = Path.Combine(AppContext.BaseDirectory, "qtable.bin");
string qSrcPath = "/home/paul/source/FlappyBrain/src/FlappyBrain.Learning/qtable.bin";
string qGamePath = "/home/paul/source/FlappyBrain/src/FlappyBrain.Game/qtable.bin";
SaveQTable(qPath);
SaveQTable(qSrcPath);
SaveQTable(qGamePath);
Console.WriteLine($"Q-table saved to {qSrcPath}");
Console.WriteLine($"Q-table saved to {qGamePath}");

// ===== Top-20 most-visited states =====
Console.WriteLine();
Console.WriteLine("Top 20 most-visited states (state_idx | birdY-vel-gapC-dist | best_action):");
var topStates = Enumerable.Range(0, STATE_COUNT)
    .Select(i => (idx: i, count: visitCounts[i]))
    .OrderByDescending(x => x.count)
    .Take(20);
foreach (var (idx, count) in topStates)
{
    int yBin = idx / (BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS);
    int rem  = idx % (BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS);
    int vBin = rem / (GAP_CTR_BINS * DIST_BINS);
    rem      = rem % (GAP_CTR_BINS * DIST_BINS);
    int gBin = rem / DIST_BINS;
    int dBin = rem % DIST_BINS;
    int action = Q[idx, 1] > Q[idx, 0] ? 1 : 0;
    string actStr = action == 1 ? "FLAP" : "wait";
    Console.WriteLine($"  visits={count,7} y={yBin,2} v={vBin,2} g={gBin,2} d={dBin,2}  Q0={Q[idx,0]:F2} Q1={Q[idx,1]:F2}  -> {actStr}");
}

return 0;

// ===== Episode runner =====
int RunEpisode(int startSection, bool training)
{
    float birdX = 200f;
    float birdY = LOG_H / 2f;
    float birdVY = 0f;

    int totalScore = 0;
    int section = startSection;

    while (section < TOTAL_SECTIONS)
    {
        var (survived, sectionPipesScored, finalY, finalVY) =
            RunSection(section, birdX, birdY, birdVY, training);

        totalScore += sectionPipesScored;

        if (!survived) break;

        // Section bonus (the +5 the game awards)
        totalScore += SECTION_BONUS;

        // Reset bird position at start of next section (matches game's StartSection)
        birdX = 200f;
        birdY = LOG_H / 2f;
        birdVY = 0f;
        section++;
    }

    return totalScore;
}

(bool survived, int pipesScored, float finalY, float finalVY)
    RunSection(int section, float birdX, float birdY, float birdVY, bool training)
{
    float speed = SectionSpeed(section);
    float spawnInt = SectionSpawnInt(section);
    float gapH = SectionGap(section);
    float[] pipeGapYs = sectionPipes[section];

    float sectionTimer = 0f;
    float spawnTimer = 0f;
    int pipeIndex = 0;

    var pipes = new List<SimPipe>();
    int pipesScored = 0;

    float maxSimTime = SECTION_DURATION + 30f;
    float t = 0f;

    while (t < maxSimTime)
    {
        if (sectionTimer < SECTION_DURATION + 1f)
        {
            sectionTimer += DT;
            spawnTimer += DT;
        }

        if (sectionTimer < SECTION_DURATION && spawnTimer >= spawnInt && pipeIndex < pipeGapYs.Length)
        {
            spawnTimer = 0f;
            pipes.Add(new SimPipe { X = LOG_W + 20f, GapY = pipeGapYs[pipeIndex], GapH = gapH });
            pipeIndex++;
        }

        // Compute current state and decide action
        int state = ComputeState(birdX, birdY, birdVY, pipes, gapH);
        int action;
        if (training && rng.NextDouble() < epsilon)
        {
            // Biased exploration: flap roughly 1/12 frames when random, matching
            // Flappy Bird's actual flap cadence. Uniform 50/50 flapping kills the
            // bird on the ceiling within 30 frames and makes credit assignment
            // impossible.
            action = rng.NextDouble() < (1.0 / 12.0) ? 1 : 0;
        }
        else
        {
            action = Q[state, 1] > Q[state, 0] ? 1 : 0;
        }

        if (training) visitCounts[state]++;

        // Apply action
        if (action == 1) birdVY = FLAP;

        // Physics
        birdVY += GRAVITY;
        if (birdVY > TERMINAL) birdVY = TERMINAL;
        birdY += birdVY;

        // World bounds death
        bool died = (birdY < 0 || birdY > LOG_H);

        // Move pipes
        if (!died)
        {
            foreach (var p in pipes) p.X -= speed * DT;
        }

        int newPipesScored = 0;
        // Score & remove
        if (!died)
        {
            for (int i = pipes.Count - 1; i >= 0; i--)
            {
                var p = pipes[i];
                if (!p.Scored && p.X + PIPE_W < birdX)
                {
                    p.Scored = true;
                    pipesScored++;
                    newPipesScored++;
                }
                if (p.X + PIPE_W < -10) pipes.RemoveAt(i);
            }
        }

        // Collision check
        if (!died && CheckCollision(birdX, birdY, pipes))
        {
            died = true;
        }

        // Section complete?
        bool sectionComplete = !died && sectionTimer >= SECTION_DURATION && pipes.Count == 0
                                && pipeIndex >= pipeGapYs.Length;

        // Compute reward
        float reward = REWARD_ALIVE + newPipesScored * REWARD_PIPE_CLEAR;
        if (died) reward = REWARD_DEATH;
        else if (sectionComplete) reward += REWARD_SECTION_CLEAR;

        // Q-update (Bellman) — using prevState/prevAction -> reward and current state's max
        if (training)
        {
            int nextState = ComputeState(birdX, birdY, birdVY, pipes, gapH);
            float maxNextQ = MathF.Max(Q[nextState, 0], Q[nextState, 1]);
            float target = died ? reward : reward + GAMMA * maxNextQ;
            Q[state, action] += ALPHA * (target - Q[state, action]);
        }

        if (died)
        {
            return (false, pipesScored, birdY, birdVY);
        }

        if (sectionComplete)
        {
            return (true, pipesScored, birdY, birdVY);
        }

        // Early-exit: timer expired and no pipes left
        if (sectionTimer >= SECTION_DURATION && pipes.Count == 0)
        {
            return (true, pipesScored, birdY, birdVY);
        }

        t += DT;
    }

    return (true, pipesScored, birdY, birdVY);
}

int ComputeState(float birdX, float birdY, float birdVY, List<SimPipe> pipes, float gapH)
{
    // Find pipe overlapping bird, or next ahead
    float birdLeft = birdX - BIRD_W / 2f + HITBOX_INSET;
    float birdRight = birdX + BIRD_W / 2f - HITBOX_INSET;

    SimPipe? inside = null;
    foreach (var p in pipes)
    {
        float pL = p.X, pR = p.X + PIPE_W;
        if (pR > birdLeft && pL < birdRight)
        {
            if (inside == null || p.X < inside.X) inside = p;
        }
    }

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
    float gapC = primary?.GapY ?? (LOG_H / 2f);
    float dist = primary != null ? MathF.Max(0f, primary.X - birdX) : LOG_W;

    return StateIndex(birdY, birdVY, gapC, dist);
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
        if (birdRight <= pipeLeft || birdLeft >= pipeRight) continue;

        float topH = MathF.Max(0f, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;

        if (topH > 0 && birdTop < topH && birdBottom > 0)
            return true;
        if (birdBottom > botY && birdTop < LOG_H)
            return true;
    }
    return false;
}

void SaveQTable(string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write(STATE_COUNT);
    bw.Write(ACTION_COUNT);
    for (int i = 0; i < STATE_COUNT; i++)
    {
        bw.Write(Q[i, 0]);
        bw.Write(Q[i, 1]);
    }
}

class SimPipe
{
    public float X;
    public float GapY;
    public float GapH;
    public bool Scored;
}
