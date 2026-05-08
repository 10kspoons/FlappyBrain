# Emotiv Epoc X Setup Guide

## Required Hardware
- Emotiv Epoc X headset
- USB receiver dongle (included with headset)

## Required Software
1. **Emotiv App** — https://www.emotiv.com/emotiv-app/ (free download)
2. **Emotiv Launcher** — bundled with Emotiv App
3. Optional: **BrainViz** — for visualising raw EEG

## Getting Cortex API Credentials

1. Go to https://emotiv.com/developer
2. Sign in or create an account
3. Create a new application:
   - Name: `FlappyBrain`
   - Redirect URI: `https://localhost` (placeholder)
4. Note your **Client ID** and **Client Secret**
5. Add them to `src/FlappyBrain.Game/appsettings.json`:

```json
{
  "Cortex": {
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
    "ProfileName": "flappybrain",
    "ActionMap": { "push": "flap" },
    "PowerThreshold": 0.6
  }
}
```

## Connecting the Headset

1. Plug in the USB dongle
2. Open **Emotiv Launcher** and then **Emotiv App**
3. Power on the Epoc X (slide switch on left side)
4. Emotiv App should detect the headset automatically
5. Fit the headset — wet all 14 electrodes with saline solution
6. Wait for all electrode indicators to turn green (or at least yellow)

## Training Mental Commands

The game uses the "push" mental command. You need to train it before BCI control will work.

### Training Process

1. In **Emotiv App**, go to **Mental Commands** training
2. Select or create a profile named to match your `appsettings.json` `ProfileName`
3. Train the **neutral** state first (do nothing, relax — 8 seconds)
4. Train the **push** command:
   - Imagine pushing something away from you forcefully
   - Or visualise a physical pushing motion
   - Or whatever mental imagery reliably produces a different brain state
   - 8 seconds per session
5. Complete **5 training sessions** for reliable detection

### Recommended Training Tips
- Train in the same physical position you'll use when playing
- Be consistent with your mental imagery across sessions
- Neutral training is as important as push training
- If detection is unreliable, delete all sessions and retrain from scratch
- After training, save the profile in Emotiv App

## Running in BCI Mode

1. Start **Emotiv App** (must be running — it hosts the Cortex WebSocket on `wss://localhost:6868`)
2. Connect headset, ensure good signal quality
3. Load your training profile in Emotiv App
4. Start FlappyBrain: `dotnet run --project src/FlappyBrain.Game`
5. The game will connect to Cortex automatically
6. Check the BCI signal dot (top-right HUD) — should be green
7. Press F1 to open the debug overlay and verify "push" events are firing

## Adjusting Sensitivity

If flaps are triggering too easily or not enough:
- Open Settings in the game
- Adjust the **BCI Threshold** slider (0.0–1.0)
- Lower = more sensitive (more false positives)
- Higher = less sensitive (may miss genuine push commands)
- Default: 0.6

## Troubleshooting

### Headset not detected
- Ensure USB dongle is plugged in
- Restart Emotiv App
- Try a different USB port
- Check Windows Device Manager for the dongle

### Cortex API auth failures
- Verify Client ID and Secret in `appsettings.json`
- Ensure you've accepted the Cortex API licence in Emotiv App (first run popup)
- Check Emotiv App is running (not just Launcher)

### Poor signal quality / red electrodes
- Re-wet electrodes with saline solution
- Adjust headset fit — electrodes need firm scalp contact
- Ensure hair is not blocking electrode contact
- Wait 2–3 minutes after fitting for impedance to stabilise

### Push command not detected
- Verify profile is loaded in Emotiv App
- Check BCI debug overlay (F1) — is the power bar moving at all when you "push"?
- If no movement: EEG signal issue (electrode contact)
- If power moves but doesn't cross threshold: lower the threshold slider
- If constantly triggering: raise the threshold slider, or retrain neutral state

### Game connects then immediately disconnects
- Usually an auth issue — check Client ID/Secret
- Try: in Emotiv App, revoke and re-grant access to your application
