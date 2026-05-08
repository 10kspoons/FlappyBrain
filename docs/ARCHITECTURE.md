# Architecture

## High-Level Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        FlappyBrain                              │
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │  Emotiv App  │    │   Keyboard   │    │  Asset Manifest  │  │
│  │  (localhost  │    │   / Gamepad  │    │  (JSON config)   │  │
│  │   :6868 WS)  │    │              │    │                  │  │
│  └──────┬───────┘    └──────┬───────┘    └────────┬─────────┘  │
│         │                  │                     │             │
│  ┌──────▼───────┐    ┌──────▼───────┐    ┌────────▼─────────┐  │
│  │ FlappyBrain  │    │ FlappyBrain  │    │  FlappyBrain     │  │
│  │    .BCI      │    │   .Input     │    │   .Assets        │  │
│  │              │    │              │    │                  │  │
│  │ CortexClient │    │IBirdController    │ IAssetManifest   │  │
│  │ BCIController│    │KeyboardCtrl  │    │ CharacterDef     │  │
│  └──────┬───────┘    └──────┬───────┘    └────────┬─────────┘  │
│         │                  │                     │             │
│         └──────────┬────────┘                    │             │
│                    │                             │             │
│  ┌─────────────────▼─────────────────────────────▼──────────┐  │
│  │                   FlappyBrain.Core                        │  │
│  │                                                           │  │
│  │   GameWorld  ──►  Bird  ──►  Pipe[]  ──►  ScoreTracker   │  │
│  │       │                                                   │  │
│  │   CollisionDetector  ──►  ParallaxSystem  ──►  Particles  │  │
│  └───────────────────────────────┬───────────────────────────┘  │
│                                  │                              │
│  ┌───────────────────────────────▼───────────────────────────┐  │
│  │                  FlappyBrain.Game                         │  │
│  │                                                           │  │
│  │  MonoGame Game  ──►  SceneManager  ──►  Renderer          │  │
│  │       │                                                   │  │
│  │  AudioManager   ──►  AdaptiveMusic  ──►  SFX              │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Projects

### `FlappyBrain.Core`
Pure game logic. No MonoGame dependency. Platform-agnostic. Fully unit-testable.

**Entities:**
- `Bird` — position, velocity, gravity constant, flap impulse, state (alive/dead)
- `Pipe` — x position, gap center Y, gap height, speed, scored flag
- `GameWorld` — owns Bird + Pipe list, drives physics tick, collision detection, scoring

**Systems:**
- `CollisionDetector` — AABB with a small forgiveness margin (makes game feel fair)
- `ParallaxSystem` — tracks scroll offset per layer, wraps at texture-width
- `DustParticleSystem` — simple 2D particle emitter (position, velocity, lifetime, alpha)
- `ScoreTracker` — current score, high score, personal best

**Data:**
- `GameConfig` — gravity, pipe speed, pipe gap, pipe spawn interval, flap impulse (all configurable)
- `GameState` — enum: `Menu | Playing | Dead | Paused`

---

### `FlappyBrain.Input`
Input abstraction — decouples game from both keyboard and BCI.

```csharp
public interface IBirdController
{
    /// Called once per game Update tick.
    /// Returns true if the player wants to flap THIS frame.
    bool ShouldFlap();
}
```

Implementations:
- `KeyboardController` — Space / Up arrow, with 1-frame debounce
- `BCIController` — drains `Channel<FlapEvent>`, returns true when event pending
- `CompositeBirdController` — ORs multiple controllers; keyboard always available as fallback

The game only ever holds an `IBirdController` reference. Swapping from keyboard to BCI is a one-line config change.

---

### `FlappyBrain.BCI`
Emotiv Cortex API integration. Runs on a dedicated background Task.

**`CortexClient`:**
- Connects to `wss://localhost:6868` via `ClientWebSocket`
- Performs JSON-RPC 2.0 handshake: `getCortexInfo` → `requestAccess` → `authorize` → `createSession` → `subscribe`
- Subscribes to `com` (mental commands) stream
- Maps configured action (default: "push") → `FlapEvent` posted to `Channel<FlapEvent>`
- Reconnects automatically on disconnect (exponential backoff, max 30s)

**`BCISignalMonitor`:**
- Tracks current mental command power (0.0–1.0)
- Exposes `SignalQuality` enum: `Poor | Fair | Good | Excellent` (for HUD indicator)
- Based on EEG contact quality from the `met` stream

**Thread model:**
```
Background Task: CortexClient
  └── WebSocket receive loop
        └── Posts FlapEvent → Channel<FlapEvent> (bounded, capacity 10)
              └── BCIController.ShouldFlap() drains on main game thread
```
No locking needed — `Channel<T>` is the thread-safe boundary.

---

