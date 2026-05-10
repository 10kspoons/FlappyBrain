using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FlappyBrain;

public class FlappyBrainGameV2 : Game
{
    // ===== Logical resolution =====
    const int LogW = 800;
    const int LogH = 600;

    // ===== Section config =====
    const int TOTAL_SECTIONS = 20;
    const float SECTION_DURATION = 10f;
    const float TRANSITION_DURATION = 3f;
    const float SECTION_BONUS = 5f;
    const float OBSTACLE_POINTS = 1f;
    const int RNG_SEED = 20260509;

    // ===== Bird physics =====
    const float GRAVITY = 0.40f;
    const float FLAP = -8.5f;
    const float TERMINAL = 14f;
    const float BIRD_WIDTH = 40f;
    const float BIRD_HEIGHT = 36f;
    // Outback theme uses the large koala sprite (160x130) — hitbox ~70% of visual
    const float OUTBACK_BIRD_W = 110f;
    const float OUTBACK_BIRD_H = 90f;
    // Gap bonus for outback mode (sprite is much larger than pixel bird)
    const float OUTBACK_GAP_BONUS = 80f;

    // ===== Pipe =====
    const float PIPE_W = 90f;

    // ===== State machine =====
    enum GameState { Menu, Playing, GoreAnimation, SectionRetry, SectionTransition, SectionComplete, Victory }

    GameState _state = GameState.Menu;
    float _stateTimer = 0f;

    // ===== Section state =====
    int _currentSection = 0;
    int _currentPipeIndex = 0;
    float _sectionTimer = 0f;
    float _spawnTimer = 0f;
    int _totalScore = 0;
    int _sectionScore = 0;
    int _sectionAttempts = 1;
    int _bestScore = 0;

    // Pre-generated pipe layout for each section
    float[][] _sectionPipes = Array.Empty<float[]>();

    // ===== Bird =====
    float _birdX, _birdY, _birdVel, _birdRot;
    float _deathX, _deathY;

    // ===== Pipes =====
    class Pipe { public float X; public float GapY; public float GapH; public bool Scored; }
    readonly List<Pipe> _pipes = new();

    // ===== Gore =====
    enum GoreType { BloodBlob, FurChunk, Feather, Bone, BloodSpray }

    class GoreParticle
    {
        public GoreType Type;
        public float X, Y;
        public float VX, VY;
        public float Gravity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
        public float Rotation;
        public float RotSpeed;
        public bool Landed;
        public List<(float X, float Y, float Age)> Trail = new();
    }

    readonly List<GoreParticle> _gore = new();
    readonly List<(float X, float Y, float R, Color C)> _bloodStains = new();

    // ===== Confetti for victory =====
    class Confetti { public float X, Y, VX, VY, Rot, RotSpeed, Size; public Color Color; }
    readonly List<Confetti> _confetti = new();

    // ===== Graphics =====
    GraphicsDeviceManager _graphics = null!;
    SpriteBatch _spriteBatch = null!;
    Texture2D _pixel = null!;

    // ===== Input =====
    KeyboardState _prevKb;

    // ===== RNG for gore (non-deterministic, doesn't affect layout) =====
    readonly Random _goreRng = new();

    bool _aiMode;
    bool _outbackTheme;
    Texture2D? _koalaTexture;
    const string KOALA_ASSET = "/tmp/flappybrain-assets/74284772-cd66-4305-b730-6ad58da2ffe8.png";
    const string BG_ASSET    = "/tmp/flappybrain-assets/image-1---e490a7db-46c4-4ae2-b801-066b168dd1eb.png";
    Texture2D? _bgTexture;
    float _bgScroll = 0f;
    float _demoCollisionTimer = 0f;   // counts real gameplay seconds
    readonly Random _demoRng = new Random(12345);
    bool _learnedMode;
    float[,]? _qTable;

