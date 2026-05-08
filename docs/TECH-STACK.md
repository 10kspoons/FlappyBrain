# Tech Stack

## Language & Runtime

### C# / .NET 8
- Strong typing, great tooling, excellent NuGet ecosystem
- Native Windows builds with `dotnet publish -r win-x64 --self-contained`
- Async/await for non-blocking BCI WebSocket communication
- xUnit for unit tests

---

## Game Engine

### MonoGame
**Why MonoGame over Unity?**

| Concern | MonoGame | Unity |
|---------|----------|-------|
| License | MIT (free forever) | Per-revenue/install fees |
| Bundle size | ~5 MB | 50–200 MB |
| Code-first | Yes — pure C# | No — editor-driven |
| 2D suitability | Excellent | Overkill |
| Transparency | Full source | Black box |
| Startup time | < 1s | 3–10s |

For a 2D arcade game, Unity is a tank where MonoGame is a sports car. No editor, no bloat, no licensing anxiety. Game logic lives entirely in code — easy to reason about, easy to test.

MonoGame provides:
- `Game` base class with a standard `Update()`/`Draw()` loop
- `SpriteBatch` for 2D sprite rendering (hardware-accelerated via DirectX on Windows)
- `SoundEffect` / `Song` for audio
- `Keyboard`, `GamePad` input
- `ContentPipeline` for asset compilation (textures, audio, fonts)

---

## BCI Integration

### Emotiv Cortex API
**Why Cortex API over raw USB HID?**

| Concern | Cortex API | Raw USB |
|---------|-----------|---------|
| Official support | ✅ Yes | ❌ No |
| Headset version changes | Handled automatically | Must reverse-engineer per firmware |
| Auth / licensing | Built-in | N/A |
| Mental command training | Managed by Emotiv App | Must implement from scratch |
| Signal processing | Done by Emotiv App | Must implement DSP pipeline |
| Cross-headset compat | Epoc, Epoc X, Flex, Insight | Model-specific only |

The Cortex API is a **WebSocket JSON-RPC 2.0 interface** served by the Emotiv App on `wss://localhost:6868`. It provides:
- Authentication (OAuth2-style with client ID + secret)
- Session management (headset connection, EEG data streaming)
- `com` (mental commands) event stream — the primary control input
- `fac` (facial expressions) event stream — blink fallback
- `met` (performance metrics) stream — focus, stress, relaxation
- Profile management — load/save trained mental command profiles

### Emotiv Cortex Client (`FlappyBrain.BCI`)
- `System.Net.WebSockets.ClientWebSocket` — native .NET WebSocket, no extra dependency
- JSON serialization: `System.Text.Json`
- Runs on a dedicated background `Task` with `CancellationToken`
- Posts `FlapEvent` objects to a `Channel<FlapEvent>` (thread-safe queue)
- Game loop reads from the channel on the main thread — zero locking

---

## Input Abstraction

### `IBirdController` interface
```csharp
public interface IBirdController
{
    bool ShouldFlap();  // called once per game update tick
}
```

Implementations:
- `KeyboardController` — reads `Keyboard.GetState()`, triggers on Space/Up
- `BCIController` — drains the `Channel<FlapEvent>` queue, returns true when event pending
- `CompositeBirdController` — ORs multiple controllers (keyboard always active as fallback)

This means BCI and keyboard are hot-swappable at runtime. No game logic knows or cares how the flap was triggered.

---

## Visual Assets

### Rendering
- MonoGame `SpriteBatch` with 4 parallax layers
- Pixel art style, 480×854 (portrait 9:16) logical resolution, upscaled to window
- `RenderTarget2D` for fixed-res rendering regardless of window size

### Parallax Background System
- Layer 0 (scroll 0.1×): Sky — burnt ochre gradient, dust haze, distant sandstone formations, dead gum tree silhouettes
- Layer 1 (scroll 0.3×): Mid-ground ruins — rusted corrugated iron shacks, water towers, mining equipment
- Layer 2 (scroll 0.6×): Desert floor — cracked red earth, dead spinifex, scattered debris and junk
- Layer 3 (scroll 1.0×): Foreground obstacles — rusty industrial conduit pipes

Each layer is a seamlessly tiling horizontal texture. Scroll offset tracked per-layer as a float, wrapped at texture width.

### Atmosphere
- Dust particle system: 50–100 particles, slow diagonal drift, fade in/out
- Heat haze: subtle sine-wave distortion shader on Layer 0 and 1 (optional, toggleable for performance)

### Color Palette
```
Sky:        #C4622D (burnt ochre), #8B3A2E (deep rust), #E8A87C (haze)
Earth:      #B5451B (red dust), #7A3520 (dark earth), #D4956A (cracked clay)
Metal:      #7A6C5D (corroded iron), #5C4A3A (rust shadow), #A89880 (weathered silver)
Accent:     #4A3728 (deep shadow), #F2D5A0 (bleached bone), #6B8E6B (dead gum)
Sky accent: #5C3D6B (dusk purple), #E8714A (sunset orange)
```

---

## Configuration

### `appsettings.json`
```json
{
  "Cortex": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "ProfileName": "your-training-profile",
    "ActionMap": {
      "push": "flap"
    },
    "PowerThreshold": 0.6
  },
  "Input": {
    "EnableKeyboard": true,
    "EnableBCI": true
  },
  "Game": {
    "Difficulty": "normal"
  }
}
```

---

## Build & CI

- `dotnet build` — standard .NET build
- `dotnet test` — xUnit test runner
- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` — single .exe output
- GitHub Actions: build + test on every push to `main`

---

## Dependencies Summary

| Package | Version | Purpose |
|---------|---------|---------|
| `MonoGame.Framework.DesktopGL` | 3.8.x | Game engine (use WindowsDX for Windows-only) |
| `System.Text.Json` | (built-in .NET 8) | JSON-RPC serialization |
| `Microsoft.Extensions.Configuration` | 8.x | appsettings.json config |
| `xunit` | 2.x | Unit testing |
| `xunit.runner.visualstudio` | 2.x | VS test runner integration |