### `FlappyBrain.Assets`
Data-driven asset system. No assets are hardcoded in game logic.

**`AssetManifest`** (loaded from `assets/manifest.json`):
```json
{
  "characters": [
    {
      "id": "koala-racer",
      "name": "Dustbucket",
      "spriteSheet": "characters/koala-racer.png",
      "frameWidth": 64,
      "frameHeight": 64,
      "animations": {
        "idle":  { "frames": [0], "fps": 1 },
        "flap":  { "frames": [1, 2, 3], "fps": 24 },
        "dead":  { "frames": [4], "fps": 1 }
      },
      "hitbox": { "x": 8, "y": 8, "w": 48, "h": 48 }
    }
  ],
  "worlds": [
    {
      "id": "outback-apocalypse",
      "name": "The Red Waste",
      "layers": [
        { "texture": "bg/sky-ochre.png",     "scrollMultiplier": 0.1, "tint": "#C4622D" },
        { "texture": "bg/ruins-mid.png",     "scrollMultiplier": 0.3, "tint": "#7A6C5D" },
        { "texture": "bg/desert-floor.png",  "scrollMultiplier": 0.6, "tint": "#B5451B" }
      ],
      "obstacle": {
        "texture": "obstacles/rusty-pipe.png",
        "width": 80,
        "gapMin": 160,
        "gapMax": 220
      },
      "music": "audio/outback-theme.ogg",
      "ambience": "audio/dust-wind.ogg"
    }
  ]
}
```

Adding a new character or world = add an entry to `manifest.json` + drop texture files in `assets/`. Zero code changes.

**`SpriteAnimator`** — frame timer, loops/oneshot modes, driven by animation name string.

---

### `FlappyBrain.Game`
MonoGame entry point. Owns the window, render loop, scene management.

**`SceneManager`** — stack-based:
- `MainMenuScene`
- `GamePlayScene`
- `GameOverScene`
- `TrainingModeScene` *(Phase 3)*
- `SettingsScene` *(Phase 3)*

**`Renderer`** — wraps `SpriteBatch`:
- Renders parallax layers back-to-front
- Renders obstacles
- Renders character (current animation frame)
- Renders particle system
- Renders HUD (score, BCI signal quality dot)
- Debug overlay on F1 (raw BCI power, FPS, hitboxes)

**`AudioManager`:**
- Adaptive music: layered OGG tracks mixed by `MediaPlayer` volume
- Intensity tiers: 0–5 pipes (ambient), 5–15 (building), 15+ (full chaos)
- `SoundEffectInstance` pool for flap/score/death SFX

**`RenderTarget2D`** — game renders at fixed logical resolution (800×600), upscaled to actual window size. Letterboxed if aspect doesn't match. This means the game looks identical regardless of monitor resolution.

---

## Data Flow

```
[Player intent]
      │
      ▼
IBirdController.ShouldFlap()
      │
      ├── KeyboardController (Space/Up)
      └── BCIController ◄── Channel<FlapEvent> ◄── CortexClient (background task)
      │
      ▼
GameWorld.Update(gameTime, shouldFlap)
      │
      ├── Bird.ApplyGravity() + Bird.Flap() if shouldFlap
      ├── Pipe.Scroll(dt) for each pipe
      ├── CollisionDetector.Check(bird, pipes)
      ├── ScoreTracker.Update(pipes)
      └── ParallaxSystem.Scroll(dt)
      │
      ▼
Renderer.Draw(gameWorld, assetManifest)
      │
      ├── Draw parallax layers (back → front)
      ├── Draw pipes (obstacle texture from world config)
      ├── Draw bird (current animation frame from character config)
      ├── Draw particles
      └── Draw HUD
```

---

## Configuration

`appsettings.json`:
```json
{
  "Cortex": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "ProfileName": "flappybrain-profile",
    "ActionMap": { "push": "flap" },
    "PowerThreshold": 0.6
  },
  "Input": {
    "EnableKeyboard": true,
    "EnableBCI": false
  },
  "Game": {
    "ActiveCharacter": "koala-racer",
    "ActiveWorld": "outback-apocalypse",
    "Difficulty": "normal",
    "LogicalWidth": 800,
    "LogicalHeight": 600
  }
}
```

---

## Testing Strategy

`FlappyBrain.Tests` (xUnit):
- `BirdPhysicsTests` — gravity, flap impulse, terminal velocity
- `CollisionDetectorTests` — AABB edge cases, forgiveness margin
- `PipeSpawnTests` — gap Y distribution, minimum edge clearance
- `ScoreTrackerTests` — increment on pipe pass, no double-count
- `ParallaxSystemTests` — scroll wrap at texture boundary
- `BCIControllerTests` — mock channel, ShouldFlap drains correctly