    public FlappyBrainGameV2(bool aiMode = false, bool learnedMode = false, bool outbackTheme = false)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = LogW,
            PreferredBackBufferHeight = LogH,
            SynchronizeWithVerticalRetrace = true,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        _aiMode = aiMode;
        _outbackTheme = outbackTheme;
        _learnedMode = learnedMode;
        if (_learnedMode)
        {
            _qTable = TryLoadQTable();
            if (_qTable == null)
            {
                Console.WriteLine("[--ai-learned] qtable.bin not found, falling back to geometry controller");
                _learnedMode = false;
            }
            else
            {
                Console.WriteLine("[--ai-learned] loaded Q-table");
            }
        }
        Window.Title = "Flappy Brain V2 — 20 Sections of Pain";
    }

    static float[,]? TryLoadQTable()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "qtable.bin");
        if (!File.Exists(path)) return null;
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            int stateCount = br.ReadInt32();
            int actionCount = br.ReadInt32();
            var q = new float[stateCount, actionCount];
            for (int i = 0; i < stateCount; i++)
                for (int a = 0; a < actionCount; a++)
                    q[i, a] = br.ReadSingle();
            return q;
        }
        catch { return null; }
    }

    protected override void Initialize()
    {
        PreGenerateSectionLayout();
        ResetToMenu();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        if (_outbackTheme && System.IO.File.Exists(BG_ASSET))
        {
            try { _bgTexture = Texture2D.FromFile(GraphicsDevice, BG_ASSET); }
            catch { }
        }
        if (_outbackTheme && System.IO.File.Exists(KOALA_ASSET))
        {
            try { _koalaTexture = Texture2D.FromFile(GraphicsDevice, KOALA_ASSET); }
            catch { /* fallback to pixel art */ }
        }
        _pixel.SetData(new[] { Color.White });
    }

    void PreGenerateSectionLayout()
    {
        var rng = new Random(RNG_SEED);
        _sectionPipes = new float[TOTAL_SECTIONS][];
        for (int s = 0; s < TOTAL_SECTIONS; s++)
        {
            float spawnInt = SectionSpawnInt(s);
            int count = (int)MathF.Ceiling(SECTION_DURATION / spawnInt) + 2;
            var arr = new float[count];
            float gap = SectionGap(s);
            float minY = gap / 2f + 60f;
            float maxY = LogH - gap / 2f - 60f;
            for (int i = 0; i < count; i++)
            {
                arr[i] = minY + (float)rng.NextDouble() * (maxY - minY);
            }
            _sectionPipes[s] = arr;
        }
    }

    static float SectionSpeed(int s) => 160f + s * 16f;
    static float SectionGap(int s) => MathF.Max(100f, 210f - s * 5.5f);
    static float SectionSpawnInt(int s) => MathF.Max(0.8f, 4.6f - s * 0.2f);

    void ResetToMenu()
    {
        _state = GameState.Menu;
        _stateTimer = 0;
        _currentSection = 0;
        _currentPipeIndex = 0;
        _sectionTimer = 0;
        _spawnTimer = 0;
        _totalScore = 0;
        _sectionScore = 0;
        _sectionAttempts = 1;
        _pipes.Clear();
        _gore.Clear();
        _bloodStains.Clear();
        _confetti.Clear();
        _birdX = 200;
        _birdY = LogH / 2f;
        _birdVel = 0;
        _birdRot = 0;
    }

    void StartSection(int section, bool isRetry)
    {
        _currentSection = section;
        _currentPipeIndex = 0;
        _sectionTimer = 0;
        _spawnTimer = 0;
        _sectionScore = 0;
        _pipes.Clear();
        _gore.Clear();
        _bloodStains.Clear();
        _birdX = 200;
        _birdY = LogH / 2f;
        _birdVel = 0;
        _birdRot = 0;
        if (isRetry) _sectionAttempts++;
        else _sectionAttempts = 1;
        _state = GameState.Playing;
        _stateTimer = 0;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var kb = Keyboard.GetState();

        if (kb.IsKeyDown(Keys.Escape)) Exit();

        bool flapPressed = (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                           (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up));
        bool restartPressed = kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R);

        if (restartPressed)
        {
            ResetToMenu();
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

        _stateTimer += dt;

        switch (_state)
        {
            case GameState.Menu:
                if (_aiMode) flapPressed = true; // auto-start
                if (flapPressed)
                {
                    _totalScore = 0;
                    StartSection(0, false);
                }
                break;

            case GameState.Playing:
                if (_aiMode) flapPressed = AiShouldFlap();
                UpdatePlaying(dt, flapPressed);
                break;

            case GameState.GoreAnimation:
                UpdateGore(dt);
                if (_stateTimer >= 2.5f)
                {
                    _state = GameState.SectionRetry;
                    _stateTimer = 0;
                }
                break;

            case GameState.SectionRetry:
                UpdateGore(dt); // gore continues to settle
                if (_stateTimer >= 1.0f)
                {
                    StartSection(_currentSection, true);
                }
                break;

            case GameState.SectionTransition:
                if (_aiMode) flapPressed = AiShouldFlap();
                UpdateBird(dt, flapPressed, allowFlap: true, allowDeath: false);
                // pipes drift off
                foreach (var p in _pipes) p.X -= SectionSpeed(_currentSection) * dt;
                _pipes.RemoveAll(p => p.X + PIPE_W < -10);
                if (_stateTimer >= TRANSITION_DURATION)
                {
                    int next = _currentSection + 1;
                    if (next >= TOTAL_SECTIONS)
                    {
                        _state = GameState.Victory;
                        _stateTimer = 0;
                        SpawnConfetti();
                    }
                    else
                    {
                        StartSection(next, false);
                    }
                }
                break;

            case GameState.Victory:
                UpdateConfetti(dt);
                if (flapPressed)
                {
                    ResetToMenu();
                }
                break;
        }

        if (_totalScore > _bestScore) _bestScore = _totalScore;

        _prevKb = kb;
        base.Update(gameTime);
    }

    void UpdatePlaying(float dt, bool flapPressed)
    {
        if (_outbackTheme) _bgScroll += 2.4f;

        // Demo: every 15 seconds of play, 1-in-3 chance of forced collision
        if (_aiMode)
        {
            _demoCollisionTimer += dt;
            if (_demoCollisionTimer >= 15f)
            {
                _demoCollisionTimer = 0f;
                if (_demoRng.Next(3) == 0)  // 1-in-3
                    TriggerGore();
            }
        }

        _sectionTimer += dt;
        _spawnTimer += dt;

        UpdateBird(dt, flapPressed, allowFlap: true, allowDeath: true);

        // Spawn pipes
        if (_sectionTimer < SECTION_DURATION)
        {
            float spawnInt = SectionSpawnInt(_currentSection);
            if (_spawnTimer >= spawnInt && _currentPipeIndex < _sectionPipes[_currentSection].Length)
            {
                _spawnTimer = 0;
                float gapY = _sectionPipes[_currentSection][_currentPipeIndex];
                _currentPipeIndex++;
                float gapH = SectionGap(_currentSection) + (_outbackTheme ? OUTBACK_GAP_BONUS : 0f);
                _pipes.Add(new Pipe { X = LogW + 20, GapY = gapY, GapH = gapH });
            }
        }

        // Move pipes
        float speed = SectionSpeed(_currentSection);
        foreach (var p in _pipes) p.X -= speed * dt;

        // Score & remove
        for (int i = _pipes.Count - 1; i >= 0; i--)
        {
            var p = _pipes[i];
            if (!p.Scored && p.X + PIPE_W < _birdX)
            {
                p.Scored = true;
                _totalScore++;
                _sectionScore++;
            }
            if (p.X + PIPE_W < -10) _pipes.RemoveAt(i);
        }

        // Collision
        if (_state == GameState.Playing)
        {
            CheckCollision();
        }

        // Section complete?
        if (_state == GameState.Playing && _sectionTimer >= SECTION_DURATION && _pipes.Count == 0)
        {
            _totalScore += (int)SECTION_BONUS;
            _state = GameState.SectionTransition;
            _stateTimer = 0;
        }
    }

    void UpdateBird(float dt, bool flapPressed, bool allowFlap, bool allowDeath)
    {
        if (allowFlap && flapPressed) _birdVel = FLAP;

        _birdVel += GRAVITY;
        if (_birdVel > TERMINAL) _birdVel = TERMINAL;
        _birdY += _birdVel;

        _birdRot = MathHelper.Clamp(_birdVel * 0.07f, -0.5f, 1.2f);

        if (allowDeath && (_birdY < 0 || _birdY > LogH))
        {
            TriggerGore();
        }

        // Keep bird in bounds during transition
        if (!allowDeath)
        {
            if (_birdY < 20) { _birdY = 20; _birdVel = 0; }
            if (_birdY > LogH - 20) { _birdY = LogH - 20; _birdVel = 0; }
        }
    }

    void CheckCollision()
    {
        float bW = _outbackTheme ? OUTBACK_BIRD_W : BIRD_WIDTH;
        float bH = _outbackTheme ? OUTBACK_BIRD_H : BIRD_HEIGHT;
        var birdBox = Collision.BirdHitbox(_birdX, _birdY, bW, bH);
        foreach (var p in _pipes)
        {
            var top = Collision.PipeTopRect(p.X, PIPE_W, p.GapY, p.GapH);
            var bot = Collision.PipeBottomRect(p.X, PIPE_W, p.GapY, p.GapH, LogH);
            if (birdBox.Intersects(top) || birdBox.Intersects(bot))
            {
                TriggerGore();
                return;
            }
        }
    }

    void TriggerGore()
    {
        _state = GameState.GoreAnimation;
        _stateTimer = 0;
        _deathX = _birdX;
        _deathY = MathHelper.Clamp(_birdY, 0, LogH);
        SpawnGoreParticles(_deathX, _deathY);
    }

    void SpawnGoreParticles(float cx, float cy)
    {
        var rng = _goreRng;

        // BloodBlob
        int blobs = rng.Next(30, 41);
        for (int i = 0; i < blobs; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 150f + (float)rng.NextDouble() * 250f;
            byte r = (byte)rng.Next(0x88, 0xCD);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.BloodBlob,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 280f,
                MaxLife = 1.8f + (float)rng.NextDouble() * 0.7f,
                Size = 6f + (float)rng.NextDouble() * 12f,
                Color = new Color(r, (byte)(0x08 + rng.Next(8)), (byte)(0x08 + rng.Next(8))),
            });
        }

        // FurChunk
        int furs = rng.Next(15, 21);
        for (int i = 0; i < furs; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 100f + (float)rng.NextDouble() * 180f;
            byte r = (byte)rng.Next(0x8B, 0xC5);
            byte g = (byte)rng.Next(0x60, 0x90);
            byte b = (byte)rng.Next(0x40, 0x60);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.FurChunk,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 220f,
                MaxLife = 2.0f + (float)rng.NextDouble() * 0.8f,
                Size = 8f + (float)rng.NextDouble() * 12f,
                Color = new Color(r, g, b),
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 8f,
            });
        }

        // Feathers
        int feathers = rng.Next(10, 16);
        for (int i = 0; i < feathers; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 60f + (float)rng.NextDouble() * 100f;
            bool grey = rng.NextDouble() > 0.5;
            Color c = grey ? new Color(0x80, 0x80, 0x80) : new Color(0xD4, 0xC0, 0x90);
            _gore.Add(new GoreParticle
            {
                Type = GoreType.Feather,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 100f,
                MaxLife = 2.5f + (float)rng.NextDouble() * 1.0f,
                Size = 5f,
                Color = c,
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 4f,
            });
        }

        // Bones
        int bones = rng.Next(5, 9);
        for (int i = 0; i < bones; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 200f + (float)rng.NextDouble() * 250f;
            _gore.Add(new GoreParticle
            {
                Type = GoreType.Bone,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 350f,
                MaxLife = 1.5f + (float)rng.NextDouble() * 0.5f,
                Size = 4f,
                Color = new Color(0xF0, 0xE8, 0xD0),
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 12f,
            });
        }

        // Blood spray
        int sprays = rng.Next(20, 31);
        for (int i = 0; i < sprays; i++)
        {
            float ang = (float)(rng.NextDouble() * MathF.PI * 2);
            float spd = 300f + (float)rng.NextDouble() * 300f;
            _gore.Add(new GoreParticle
            {
                Type = GoreType.BloodSpray,
                X = cx, Y = cy,
                VX = MathF.Cos(ang) * spd,
                VY = MathF.Sin(ang) * spd,
                Gravity = 200f,
                MaxLife = 0.8f + (float)rng.NextDouble() * 0.6f,
                Size = 2f + (float)rng.NextDouble() * 4f,
                Color = new Color(255, 16, 32) * 0.7f,
            });
        }
    }

    void UpdateGore(float dt)
    {
        for (int i = _gore.Count - 1; i >= 0; i--)
        {
            var g = _gore[i];
            g.Life += dt;
            if (g.Life >= g.MaxLife)
            {
                _gore.RemoveAt(i);
                continue;
            }
            if (!g.Landed)
            {
                g.X += g.VX * dt;
                g.Y += g.VY * dt;
                g.VY += g.Gravity * dt;
                g.Rotation += g.RotSpeed * dt;

                // Trails for blood spray
                if (g.Type == GoreType.BloodSpray)
                {
                    g.Trail.Add((g.X, g.Y, 0));
                    if (g.Trail.Count > 3) g.Trail.RemoveAt(0);
                }

                // Blood blob hits ground
                if (g.Type == GoreType.BloodBlob && g.Y >= LogH - 80)
                {
                    g.Y = LogH - 80;
                    g.Landed = true;
                    _bloodStains.Add((g.X, g.Y + g.Size * 0.4f, g.Size * 1.2f, new Color(g.Color.R, g.Color.G, g.Color.B) * 0.6f));
                }

                // Fur chunks settle on ground band
                if (g.Type == GoreType.FurChunk && g.Y >= LogH - 60)
                {
                    g.Y = LogH - 60 + (float)_goreRng.NextDouble() * 8f;
                    g.VX *= 0.4f;
                    g.VY = 0;
                    g.Gravity = 0;
                    g.RotSpeed *= 0.3f;
                }
            }
        }
    }

    void SpawnConfetti()
    {
        var rng = _goreRng;
        for (int i = 0; i < 200; i++)
        {
            Color c = new Color(rng.Next(100, 256), rng.Next(100, 256), rng.Next(100, 256));
            _confetti.Add(new Confetti
            {
                X = (float)rng.NextDouble() * LogW,
                Y = -20 - (float)rng.NextDouble() * 200,
                VX = ((float)rng.NextDouble() - 0.5f) * 80,
                VY = 60f + (float)rng.NextDouble() * 100,
                Rot = (float)rng.NextDouble() * MathF.PI * 2,
                RotSpeed = ((float)rng.NextDouble() - 0.5f) * 8,
                Size = 6f + (float)rng.NextDouble() * 6,
                Color = c,
            });
        }
    }

    void UpdateConfetti(float dt)
    {
        foreach (var c in _confetti)
        {
            c.X += c.VX * dt;
            c.Y += c.VY * dt;
            c.Rot += c.RotSpeed * dt;
            if (c.Y > LogH + 20) c.Y = -20;
        }
    }

    // ===== DRAWING =====

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_outbackTheme ? new Color(0x1A, 0x0F, 0x08) : new Color(0x6E, 0xC8, 0xE6));

        // Compute screen shake offset
        Vector2 shake = Vector2.Zero;
        if (_state == GameState.GoreAnimation)
        {
            float amt = MathHelper.Clamp(12f * (1f - _stateTimer / 2.5f), 0, 12);
            shake = new Vector2(MathF.Sin(_stateTimer * 80f) * amt, MathF.Cos(_stateTimer * 70f) * amt);
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        DrawSky(shake);
        DrawGround(shake);

        // Pipes
        if (_state == GameState.Playing || _state == GameState.GoreAnimation ||
            _state == GameState.SectionRetry || _state == GameState.SectionTransition)
        {
            foreach (var p in _pipes) DrawPipe(p, shake);
        }

        // Blood stains (persist on ground)
        foreach (var s in _bloodStains)
        {
            DrawCircle(s.X + shake.X, s.Y + shake.Y, s.R, s.C);
        }

        // Gore particles
        foreach (var g in _gore) DrawGoreParticle(g, shake);

        // Bird (not during gore)
        if (_state != GameState.GoreAnimation && _state != GameState.SectionRetry && _state != GameState.Victory)
        {
            DrawBird(_birdX + shake.X, _birdY + shake.Y, _birdRot);
        }

        // Red flash on gore
        if (_state == GameState.GoreAnimation && _stateTimer < 0.15f)
        {
            float a = (1f - _stateTimer / 0.15f) * 0.6f;
            DrawRect(0, 0, LogW, LogH, new Color(255, 0, 0) * a);
        }

        // State-specific overlays
        switch (_state)
        {
            case GameState.Menu: DrawMenu(); break;
            case GameState.Playing: DrawHud(); break;
            case GameState.GoreAnimation: DrawGoreOverlay(); DrawHud(); break;
            case GameState.SectionRetry: DrawRetryOverlay(); break;
            case GameState.SectionTransition: DrawTransitionOverlay(); DrawHud(); break;
            case GameState.Victory: DrawVictoryOverlay(); break;
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    void DrawSky(Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackSky(shake); return; }
        // Gradient bands
        for (int y = 0; y < 4; y++)
        {
            byte b = (byte)(0xE6 - y * 8);
            byte g = (byte)(0xC8 - y * 6);
            DrawRect(shake.X, shake.Y + y * (LogH / 4f), LogW, LogH / 4f + 2, new Color(0x6E + y * 4, g, b));
        }
        // Clouds
        DrawCloud(120 + shake.X, 80 + shake.Y);
        DrawCloud(450 + shake.X, 130 + shake.Y);
        DrawCloud(680 + shake.X, 60 + shake.Y);
    }

    void DrawCloud(float x, float y)
    {
        var c = Color.White * 0.85f;
        DrawCircle(x, y, 22, c);
        DrawCircle(x + 24, y - 8, 26, c);
        DrawCircle(x + 50, y, 22, c);
        DrawCircle(x + 30, y + 10, 24, c);
    }

    void DrawGround(Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackGround(shake); return; }
        DrawRect(shake.X, shake.Y + LogH - 60, LogW, 60, new Color(0x8B, 0x6F, 0x4E));
        DrawRect(shake.X, shake.Y + LogH - 60, LogW, 6, new Color(0x6E, 0xB7, 0x4E));
        // Grass tufts
        for (int i = 0; i < LogW; i += 16)
        {
            DrawRect(shake.X + i, shake.Y + LogH - 62, 3, 4, new Color(0x4E, 0x8B, 0x3A));
        }
    }

    void DrawPipe(Pipe p, Vector2 shake)
    {
        if (_outbackTheme) { DrawOutbackPipe(p, shake); return; }
        float topH = MathF.Max(0, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;
        float botH = MathF.Max(0, LogH - 60 - botY);

        Color body = new Color(0x4E, 0xA8, 0x4E);
        Color shade = new Color(0x36, 0x80, 0x36);
        Color light = new Color(0x80, 0xD8, 0x70);
        Color rim = new Color(0x28, 0x60, 0x28);

        // Top pipe body
        DrawRect(p.X + shake.X, shake.Y, PIPE_W, topH, body);
        // Corrugated stripes
        for (int i = 0; i < topH; i += 12)
        {
            DrawRect(p.X + shake.X + 6, shake.Y + i, 4, 8, light);
            DrawRect(p.X + shake.X + PIPE_W - 10, shake.Y + i, 4, 8, shade);
        }
        // Top cap
        if (topH > 0)
        {
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 18, PIPE_W + 8, 18, body);
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 18, PIPE_W + 8, 4, light);
            DrawRect(p.X + shake.X - 4, shake.Y + topH - 4, PIPE_W + 8, 4, rim);
        }

        // Bottom pipe body
        DrawRect(p.X + shake.X, shake.Y + botY, PIPE_W, botH, body);
        for (int i = 0; i < botH; i += 12)
        {
            DrawRect(p.X + shake.X + 6, shake.Y + botY + i, 4, 8, light);
            DrawRect(p.X + shake.X + PIPE_W - 10, shake.Y + botY + i, 4, 8, shade);
        }
        // Bottom cap
        if (botH > 0)
        {
            DrawRect(p.X + shake.X - 4, shake.Y + botY, PIPE_W + 8, 18, body);
            DrawRect(p.X + shake.X - 4, shake.Y + botY, PIPE_W + 8, 4, light);
            DrawRect(p.X + shake.X - 4, shake.Y + botY + 14, PIPE_W + 8, 4, rim);
        }
    }

    void DrawBird(float x, float y, float rot)
    {
        if (_outbackTheme && _koalaTexture != null)
        {
            // Draw koala asset sprite
            float sprW = 160f, sprH = 130f;
            float r = MathHelper.Clamp(rot, -0.5f, 1.2f);
            float scaleX = sprW / _koalaTexture.Width;
            float scaleY = sprH / _koalaTexture.Height;
            // Draw centered at (x, y) — origin = texture center, position = bird center
            _spriteBatch.Draw(_koalaTexture,
                new Vector2(x, y),
                null,
                Color.White,
                r,
                new Vector2(_koalaTexture.Width / 2f, _koalaTexture.Height / 2f),
                new Vector2(scaleX, scaleY),
                SpriteEffects.None,
                0f);
            // Draw hitbox outline for debugging (thin red border)
            float hbW = OUTBACK_BIRD_W - 8f, hbH = OUTBACK_BIRD_H - 8f;
            var hitCol = new Color(255, 0, 0) * 0.6f;
            DrawRect(x - hbW/2, y - hbH/2, hbW, 2, hitCol);  // top
            DrawRect(x - hbW/2, y + hbH/2, hbW, 2, hitCol);  // bottom
            DrawRect(x - hbW/2, y - hbH/2, 2, hbH, hitCol);  // left
            DrawRect(x + hbW/2, y - hbH/2, 2, hbH, hitCol);  // right
            return;
        }
        // Body — golden
        Color body = new Color(0xFF, 0xD0, 0x40);
        Color belly = new Color(0xFF, 0xE8, 0x80);
        Color wing = new Color(0xE0, 0xA0, 0x20);
        Color beak = new Color(0xFF, 0x80, 0x20);
        Color eye = Color.Black;

        // approximate rotation by tilting via offsets (cheap)
        float tilt = rot * 6f;

        DrawRect(x - 16, y - 14 + tilt * 0.2f, 32, 28, body);
        DrawRect(x - 14, y - 4 + tilt * 0.2f, 28, 14, belly);
        // wing
        DrawRect(x - 10, y - 2, 14, 10, wing);
        // eye
        DrawRect(x + 4, y - 8, 6, 6, Color.White);
        DrawRect(x + 6, y - 7, 3, 3, eye);
        // beak
        DrawRect(x + 14, y - 2 + tilt * 0.3f, 10, 6, beak);
    }

    void DrawGoreParticle(GoreParticle g, Vector2 shake)
    {
        float alpha = 1f;
        float fadeStart = g.MaxLife * 0.7f;
        if (g.Life > fadeStart) alpha = 1f - (g.Life - fadeStart) / (g.MaxLife - fadeStart);
        Color c = g.Color * alpha;

        float x = g.X + shake.X;
        float y = g.Y + shake.Y;

        switch (g.Type)
        {
            case GoreType.BloodBlob:
                DrawCircle(x, y, g.Size, c);
                DrawCircle(x - g.Size * 0.3f, y - g.Size * 0.2f, g.Size * 0.7f, c);
                break;
            case GoreType.FurChunk:
                DrawCircle(x, y, g.Size * 0.6f, c);
                DrawCircle(x + g.Size * 0.4f, y - g.Size * 0.2f, g.Size * 0.5f, c);
                DrawCircle(x - g.Size * 0.3f, y + g.Size * 0.3f, g.Size * 0.5f, c);
                break;
            case GoreType.Feather:
                DrawRotRect(x, y, 5, 18, g.Rotation, c);
                break;
            case GoreType.Bone:
                DrawRotRect(x, y, 4, 12, g.Rotation, c);
                break;
            case GoreType.BloodSpray:
                // Trail
                for (int i = 0; i < g.Trail.Count; i++)
                {
                    float ta = alpha * (i + 1) / (float)(g.Trail.Count + 1) * 0.5f;
                    DrawCircle(g.Trail[i].X + shake.X, g.Trail[i].Y + shake.Y, g.Size * 0.7f, c * ta);
                }
                DrawCircle(x, y, g.Size, c);
                break;
        }
    }

    void DrawGoreOverlay()
    {
        if (_stateTimer >= 0.3f)
        {
            float fadeIn = MathHelper.Clamp((_stateTimer - 0.3f) / 0.4f, 0, 1);
            string txt = "💀 SPLAT!";
            DrawBigText(txt, LogW / 2f, LogH / 2f, 72, new Color(255, 32, 32) * fadeIn, centered: true, outline: true);
            DrawBigText($"SECTION {_currentSection + 1} FAILED", LogW / 2f, LogH / 2f + 60, 28, Color.White * fadeIn, centered: true);
        }
    }

    void DrawRetryOverlay()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.7f);
        DrawBigText($"SECTION {_currentSection + 1} — RETRY", LogW / 2f, LogH / 2f - 20, 48, Color.White, centered: true, outline: true);
        DrawBigText($"Attempt {_sectionAttempts + 1}", LogW / 2f, LogH / 2f + 30, 28, new Color(180, 180, 180), centered: true);
    }

    void DrawTransitionOverlay()
    {
        // Top countdown bar
        float prog = 1f - (_stateTimer / TRANSITION_DURATION);
        DrawRect(0, 0, LogW * prog, 6, new Color(0xFF, 0xD0, 0x40));

        DrawBigText($"✓ SECTION {_currentSection + 1} COMPLETE", LogW / 2f, LogH / 2f - 60, 52, new Color(0x40, 0xFF, 0x80), centered: true, outline: true);
        DrawBigText("+5 POINTS", LogW / 2f, LogH / 2f, 36, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        if (_currentSection + 1 >= TOTAL_SECTIONS)
        {
            DrawBigText("FINAL SECTION!", LogW / 2f, LogH / 2f + 50, 28, Color.White, centered: true);
        }
        else
        {
            DrawBigText($"Next: Section {_currentSection + 2}", LogW / 2f, LogH / 2f + 50, 24, Color.White, centered: true);
        }
    }

    void DrawVictoryOverlay()
    {
        DrawRect(0, 0, LogW, LogH, new Color(0x10, 0x10, 0x40) * 0.85f);

        // Confetti
        foreach (var c in _confetti)
        {
            DrawRotRect(c.X, c.Y, c.Size, c.Size * 0.6f, c.Rot, c.Color);
        }

        DrawBigText("\U0001F389 COMPLETE!", LogW / 2f, 140, 72, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        DrawBigText($"FINAL SCORE: {_totalScore}", LogW / 2f, 250, 48, Color.White, centered: true, outline: true);
        DrawBigText($"All {TOTAL_SECTIONS} sections cleared!", LogW / 2f, 320, 28, new Color(0x40, 0xFF, 0x80), centered: true);
        DrawBigText($"Best: {_bestScore}", LogW / 2f, 370, 24, new Color(200, 200, 200), centered: true);
        DrawBigText("Press SPACE to play again", LogW / 2f, 470, 24, Color.White, centered: true);
    }

    void DrawMenu()
    {
        DrawRect(0, 0, LogW, LogH, Color.Black * 0.45f);
        DrawBigText("FLAPPY BRAIN V2", LogW / 2f, 140, 64, new Color(0xFF, 0xD0, 0x40), centered: true, outline: true);
        DrawBigText("20 SECTIONS OF PAIN", LogW / 2f, 210, 32, new Color(0xFF, 0x40, 0x40), centered: true, outline: true);
        DrawBigText("Survive each section. Die = retry.", LogW / 2f, 290, 22, Color.White, centered: true);
        DrawBigText("+1 per pipe  •  +5 per section", LogW / 2f, 325, 22, new Color(0x40, 0xFF, 0x80), centered: true);
        DrawBigText("Press SPACE / UP to start", LogW / 2f, 410, 28, Color.White, centered: true, outline: true);
        DrawBigText("R = restart  •  ESC = quit", LogW / 2f, 460, 18, new Color(180, 180, 180), centered: true);
        if (_bestScore > 0)
        {
            DrawBigText($"Best: {_bestScore}", LogW / 2f, 510, 22, new Color(0xFF, 0xD0, 0x40), centered: true);
        }
    }

    void DrawHud()
    {
        // Left: section + score
        DrawBigText($"SECTION: {_currentSection + 1}/{TOTAL_SECTIONS}", 16, 14, 22, Color.White, centered: false, outline: true);
        DrawBigText($"SCORE: {_totalScore}", 16, 42, 28, new Color(0xFF, 0xD0, 0x40), centered: false, outline: true);

        // Section progress bar
        float secProg = MathHelper.Clamp(_sectionTimer / SECTION_DURATION, 0, 1);
        DrawRect(16, 80, 220, 8, new Color(40, 40, 40));
        DrawRect(16, 80, 220 * secProg, 8, new Color(0x40, 0xFF, 0x80));

        // Right: best
        DrawBigText($"BEST: {_bestScore}", LogW - 140, 14, 22, Color.White, centered: false, outline: true);

        // Section tick marks at top
        int tickW = 28;
        int gap = 4;
        int totalW = TOTAL_SECTIONS * tickW + (TOTAL_SECTIONS - 1) * gap;
        float startX = (LogW - totalW) / 2f;
        for (int s = 0; s < TOTAL_SECTIONS; s++)
        {
            Color c = s < _currentSection ? new Color(0xFF, 0xD0, 0x40) :
                      s == _currentSection ? Color.White :
                      new Color(60, 60, 60);
            DrawRect(startX + s * (tickW + gap), 6, tickW, 6, c);
        }

        if (_sectionAttempts > 1)
        {
            DrawBigText($"ATTEMPT {_sectionAttempts}", 16, 100, 18, new Color(255, 80, 80), centered: false, outline: true);
        }
    }

    // ===== Primitive draw helpers =====

    void DrawRect(float x, float y, float w, float h, Color c)
    {
        if (w <= 0 || h <= 0) return;
        _spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)MathF.Ceiling(w), (int)MathF.Ceiling(h)), c);
    }

    void DrawCircle(float cx, float cy, float r, Color c)
    {
        if (r <= 0) return;
        int ir = (int)MathF.Ceiling(r);
        for (int dy = -ir; dy <= ir; dy++)
        {
            int dx = (int)MathF.Sqrt(r * r - dy * dy);
            DrawRect(cx - dx, cy + dy, dx * 2, 1, c);
        }
    }

    void DrawRotRect(float cx, float cy, float w, float h, float rot, Color c)
    {
        _spriteBatch.Draw(_pixel,
            position: new Vector2(cx, cy),
            sourceRectangle: null,
            color: c,
            rotation: rot,
            origin: new Vector2(0.5f, 0.5f),
            scale: new Vector2(w, h),
            effects: SpriteEffects.None,
            layerDepth: 0);
    }

    // Bitmap pixel font (5x7) — uppercase + digits + a few symbols
    static readonly Dictionary<char, string[]> Font = BuildFont();

    static Dictionary<char, string[]> BuildFont()
    {
        var f = new Dictionary<char, string[]>();
        void Add(char ch, params string[] rows) => f[ch] = rows;

        Add('A', "01110", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('B', "11110", "10001", "10001", "11110", "10001", "10001", "11110");
        Add('C', "01110", "10001", "10000", "10000", "10000", "10001", "01110");
        Add('D', "11110", "10001", "10001", "10001", "10001", "10001", "11110");
        Add('E', "11111", "10000", "10000", "11110", "10000", "10000", "11111");
        Add('F', "11111", "10000", "10000", "11110", "10000", "10000", "10000");
        Add('G', "01110", "10001", "10000", "10111", "10001", "10001", "01110");
        Add('H', "10001", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('I', "01110", "00100", "00100", "00100", "00100", "00100", "01110");
        Add('J', "00111", "00010", "00010", "00010", "00010", "10010", "01100");
        Add('K', "10001", "10010", "10100", "11000", "10100", "10010", "10001");
        Add('L', "10000", "10000", "10000", "10000", "10000", "10000", "11111");
        Add('M', "10001", "11011", "10101", "10001", "10001", "10001", "10001");
        Add('N', "10001", "11001", "10101", "10011", "10001", "10001", "10001");
        Add('O', "01110", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('P', "11110", "10001", "10001", "11110", "10000", "10000", "10000");
        Add('Q', "01110", "10001", "10001", "10001", "10101", "10010", "01101");
        Add('R', "11110", "10001", "10001", "11110", "10100", "10010", "10001");
        Add('S', "01111", "10000", "10000", "01110", "00001", "00001", "11110");
        Add('T', "11111", "00100", "00100", "00100", "00100", "00100", "00100");
        Add('U', "10001", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('V', "10001", "10001", "10001", "10001", "10001", "01010", "00100");
        Add('W', "10001", "10001", "10001", "10001", "10101", "11011", "10001");
        Add('X', "10001", "10001", "01010", "00100", "01010", "10001", "10001");
        Add('Y', "10001", "10001", "01010", "00100", "00100", "00100", "00100");
        Add('Z', "11111", "00001", "00010", "00100", "01000", "10000", "11111");
        Add('0', "01110", "10001", "10011", "10101", "11001", "10001", "01110");
        Add('1', "00100", "01100", "00100", "00100", "00100", "00100", "01110");
        Add('2', "01110", "10001", "00001", "00010", "00100", "01000", "11111");
        Add('3', "01110", "10001", "00001", "00110", "00001", "10001", "01110");
        Add('4', "00010", "00110", "01010", "10010", "11111", "00010", "00010");
        Add('5', "11111", "10000", "11110", "00001", "00001", "10001", "01110");
        Add('6', "00110", "01000", "10000", "11110", "10001", "10001", "01110");
        Add('7', "11111", "00001", "00010", "00100", "01000", "01000", "01000");
        Add('8', "01110", "10001", "10001", "01110", "10001", "10001", "01110");
        Add('9', "01110", "10001", "10001", "01111", "00001", "00010", "01100");
        Add(' ', "00000", "00000", "00000", "00000", "00000", "00000", "00000");
        Add(':', "00000", "00100", "00100", "00000", "00100", "00100", "00000");
        Add('!', "00100", "00100", "00100", "00100", "00100", "00000", "00100");
        Add('.', "00000", "00000", "00000", "00000", "00000", "00100", "00100");
        Add('/', "00001", "00010", "00010", "00100", "01000", "01000", "10000");
        Add('-', "00000", "00000", "00000", "11111", "00000", "00000", "00000");
        Add('+', "00000", "00100", "00100", "11111", "00100", "00100", "00000");
        Add('•', "00000", "00000", "01110", "01110", "01110", "00000", "00000");
        return f;
    }

    void DrawBigText(string text, float x, float y, int sizePx, Color c, bool centered = false, bool outline = false)
    {
        // map all chars to uppercase, unknown -> space
        text = text.ToUpperInvariant();
        // strip emoji-likes the font doesn't have, replace with placeholders
        var clean = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (Font.ContainsKey(ch))
            {
                clean.Append(ch);
            }
            else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                // skip emoji surrogate pair, replace with star
                clean.Append('*');
                i++;
            }
            else if (ch == '✓' || ch == '✔') // check marks
            {
                clean.Append('+');
            }
            else
            {
                clean.Append(' ');
            }
        }
        text = clean.ToString();

        // pixel cell size
        int pixSize = MathF.Max(1, sizePx / 8) > 0 ? (int)MathF.Max(1, sizePx / 8f) : 1;
        if (pixSize < 1) pixSize = 1;
        int charW = 5 * pixSize;
        int charH = 7 * pixSize;
        int charSpacing = pixSize;

        int totalW = text.Length * (charW + charSpacing) - charSpacing;
        float startX = centered ? x - totalW / 2f : x;
        float startY = centered ? y - charH / 2f : y;

        if (outline)
        {
            int o = MathF.Max(1, pixSize / 2) > 0 ? (int)MathF.Max(1, pixSize / 2f) : 1;
            for (int dx = -o; dx <= o; dx += o)
                for (int dy = -o; dy <= o; dy += o)
                    if (dx != 0 || dy != 0)
                        DrawTextRaw(text, startX + dx, startY + dy, pixSize, Color.Black);
        }
        DrawTextRaw(text, startX, startY, pixSize, c);
    }

    void DrawTextRaw(string text, float x, float y, int pixSize, Color c)
    {
        int charW = 5 * pixSize;
        int charH = 7 * pixSize;
        int charSpacing = pixSize;
        float cx = x;
        foreach (char ch in text)
        {
            if (!Font.TryGetValue(ch, out var rows))
            {
                cx += charW + charSpacing;
                continue;
            }
            for (int row = 0; row < 7; row++)
            {
                string r = rows[row];
                for (int col = 0; col < 5; col++)
                {
                    if (r[col] == '1')
                    {
                        DrawRect(cx + col * pixSize, y + row * pixSize, pixSize, pixSize, c);
                    }
                }
            }
            cx += charW + charSpacing;
        }
    }

    // ===== AI Autopilot =====
    int _aiDebugFrame = 0;


    // ===== Q-table state binning (must match FlappyBrain.Learning) =====
    const int QL_BIRD_Y_BINS   = 20;
    const int QL_BIRD_VEL_BINS = 16;
    const int QL_GAP_CTR_BINS  = 15;
    const int QL_DIST_BINS     = 10;
    const float QL_HITBOX_INSET = 4f;

    static int QlBinBirdY(float y)     => Math.Clamp((int)(y / 30f), 0, QL_BIRD_Y_BINS - 1);
    static int QlBinBirdVel(float v)   => Math.Clamp((int)((v + 9f) / 1.5f), 0, QL_BIRD_VEL_BINS - 1);
    static int QlBinGapCenter(float g) => Math.Clamp((int)(g / 40f), 0, QL_GAP_CTR_BINS - 1);
    static int QlBinDist(float d)      => Math.Clamp((int)(d / 80f), 0, QL_DIST_BINS - 1);

    static int QlStateIndex(float y, float vel, float gapC, float dist) =>
        QlBinBirdY(y) * (QL_BIRD_VEL_BINS * QL_GAP_CTR_BINS * QL_DIST_BINS) +
        QlBinBirdVel(vel) * (QL_GAP_CTR_BINS * QL_DIST_BINS) +
        QlBinGapCenter(gapC) * QL_DIST_BINS +
        QlBinDist(dist);

    bool AiShouldFlapLearned()
    {
        // Find pipe overlapping bird (preferred), or next ahead
        float bW2 = _outbackTheme ? OUTBACK_BIRD_W : BIRD_WIDTH;
        float birdLeft = _birdX - bW2 / 2f + QL_HITBOX_INSET;
        float birdRight = _birdX + bW2 / 2f - QL_HITBOX_INSET;

        Pipe? inside = null;
        foreach (var p in _pipes)
        {
            float pL = p.X, pR = p.X + PIPE_W;
            if (pR > birdLeft && pL < birdRight)
            {
                if (inside == null || p.X < inside.X) inside = p;
            }
        }

        Pipe? next = null;
        if (inside == null)
        {
            float bestX = float.MaxValue;
            foreach (var p in _pipes)
            {
                if (p.X >= birdRight && p.X < bestX)
                {
                    next = p;
                    bestX = p.X;
                }
            }
        }

        var primary = inside ?? next;
        float gapC = primary?.GapY ?? (LogH / 2f);
        float dist = primary != null ? MathF.Max(0f, primary.X - _birdX) : LogW;

        int state = QlStateIndex(_birdY, _birdVel, gapC, dist);
        return _qTable![state, 1] > _qTable[state, 0];
    }

    bool AiShouldFlap()
    {
        if (_learnedMode && _qTable != null) return AiShouldFlapLearned();

        _aiDebugFrame++;

        // Find nearest upcoming pipe
        Pipe? next = null;
        float minDist = float.MaxValue;
        foreach (var p in _pipes)
        {
            if (p.X + PIPE_W > _birdX)
            {
                float d = p.X - _birdX;
                if (d < minDist) { minDist = d; next = p; }
            }
        }

        float gapCenter = next != null ? next.GapY : LogH / 2f;
        float gapH      = next != null ? next.GapH : 160f;
        float gapTop    = gapCenter - gapH / 2f;
        float gapBottom = gapCenter + gapH / 2f;

        // Safe flap zone: bird must be in lower gapH*0.28 portion so peak stays in gap
        // With flap=-8.5 and gravity=0.4: peak rise ≈ 8.5*8.5/(2*0.4) ≈ 90px
        // So flap when birdY >= gapCenter + (90 - gapH/2 + margin)
        float minFlapY  = gapCenter + Math.Max(20f, 90f - gapH / 2f + 15f);

        // Debug every 60 frames
        if (_aiDebugFrame % 60 == 0)
            System.Console.WriteLine(
                $"AI #{_aiDebugFrame} birdY={_birdY:F0} vel={_birdVel:F1} " +
                $"gapC={gapCenter:F0} gapH={gapH:F0} minFlapY={minFlapY:F0} " +
                $"pipes={_pipes.Count} sec={_currentSection} score={_totalScore}");

        // RULE 1: Never flap if above the minimum flap Y (stay above center naturally)
        if (_birdY < minFlapY) return false;

        // RULE 2: Flap when in the safe zone and not already rising fast
        if (_birdVel > -4f) return true;

        // RULE 3: Emergency — about to hit floor or ground
        if (_birdY > gapBottom - 10f) return true;
        if (_birdY > LogH - 60f) return true;

        return false;
    }


    // ===== OUTBACK THEME HELPERS =====

    void DrawOutbackSky(Vector2 shake)
    {
        // Post-apocalyptic: draw BG asset first, then burnt-orange sky gradient overlay
        if (_bgTexture != null)
        {
            _spriteBatch.Draw(_bgTexture,
                new Rectangle(0, 0, LogW, LogH),
                Color.White);
        }

        // Burnt-orange gradient overlay at 65% opacity: #1A0F08 → #6B3020 → #C4622D
        var c1 = new Color(0x1A, 0x0F, 0x08);
        var c2 = new Color(0x6B, 0x30, 0x20);
        var c3 = new Color(0xC4, 0x62, 0x2D);
        int skyH = LogH - 70;
        for (int y = 0; y < skyH; y++)
        {
            float t = y / (float)skyH;
            Color c = t < 0.55f
                ? Color.Lerp(c1, c2, t / 0.55f)
                : Color.Lerp(c2, c3, (t - 0.55f) / 0.45f);
            DrawRect(0, y + shake.Y, LogW, 1, c * 0.65f);
        }

        // Speed lines: 15 horizontal streaks from left edge
        var rngSL = new Random((int)(_bgScroll / 30f));
        for (int i = 0; i < 15; i++)
        {
            float py = rngSL.NextSingle() * LogH;
            float len = 60 + rngSL.NextSingle() * 120;
            float alpha = 0.20f + rngSL.NextSingle() * 0.25f;
            DrawRect(shake.X, py + shake.Y, len, 1, new Color(0xC4, 0x62, 0x2D) * alpha);
        }

        // Dust particles drifting left
        var rng2 = new Random(42);
        for (int i = 0; i < 80; i++)
        {
            float px = ((rng2.NextSingle() * LogW * 2 - _bgScroll * (0.5f + rng2.NextSingle())) % (LogW * 2));
            if (px < 0) px += LogW * 2;
            if (px > LogW) continue;
            float py = 30 + rng2.NextSingle() * 500;
            float sz = 2 + rng2.NextSingle() * 2;
            DrawRect(px + shake.X, py + shake.Y, (int)sz, (int)sz, new Color(0xD4, 0x95, 0x6A) * 0.35f);
        }

        // Ruined city silhouette (far background parallax)
        DrawRuinedSkyline(shake);
    }

    void DrawRuinedSkyline(Vector2 shake)
    {
        float offset = (_bgScroll * 0.08f) % (LogW * 2);
        var col = new Color(0x2A, 0x15, 0x08) * 0.9f;
        int[] widths  = { 40, 25, 55, 30, 70, 20, 45, 35 };
        int[] heights = { 180, 120, 220, 140, 160, 90, 200, 130 };
        int x = 0;
        for (int i = 0; i < widths.Length * 2; i++)
        {
            int idx = i % widths.Length;
            int bx = (int)(x - offset + LogW);
            if (bx > -80 && bx < LogW + 20)
            {
                int by = LogH - 70 - heights[idx];
                DrawRect(bx + shake.X, by + shake.Y, widths[idx], heights[idx], col);
                // Broken top edge
                DrawRect(bx + shake.X + widths[idx] - 8, by + shake.Y - 20, 8, 20, col);
            }
            x += widths[idx] + 10;
        }
    }

    void DrawGumTrees(float parallax, Color col, int baseY)
    {
        float offset = (_bgScroll * parallax) % (LogW * 2);
        int[] xs = { 0, 220, 450, 670, 900, 1120 };
        foreach (int tx in xs)
        {
            int x = (int)(tx - offset);
            if (x > -120 && x < LogW + 60)
                DrawSingleGumTree(x, baseY, col);
        }
    }

    void DrawSingleGumTree(int x, int baseY, Color col)
    {
        // Trunk
        DrawRect(x, baseY - 80, 8, 80, col);
        // Leaf clusters
        DrawCircle(x + 4, baseY - 95, 28, col);
        DrawCircle(x - 20, baseY - 80, 22, col);
        DrawCircle(x + 25, baseY - 78, 25, col);
    }

    void DrawOutbackGround(Vector2 shake)
    {
        // Post-apoc: dark ash and rubble
        int groundY = LogH - 70;
        for (int y = 0; y < 70; y++)
        {
            float t = y / 70f;
            Color c = Color.Lerp(new Color(0x3A, 0x20, 0x10), new Color(0x1A, 0x0A, 0x05), t);
            DrawRect(0, groundY + y + shake.Y, LogW, 1, c);
        }
        // Rubble chunks
        var rng3 = new Random(77);
        for (int i = 0; i < 20; i++)
        {
            int rx = (int)((rng3.NextSingle() * LogW * 2 - _bgScroll * 0.3f) % (LogW * 2));
            if (rx > LogW || rx < -30) continue;
            int rw = 4 + (int)(rng3.NextSingle() * 14);
            int rh = 3 + (int)(rng3.NextSingle() * 8);
            DrawRect(rx + shake.X, groundY + shake.Y + 3, rw, rh, new Color(0x2A, 0x15, 0x08));
        }
    }

    void DrawOutbackPipe(Pipe p, Vector2 shake)
    {
        float topH = MathF.Max(0, p.GapY - p.GapH / 2f);
        float botY = p.GapY + p.GapH / 2f;
        float botH = MathF.Max(0, LogH - 70 - botY);

        // Post-apoc: darker, rustier corrugated iron
        var body   = new Color(0x5A, 0x30, 0x10);
        var stripe = new Color(0x6B, 0x40, 0x20);
        var cap    = new Color(0x4A, 0x28, 0x08);

        // Top pipe
        DrawRect(p.X + shake.X, shake.Y, PIPE_W, topH, body);
        for (float i = 0; i < topH; i += 12)
            DrawRect(p.X + shake.X, shake.Y + i, PIPE_W, 2, stripe);
        if (topH > 0)
            DrawRect(p.X + shake.X - 5, shake.Y + topH - 20, PIPE_W + 10, 20, cap);

        // Bottom pipe
        DrawRect(p.X + shake.X, shake.Y + botY, PIPE_W, botH, body);
        for (float i = 0; i < botH; i += 12)
            DrawRect(p.X + shake.X, shake.Y + botY + i, PIPE_W, 2, stripe);
        if (botH > 0)
            DrawRect(p.X + shake.X - 5, shake.Y + botY, PIPE_W + 10, 20, cap);
    }


}