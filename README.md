# 🧠 FlappyBrain

> *Flappy Bird. But your brain is the controller.*

A Windows desktop game inspired by Flappy Bird, controlled by the **Emotiv Epoc X** BCI headset. Think "push" — your koala pilot flaps. Keyboard fallback included for dev/testing when you don't want electrodes on your head at 9am.

---

![C#](https://img.shields.io/badge/C%23-.NET%208-purple?style=flat-square&logo=dotnet)
![MonoGame](https://img.shields.io/badge/Engine-MonoGame-blue?style=flat-square)
![Emotiv](https://img.shields.io/badge/BCI-Emotiv%20Epoc%20X-green?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey?style=flat-square&logo=windows)

---

## 🎮 Gameplay

You are a **koala pilot** aboard a cobbled-together flying machine, navigating the post-apocalyptic Australian outback. Red dust. Rusted iron shacks. Dead gum trees. The kind of landscape where you build your aircraft from scrap and fly it with your mind.

Navigate through rusty corrugated-iron pipes. Don't crash. Try to beat your score.

---

## 🧠 Controls

| Method | Action | Notes |
|--------|--------|-------|
| Emotiv Epoc X — "push" command | Flap | Requires training profile |
| Emotiv Epoc X — blink | Flap (fallback) | Configurable in settings |
| `Space` / `↑` | Flap | Keyboard fallback for dev/testing |
| `F1` | Toggle BCI debug overlay | Shows raw mental command power |
| `Esc` | Pause / Main Menu | — |

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Emotiv App](https://www.emotiv.com/emotiv-app/) + Epoc X headset *(optional — keyboard works without it)*

### Run (keyboard mode)
```bash
git clone https://github.com/10kspoons/FlappyBrain.git
cd FlappyBrain
dotnet run --project src/FlappyBrain.Game
```

### Run (BCI mode)
1. Start the Emotiv App and connect your Epoc X headset
2. Configure your credentials in `src/FlappyBrain.Game/appsettings.json`
3. `dotnet run --project src/FlappyBrain.Game`

See [`docs/EMOTIV-SETUP.md`](docs/EMOTIV-SETUP.md) for full headset setup instructions.

---

## 📁 Project Structure

```
FlappyBrain/
├── src/
│   ├── FlappyBrain.Core/       # Game logic, entities, physics
│   ├── FlappyBrain.Game/       # MonoGame entry point + renderer
│   ├── FlappyBrain.BCI/        # Emotiv Cortex WebSocket client
│   └── FlappyBrain.Input/      # Input abstraction (IBirdController)
├── assets/                     # Sprites, audio, tilesets
├── docs/                       # Architecture, design, setup docs
└── tests/                      # xUnit unit tests
```

---

## 📖 Documentation

| Doc | Description |
|-----|-------------|
| [`docs/TECH-STACK.md`](docs/TECH-STACK.md) | Technology choices and rationale |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | System architecture and data flow |
| [`docs/DESIGN.md`](docs/DESIGN.md) | Game design document |
| [`docs/IMPLEMENTATION-PLAN.md`](docs/IMPLEMENTATION-PLAN.md) | Phased build plan |
| [`docs/EMOTIV-SETUP.md`](docs/EMOTIV-SETUP.md) | Headset setup and training guide |

---

## 🏜️ World

The game is set in a **post-apocalyptic Australian desert** — burnt ochre sky, crumbling sandstone formations, rusted corrugated iron ruins, dead spinifex. The parallax scrolling background layers the world at depth: distant rock formations, mid-ground ruins, cracked desert floor. Heat haze shimmers on the horizon.

The "pipes" aren't Nintendo green — they're rusty industrial conduit sunk into the red earth.

---

## 🐨 Characters

A family of **koala pilots** flying improvised scrap-metal aircraft. Post-apocalyptic aviator gear, goggles, leather, the occasional eyepatch. Each character unlocks with score milestones.

---

## License

MIT
