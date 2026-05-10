using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ===== Game constants (must match FlappyBrainGameV2.cs exactly) =====
const float GRAVITY      = 0.15f;
const float FLAP         = -6.5f;
const float TERMINAL     = 10f;
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
static float SectionSpawnInt(int s) => MathF.Max(0.8f, 4.6f - s * 0.2f);

// ===== State discretization =====
const int BIRD_Y_BINS    = 24; // 25px each
const int BIRD_VEL_BINS  = 18;
const int GAP_CTR_BINS   = 18; // ~33px each
const int DIST_BINS      = 12; // ~67px each

const int STATE_COUNT  = BIRD_Y_BINS * BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS; // 93312
const int ACTION_COUNT = 2;

static int BinBirdY(float y)     => Math.Clamp((int)(y / 25f), 0, BIRD_Y_BINS - 1);
static int BinBirdVel(float v)   => Math.Clamp((int)((v + 7f) / 1.0f), 0, BIRD_VEL_BINS - 1);
static int BinGapCenter(float g) => Math.Clamp((int)(g / 33f), 0, GAP_CTR_BINS - 1);
static int BinDist(float d)      => Math.Clamp((int)(d / 67f), 0, DIST_BINS - 1);

static int StateIndex(float y, float vel, float gapC, float dist) =>
    BinBirdY(y) * (BIRD_VEL_BINS * GAP_CTR_BINS * DIST_BINS) +
    BinBirdVel(vel) * (GAP_CTR_BINS * DIST_BINS) +
    BinGapCenter(gapC) * DIST_BINS +
    BinDist(dist);

// ===== Q-table & hyperparameters =====
var Q = new float[STATE_COUNT, ACTION_COUNT];

const float ALPHA         = 0.25f;
const float GAMMA          = 0.97f;
const float EPSILON_START  = 0.6f;
const float EPSILON_END    = 0.02f;
const int   EPISODES       = 1_500_000;
const float EPSILON_DECAY  = (EPSILON_START - EPSILON_END) / (EPISODES * 0.7f);

const float REWARD_PIPE_CLEAR    =  40f;
const float REWARD_SECTION_CLEAR = 200f;
const float REWARD_DEATH         = -100f;
const float REWARD_ALIVE         =  0.5f;

// ===== Pre-generate pipe layout (matches the game and SimTest) =====
var sectionPipes = new float[TOTAL_SECTIONS][];
for (int s = 0; s < TOTAL_SECTIONS; s++)
{
    var layoutRng = new Random(SEED + s);
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
    // Heavily bias toward sections 0-3 since the test starts there.
    int startSection;
    if (ep < 200_000)
        startSection = 0;
    else if (ep < 500_000)
        startSection = rng.Next(0, 4);
    else if (ep < 800_000)
        startSection = rng.Next(0, 8);
    else if (ep < 1_000_000)
        startSection = rng.Next(0, TOTAL_SECTIONS);
    else
    {
        // Final phase: 90% on sections 0-3 (the targets), 10% on rest
        if (rng.NextDouble() < 0.9) startSection = rng.Next(0, 4);
        else startSection = rng.Next(0, TOTAL_SECTIONS);
    }

    // Slight start-state jitter so the policy is robust around the deterministic
    // start position (200, LOG_H/2, 0). Without this the greedy policy can be
    // brittle — passing the deterministic test depends on exact state.
    float startY = LOG_H / 2f + (float)(rng.NextDouble() - 0.5) * 60f;
    float startVY = (float)(rng.NextDouble() - 0.5) * 2f;
    int score = RunEpisode(startSection, training: true, startY: startY, startVY: startVY);

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
    var (survived, scored, finalY, finalVY) = RunSection(s, 200f, LOG_H / 2f, 0f, training: false);
    string status = survived ? "CLEAR" : "FAIL ";
    Console.WriteLine($"  Section {s + 1,2}: {status}  pipes_passed={scored}  final y={finalY:F0} vy={finalVY:F1}");
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
int RunEpisode(int startSection, bool training, float startY = -1f, float startVY = 0f)
{
    float birdX = 200f;
    float birdY = (startY < 0f) ? LOG_H / 2f : startY;
    float birdVY = startVY;

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
            // Biased exploration: flap roughly 1/18 frames when random.
            // Softer physics (G=0.15, FLAP=-6.5) means each flap arc lasts ~90
            // frames; uniform/frequent flapping pins the bird to the ceiling.
            action = rng.NextDouble() < (1.0 / 18.0) ? 1 : 0;
        }
        else
        {
            // Tie-breaker: when Q-values are equal (untrained state), prefer FLAP
            // if bird is below the gap target (or mid-screen if no pipe in view)
            // and not already flying upward. Prevents floor death-spiral in
            // unexplored states.
            if (Q[state, 1] > Q[state, 0]) action = 1;
            else if (Q[state, 1] < Q[state, 0]) action = 0;
            else
            {
                // Find next gap target
                float tgt = LOG_H / 2f;
                float minD = float.MaxValue;
                foreach (var p in pipes)
                {
                    if (p.X + PIPE_W >= birdX)
                    {
                        float d = p.X - birdX;
                        if (d < minD) { minD = d; tgt = p.GapY; }
                    }
                }
                // Flap if below target by >30px and not already rising fast
                if (birdY > tgt + 30f && birdVY > -2f) action = 1;
                else action = 0;
            }
        }

        if (training) visitCounts[state]++;

        // Apply action
        if (action == 1) birdVY = FLAP;

        // Physics
        birdVY += GRAVITY;
        if (birdVY > TERMINAL) birdVY = TERMINAL;
        birdY += birdVY;

        // World bounds death
        bool died = (birdY < 10 || birdY > LOG_H - 60);

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

        // Shaping: penalize being close to floor/ceiling, bonus near gap target.
        // Especially important in early sections where pipes are spaced far apart
        // and the bird must hover for seconds between obstacles.
        if (!died)
        {
            // Find the gap target the bird should track (next pipe or screen center)
            float gapTarget = LOG_H / 2f;
            float minDist = float.MaxValue;
            foreach (var p in pipes)
            {
                if (p.X + PIPE_W >= birdX)
                {
                    float d = p.X - birdX;
                    if (d < minDist) { minDist = d; gapTarget = p.GapY; }
                }
            }
            float yErr = MathF.Abs(birdY - gapTarget);
            // Negative shaping proportional to gap-target offset
            reward -= (yErr / LOG_H) * 1.0f;

            // Strong proximity-to-edge penalty (death zone is y<10 or y>540)
            if (birdY < 60f) reward -= 0.8f;
            else if (birdY > LOG_H - 110f) reward -= 0.8f;
        }

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
