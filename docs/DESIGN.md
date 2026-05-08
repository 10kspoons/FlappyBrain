# Game Design Document

## Concept

**FlappyBrain** is a side-scrolling arcade game in the tradition of Flappy Bird, set in a post-apocalyptic Australian desert. You pilot a koala aviator through a wasteland of rusted industrial obstacles using the power of your mind — or a keyboard if your neurons aren't cooperating.

---

## Core Loop

```
Tap / Think "push"
       ↓
   Bird flaps upward
       ↓
   Gravity pulls down
       ↓
Navigate gap between pipes
       ↓
    Score +1
       ↓
  Repeat, faster
```

Physics constants (normal difficulty):
- Gravity: `0.35` units/tick²
- Flap impulse: `-7.5` units/tick (upward)
- Terminal velocity: `+12` units/tick (downward)
- Pipe scroll speed: `180` px/sec, +5 px/sec per 10 score
- Pipe gap: `200px` at score 0, narrowing to `150px` at score 30

---

## BCI Interaction Design

### Primary Control: Mental Command "push"
The Emotiv Cortex API's `com` stream fires a `push` event when the trained mental command is detected above a configurable power threshold.

- **Threshold**: 0.0–1.0 slider in Settings. Default: 0.6.
  - Lower = more sensitive (risk of accidental flaps)
  - Higher = less sensitive (risk of missed flaps)
- **Latency**: typical Cortex API event latency is 50–150ms — acceptable for a casual game
- **Training**: 5 sessions recommended for reliable detection. See `EMOTIV-SETUP.md`.

### Fallback: Blink Detection
If mental commands aren't trained or aren't working, `fac` (facial expression) blink events can trigger a flap. Configurable in `appsettings.json`.

### BCI Debug Overlay (F1)
Shows in-game without pausing:
- Raw `push` command power (0.00–1.00), live bar graph
- Current threshold line
- Event log: last 5 detected flap events with timestamps
- Headset contact quality per electrode (green/yellow/red)
- Effective BCI latency (ms, rolling average)

### HUD Signal Indicator
Top-right corner: coloured dot showing headset signal quality:
- 🟢 Green — all contacts good, BCI reliable
- 🟡 Yellow — some poor contacts, BCI may miss inputs  
- 🔴 Red — poor signal, fall back to keyboard recommended
- ⚫ Grey — Cortex not connected / keyboard-only mode

---

## Flexible Asset Design

Characters, backgrounds, and obstacles are entirely data-driven. The game reads `assets/manifest.json` at startup. **No code changes are needed to add or swap characters, worlds, or obstacles.**

### Character Design Contract
Each character needs:
- A sprite sheet PNG (transparent background)
- Frame dimensions (e.g. 64×64)
- Animation frames for: `idle`, `flap`, `dead`
- A hitbox definition (usually slightly smaller than the visible sprite for fair feel)

Characters will evolve rapidly. The engine doesn't care — swap the PNG and update the JSON.

### World / Background Design Contract
Each world needs:
- 2–4 parallax layer textures (seamlessly tiling, any width)
- An obstacle texture (tiling vertical strip, any width)
- Background music (OGG) + optional ambience loop (OGG)

---

## Visual Design

### Current World: The Red Waste
An Australian desert wasteland, post-apocalypse, brutally alive with decay and dust.

**Parallax Layers (back → front):**

| Layer | Speed | Content |
|-------|-------|---------|
| Sky | 0.1× | Vast ochre/rust sky, heat haze shimmer, dust on the horizon, distant crumbling sandstone formations, dead gum tree silhouettes |
| Mid ruins | 0.3× | Rusted corrugated iron shacks, abandoned mining derricks, old water towers, 44-gallon drum piles |
| Desert floor | 0.6× | Cracked red earth, dead spinifex clumps, scattered junk, tyre tracks, rusted hulks |
| Obstacles | 1.0× | Rusty industrial conduit pipes, vertically erupting from the ground |

**Colour Palette:**
```
Sky:      #C4622D  #8B3A2E  #E8A87C  #5C3D6B (dusk purple accent)
Earth:    #B5451B  #7A3520  #D4956A
Metal:    #7A6C5D  #5C4A3A  #A89880
Bright:   #F2D5A0 (bleached bone)  #6B8E6B (dead gum)
```

**Atmosphere:**
- 60–80 dust particles: slow diagonal drift (right-to-left + slight downward), alpha 0.3–0.6, soft circular sprites
- Heat haze: optional sine-wave vertex distortion on sky and mid layers (toggle in settings for lower-end PCs)
- Day/night cycle consideration: could offer `dawn`, `midday`, `dusk` variants of the sky layer

