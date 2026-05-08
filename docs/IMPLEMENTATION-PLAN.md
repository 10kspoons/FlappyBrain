# Implementation Plan

## Overview

4 phases over ~11 days. Each phase is shippable — Phase 1 delivers a fully playable keyboard game, Phase 2 adds BCI, Phase 3 adds polish, Phase 4 ships.

---

## Phase 0 — Project Setup (Day 1)

**Goal:** Empty window that opens, renders a black screen, and closes cleanly.

- [ ] Create .NET 8 solution: `FlappyBrain.sln`
- [ ] Create projects: `FlappyBrain.Game`, `FlappyBrain.Core`, `FlappyBrain.Input`, `FlappyBrain.BCI`, `FlappyBrain.Assets`, `FlappyBrain.Tests`
- [ ] Add MonoGame.Framework.DesktopGL NuGet to `FlappyBrain.Game`
- [ ] Implement `Program.cs` entry point
- [ ] Stub `IBirdController` interface in `FlappyBrain.Input`
- [ ] Implement `KeyboardController` (Space/Up → flap)
- [ ] Implement basic `GameConfig` with defaults
- [ ] Configure `.gitignore`, `.editorconfig`, `Directory.Build.props`
- [ ] Verify: `dotnet run --project src/FlappyBrain.Game` opens a window

**Definition of Done:** Window opens, shows background colour, closes on Escape. No exceptions.

---

## Phase 1 — Core Game (Days 2–4)

**Goal:** Fully playable Flappy Bird with keyboard. All core mechanics working.

### Day 2 — Bird + Physics
- [ ] `Bird` entity: position (Vector2), velocity (float), gravity constant
- [ ] `Bird.Flap()` — sets velocity to flap impulse
- [ ] `Bird.Update(dt)` — apply gravity, clamp to terminal velocity, move
- [ ] Render bird as coloured rectangle (placeholder, 40×40px)
- [ ] Bird starts at x=200, y=300; falls under gravity
- [ ] Press Space → bird flaps upward
- [ ] Bird dies if it hits top or bottom of screen

### Day 3 — Pipes + Scroll
- [ ] `Pipe` entity: x position, gap centre Y, gap height, speed
- [ ] `Pipe.Update(dt)` — scroll left at configured speed
- [ ] `PipeSpawner` — spawns pipe every N seconds at right edge, random gap Y
- [ ] Render pipes as coloured rectangles (top + bottom segments)
- [ ] Pipes despawn when off-screen left

### Day 4 — Collision + Score + Game States
- [ ] `CollisionDetector.Check(bird, pipes)` — AABB with 4px forgiveness margin
- [ ] `ScoreTracker` — increment when bird passes a pipe's x position (once per pipe)
- [ ] `GameState` enum: `Menu | Playing | Dead`
- [ ] `MainMenuScene` — press Space to start
- [ ] `GameOverScene` — show score, press Space to restart
- [ ] Window title shows current score (placeholder until SpriteFont)
- [ ] Difficulty scaling: pipe speed increases every 10 points

**Definition of Done:** Full Flappy Bird loop playable with keyboard. Score increments. Death works. Restart works. Plays indefinitely.

---

## Phase 1.5 — Visual Prototype (Day 5)

**Goal:** Correct art direction in-engine, even if hand-drawn/generated assets.

- [ ] Create 1×1 white pixel texture (generated in code — no content pipeline needed)
- [ ] Implement parallax system: 3 layers, different scroll speeds
- [ ] Render layered colour bands for background (ochre sky, rust mid, red earth)
- [ ] Add `DustParticleSystem` — 60 particles, diagonal drift, alpha fade
- [ ] Render pipes as rust-orange rather than green rectangles
- [ ] Asset manifest JSON structure created (even if pointing at placeholder textures)
- [ ] Load character config from manifest (even if still rendering rectangles)

**Definition of Done:** Game looks like it's set in the right world, even without final art.

---

## Phase 2 — Emotiv BCI Integration (Days 6–8)

**Goal:** Game controllable by "push" mental command.

### Day 6 — Cortex Client
- [ ] `CortexClient` WebSocket connection to `wss://localhost:6868`
- [ ] JSON-RPC 2.0 request/response framework (request id tracking)
- [ ] Auth flow: `getCortexInfo` → `requestAccess` → `authorize`
- [ ] Session: `queryHeadsets` → `createSession` → `updateSession(activate)`
- [ ] Profile: `queryProfile` → `loadGuestProfile` / `loadProfile`
- [ ] Subscribe to `com` stream
- [ ] Graceful disconnect + reconnect (exponential backoff)

### Day 7 — BCI Controller
- [ ] `BCIController` implements `IBirdController`
- [ ] Drains `Channel<FlapEvent>` queue from `CortexClient`
- [ ] `CompositeBirdController` — ORs keyboard + BCI
- [ ] `BCISignalMonitor` — tracks power, reports `SignalQuality`
- [ ] Add `Cortex` section to `appsettings.json`

### Day 8 — HUD Integration
- [ ] BCI signal quality dot in HUD (top-right)
- [ ] BCI debug overlay (F1 toggle): power bar, threshold line, event log, electrode map
- [ ] Settings: threshold slider (keyboard navigation)
- [ ] Blink detection fallback via `fac` stream

**Definition of Done:** Game controllable by headset. Signal quality visible. Keyboard still works.

---

## Phase 3 — Polish, Audio & Training Mode (Days 9–10)

**Goal:** Game feels finished. Looks and sounds great.

### Visual Polish
- [ ] Replace placeholder rectangles with sprite assets (character + pipe textures)
- [ ] `SpriteAnimator` for character flap animation
- [ ] Score rendered with pixel font (SpriteBatch bitmap font or MonoGame SpriteFont)
- [ ] Main menu with animated background + character idle
- [ ] Death animation (character tumbles/spins)
- [ ] Screen shake on death (0.3s, 4px amplitude)
- [ ] Heat haze shader on sky layer (optional, toggleable)

### Audio
- [ ] `AudioManager` with adaptive music layers (OGG tracks via `MediaPlayer`)
- [ ] Intensity tiers: calm → building → charged → full-send
- [ ] SFX: flap, score ding, death crash
- [ ] Wind ambience loop
- [ ] Volume settings (music, SFX, ambience)

### Training Mode
- [ ] `TrainingModeScene`: live Cortex power bar, "think PUSH now" prompts
- [ ] 5-session training recorder with progress indicator
- [ ] Profile save/load via Cortex API

### High Scores
- [ ] Local high score board (top 10) saved to `%APPDATA%/FlappyBrain/scores.json`
- [ ] Game Over screen shows rank

### Character / World Select
- [ ] Settings screen: character select (sprite preview, locked state)
- [ ] World select
- [ ] Unlock milestones: Rocket at score 20, One-Eye at score 50

**Definition of Done:** Complete game feel. Everything looks and sounds intentional.

---

## Phase 4 — Release (Day 11)

**Goal:** Ship a v1.0.0 Windows build.

- [ ] `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`
- [ ] Test on clean Windows install (no .NET SDK)
- [ ] README screenshots + 30s gameplay GIF
- [ ] GitHub Actions workflow: build + test on push to `main`
- [ ] `v1.0.0` release tag + GitHub Release with attached `.exe` zip
- [ ] MSIX installer (optional, for cleaner install experience)

**Definition of Done:** Tagged release. Downloadable .exe runs on Windows. CI green.

---

## Asset Pipeline Notes

**No Content Pipeline required for prototype:**
All placeholder assets are generated in code (1×1 white pixel, coloured rectangles). This means zero build friction and no MGCB tool dependency in Phase 0–1.

**Introducing real assets (Phase 3):**
- Add `FlappyBrain.Game.Content.mgcb` project file
- Register textures and fonts
- `dotnet mgcb-editor` for content management
- Or: load PNG directly via `Texture2D.FromFile()` (simpler, no pipeline needed for prototype-quality assets)

**Recommended: load textures directly from disk** in development. Only use the content pipeline for final optimised builds.