**Pipes / Obstacles:**
- NOT green Nintendo pipes
- Rusty corrugated iron shafts / industrial conduit
- Metal caps top and bottom
- Texture: scratched, pitted, streaked with rust and grease
- Slightly different widths and rust patterns randomised per spawn

### Current Character: Koala Pilots
Referencing the concept art — rugged, scrappy koala aviators in cobbled-together flying machines. Post-apocalyptic gear: leather, goggles, scarves, eyepatches.

Character roster (unlocked by score milestones):
1. **Dustbucket** (default) — compact hover-racer, grey-silver, aviator goggles
2. **The Rocket** (score 20) — elongated missile-plane, speedster energy
3. **One-Eye** (score 50) — eyepatch + battle flag, rogue pilot

Future characters added via manifest only — no code changes.

---

## Audio Design

### Mad Max Vibe
The score should feel like something played by the Doof Warrior while riding a scrap-metal truck through a dust storm.

**Main Theme (gameplay loop):**
- Driving tribal war drums — heavy, relentless
- Distorted electric guitar drones — not melodic, textural
- Underlying didgeridoo pulse — Australian roots, unsettling
- Industrial percussion hits on strong beats — chains, metal sheets, struck pipes
- Adaptive layers: more instruments and intensity join as score climbs

**Intensity Tiers:**
| Score | Tier | Musical Character |
|-------|------|------------------|
| 0–5 | Calm | Sparse drums, lone guitar drone, wind ambience |
| 6–15 | Building | Full drum pattern enters, bass growl, tension |
| 16–30 | Charged | Everything in, faster tempo feel, guitar more aggressive |
| 31+ | Full Send | Layered chaos, extra percussion, like a war convoy |

**Ambience (looped):**
- Dust storm wind (constant, low)
- Distant metal creaking
- Occasional crow call (distant, rare)

**Sound Effects:**
- Flap: short mechanical whirr / wing-beat
- Score: quick metallic ping
- Death: jarring metallic crash + silence (let the ambience fill the void)
- Menu navigation: dry metal click

**Implementation:**
- Adaptive music via independent OGG track layers (one per intensity tier)
- `MediaPlayer` crossfade on tier change (1.5s blend)
- SFX via `SoundEffectInstance` pool

---

## Scenes

```
MainMenuScene
    │
    ├──► GamePlayScene ──► GameOverScene
    │                           │
    ├──► SettingsScene          └──► GamePlayScene (restart)
    │
    └──► TrainingModeScene (BCI calibration)
```

### MainMenuScene
- Parallax background scrolling (no obstacles)
- Character bobbing idle animation
- Atmospheric ambience playing
- Menu items: Play · Settings · Train BCI · Quit
- High score displayed

### GamePlayScene
- Full gameplay loop
- HUD: score (top centre), BCI signal dot (top right), debug overlay (F1)

### GameOverScene
- Brief death animation (character spins/falls)
- Score + best score
- Metallic crash → silence → ambience resumes
- Options: Retry (Space / Enter) · Main Menu (Esc)

### SettingsScene
- BCI power threshold slider
- Enable/disable keyboard fallback
- Character select (unlocked characters)
- World select
- Volume controls (music, SFX, ambience)
- Heat haze toggle

### TrainingModeScene *(Phase 3)*
- No gameplay — pure BCI calibration
- Live mental command power bar
- Prompts: "Think PUSH now" with countdown
- 5-session training recorder
- Export profile to Emotiv App

---

## Difficulty Scaling

| Parameter | Score 0 | Score 15 | Score 30 | Score 50 |
|-----------|---------|---------|---------|---------|
| Pipe speed (px/sec) | 180 | 200 | 230 | 270 |
| Pipe gap (px) | 200 | 185 | 165 | 150 |
| Pipe spawn interval (sec) | 2.2 | 2.0 | 1.8 | 1.6 |

Difficulty scales continuously, not in steps. Formulae in `GameConfig`.

---

## HUD Layout

```
┌─────────────────────────────────────────────┐
│  [Score: 42]                       [🟢 BCI]  │
│                                              │
│                  [BIRD]                      │
│                                              │
│        [PIPE▓▓▓]      [PIPE▓▓▓]             │
│                                              │
│──────────────────────── dust particles ─────│
└─────────────────────────────────────────────┘
```

Score: pixel font, top-centre, drop shadow
BCI dot: 16×16 coloured circle, top-right, with subtle pulse animation when active
