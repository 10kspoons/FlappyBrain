using System;
using System.Collections.Generic;
using FlappyBrain.BCI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FlappyBrain.Scenes;

/// <summary>
/// In-app Emotiv Cortex training flow. Replaces the need to use the EmotivBCI
/// desktop app before play. Drives the user through four phases:
///   0) Headset contact-quality check
///   1) Neutral baseline (10s)
///   2) Lift command training (8 reps, with retry-on-reject)
///   3) Complete — "Play Now" / "Retrain"
/// The user may bail out at any time via the "Skip (use keyboard)" link.
/// </summary>
public sealed class TrainingScene
{
    public enum Phase { HeadsetCheck, NeutralBaseline, LiftTraining, Complete, Skipped }

    public enum Outcome { InProgress, Trained, Skipped }

    public Phase CurrentPhase { get; private set; } = Phase.HeadsetCheck;
    public Outcome Result { get; private set; } = Outcome.InProgress;
    public bool IsComplete => Result != Outcome.InProgress;

    // Tunables — LogW is settable so fullscreen can widen the logical canvas to match display ratio
    public static int LogW = 800;
    const int LogH = 600;
    const float NEUTRAL_DURATION = 10f;
    const int TOTAL_REPS = 8;
    const float REP_COUNTDOWN = 3f;       // 3..2..1 before the "IMAGINE" flash
    const float REP_HOLD = 1f;            // hold the prompt
    const float REP_RELAX = 2f;           // breather between reps
    const int MIN_GOOD_CONTACTS = 10;     // of 14 electrodes

    // Visual constants — match existing game aesthetic
    static readonly Color SkyTop  = new Color(0x4A, 0x2A, 0x1A);
    static readonly Color SkyBot  = new Color(0xC4, 0x62, 0x2D);
    static readonly Color Accent  = new Color(0xE8, 0xC4, 0x40); // gold (bird colour)
    static readonly Color Good    = new Color(0x40, 0xE0, 0x70);
    static readonly Color Fair    = new Color(0xE8, 0xC4, 0x40);
    static readonly Color Bad     = new Color(0xE0, 0x40, 0x40);
    static readonly Color Muted   = new Color(180, 180, 180);

    // 14 Epoc X electrodes laid out roughly on a head silhouette.
    // (Front of head at top.) Coordinates are normalized within the head box.
    static readonly (string Name, float X, float Y)[] Electrodes =
    {
        ("AF3", 0.38f, 0.10f), ("AF4", 0.62f, 0.10f),
        ("F7",  0.18f, 0.22f), ("F3",  0.38f, 0.22f),
        ("F4",  0.62f, 0.22f), ("F8",  0.82f, 0.22f),
        ("FC5", 0.25f, 0.36f), ("FC6", 0.75f, 0.36f),
        ("T7",  0.10f, 0.50f), ("T8",  0.90f, 0.50f),
        ("P7",  0.22f, 0.72f), ("P8",  0.78f, 0.72f),
        ("O1",  0.40f, 0.88f), ("O2",  0.60f, 0.88f),
    };

    readonly CortexClient? _cortex;
    readonly Action<float, float, float, float, Color> _drawRect;
    readonly Action<float, float, float, Color> _drawCircle;
    readonly DrawTextFn _drawTextFn;

    public delegate void DrawTextFn(string text, float x, float y, int sizePx, Color c, bool centered, bool outline);

    // Phase state
    float _phaseTimer;
    int _currentRep;
    int _consecutiveRejects;
    float _repTimer;
    RepState _repState = RepState.Countdown;
    bool _waitingForCortexResult;
    KeyboardState _prevKb;
    string _statusMessage = "";

    // "Continue" / "Skip" button hot rects (kept simple — keyboard activated)
    bool _continueAvailable;

    enum RepState { Countdown, Prompt, Relax, AwaitingResult, Accepted, Rejected }

    public TrainingScene(
        CortexClient? cortex,
        DrawTextFn drawText,
        Action<float, float, float, float, Color> drawRect,
        Action<float, float, float, Color> drawCircle)
    {
        _cortex = cortex;
        _drawTextFn = drawText;
        _drawRect = drawRect;
        _drawCircle = drawCircle;

        if (_cortex != null)
        {
            _cortex.OnTrainingSucceeded += HandleTrainingSucceeded;
            _cortex.OnTrainingFailed += HandleTrainingFailed;
        }
    }

    public void Detach()
    {
        if (_cortex != null)
        {
            _cortex.OnTrainingSucceeded -= HandleTrainingSucceeded;
            _cortex.OnTrainingFailed -= HandleTrainingFailed;
        }
    }

    void HandleTrainingSucceeded(string action)
    {
        if (CurrentPhase != Phase.LiftTraining && CurrentPhase != Phase.NeutralBaseline) return;
        if (!_waitingForCortexResult) return;
        _waitingForCortexResult = false;
        _consecutiveRejects = 0;

        // Auto-accept the rep so Cortex stores it, then advance.
        try { _ = _cortex?.AcceptTrainingAsync(action); } catch { }

        _repState = RepState.Accepted;
        _repTimer = 0;
    }

    void HandleTrainingFailed(string action, string message)
    {
        if (CurrentPhase != Phase.LiftTraining && CurrentPhase != Phase.NeutralBaseline) return;
        if (!_waitingForCortexResult) return;
        _waitingForCortexResult = false;
        _consecutiveRejects++;

        try { _ = _cortex?.RejectTrainingAsync(action); } catch { }

        _repState = RepState.Rejected;
        _repTimer = 0;
    }

    public void Update(float dt, KeyboardState kb)
    {
        bool spacePressed = kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space);
        bool enterPressed = kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter);
        bool skipPressed  = kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S);
        bool retrainPressed = kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R);
        bool retryPressed  = kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab);

        // Universal: S = skip training (use keyboard)
        if (skipPressed && CurrentPhase != Phase.Complete)
        {
            Result = Outcome.Skipped;
            CurrentPhase = Phase.Skipped;
            _prevKb = kb;
            return;
        }

        _phaseTimer += dt;

        switch (CurrentPhase)
        {
            case Phase.HeadsetCheck:
                UpdateHeadsetCheck(spacePressed || enterPressed, retryPressed);
                break;
            case Phase.NeutralBaseline:
                UpdateNeutralBaseline(dt);
                break;
            case Phase.LiftTraining:
                UpdateLiftTraining(dt);
                break;
            case Phase.Complete:
                if (spacePressed || enterPressed)
                {
                    Result = Outcome.Trained;
                }
                else if (retrainPressed)
                {
                    // Restart at Phase 1.
                    EnterPhase(Phase.NeutralBaseline);
                }
                break;
        }

        _prevKb = kb;
    }

    void UpdateHeadsetCheck(bool continuePressed, bool retryPressed)
    {
        if (_cortex == null || !_cortex.IsConnected)
        {
            _statusMessage = "Connect Emotiv headset and launch Cortex first";
            _continueAvailable = false;
            // Retry: the CortexClient auto-reconnects, but pressing R re-evaluates immediately.
            if (retryPressed) { _statusMessage = "Retrying..."; }
            return;
        }

        int goodCount = CountGoodElectrodes();
        _continueAvailable = goodCount >= MIN_GOOD_CONTACTS;
        _statusMessage = _continueAvailable
            ? $"{goodCount}/14 electrodes ready — press SPACE to continue"
            : $"{goodCount}/14 electrodes ready — adjust until green";

        if (continuePressed && _continueAvailable)
        {
            EnterPhase(Phase.NeutralBaseline);
        }
    }

    void UpdateNeutralBaseline(float dt)
    {
        if (_phaseTimer < 0.2f && !_waitingForCortexResult)
        {
            try { _ = _cortex?.StartTrainingAsync("neutral"); } catch { }
            _waitingForCortexResult = true;
        }
        if (_phaseTimer >= NEUTRAL_DURATION)
        {
            // If Cortex hasn't fired an event by now, auto-accept and move on so the booth flow doesn't stall.
            if (_waitingForCortexResult)
            {
                try { _ = _cortex?.AcceptTrainingAsync("neutral"); } catch { }
                _waitingForCortexResult = false;
            }
            EnterPhase(Phase.LiftTraining);
        }
    }

    void UpdateLiftTraining(float dt)
    {
        _repTimer += dt;

        switch (_repState)
        {
            case RepState.Countdown:
                if (_repTimer >= REP_COUNTDOWN)
                {
                    try { _ = _cortex?.StartTrainingAsync("lift"); } catch { }
                    _waitingForCortexResult = true;
                    _repState = RepState.Prompt;
                    _repTimer = 0;
                }
                break;

            case RepState.Prompt:
                if (_repTimer >= REP_HOLD)
                {
                    _repState = RepState.Relax;
                    _repTimer = 0;
                }
                break;

            case RepState.Relax:
                if (_repTimer >= REP_RELAX)
                {
                    // If Cortex hasn't responded by the end of relax, treat it as accepted
                    // (booth fallback — better than blocking forever with no headset).
                    if (_waitingForCortexResult)
                    {
                        try { _ = _cortex?.AcceptTrainingAsync("lift"); } catch { }
                        _waitingForCortexResult = false;
                        _repState = RepState.Accepted;
                        _repTimer = 0;
                    }
                    else
                    {
                        // Wait for AwaitingResult transition driven by the event handler.
                        _repState = RepState.AwaitingResult;
                        _repTimer = 0;
                    }
                }
                break;

            case RepState.AwaitingResult:
                if (_repTimer >= 4f)
                {
                    // Timeout — accept and move on rather than blocking.
                    try { _ = _cortex?.AcceptTrainingAsync("lift"); } catch { }
                    _waitingForCortexResult = false;
                    _repState = RepState.Accepted;
                    _repTimer = 0;
                }
                break;

            case RepState.Accepted:
                if (_repTimer >= 1.0f)
                {
                    _currentRep++;
                    if (_currentRep >= TOTAL_REPS)
                    {
                        try { _ = _cortex?.SaveProfileAsync(); } catch { }
                        EnterPhase(Phase.Complete);
                    }
                    else
                    {
                        _repState = RepState.Countdown;
                        _repTimer = 0;
                    }
                }
                break;

            case RepState.Rejected:
                if (_repTimer >= 1.5f)
                {
                    // Retry the same rep — don't increment _currentRep.
                    _repState = RepState.Countdown;
                    _repTimer = 0;
                }
                break;
        }
    }

    void EnterPhase(Phase p)
    {
        CurrentPhase = p;
        _phaseTimer = 0;
        _repTimer = 0;
        _waitingForCortexResult = false;
        if (p == Phase.LiftTraining || p == Phase.NeutralBaseline)
        {
            _currentRep = 0;
            _consecutiveRejects = 0;
            _repState = RepState.Countdown;
        }
    }

    int CountGoodElectrodes()
    {
        if (_cortex == null) return 0;
        var cq = _cortex.GetHeadsetContactQuality();
        int n = 0;
        foreach (var (name, _, _) in Electrodes)
        {
            if (cq.TryGetValue(name, out var v) && v == "good") n++;
        }
        return n;
    }

    public void Draw()
    {
        DrawBackground();
        switch (CurrentPhase)
        {
            case Phase.HeadsetCheck:     DrawHeadsetCheck(); break;
            case Phase.NeutralBaseline:  DrawNeutralBaseline(); break;
            case Phase.LiftTraining:     DrawLiftTraining(); break;
            case Phase.Complete:         DrawComplete(); break;
        }
        DrawSkipFooter();
    }

    void DrawBackground()
    {
        // Burnt-ochre gradient — matches the existing game's outback aesthetic.
        for (int y = 0; y < LogH; y++)
        {
            float t = y / (float)LogH;
            var c = Color.Lerp(SkyTop, SkyBot, t);
            _drawRect(0, y, LogW, 1, c);
        }
        // Subtle vignette
        _drawRect(0, 0, LogW, LogH, new Color(0, 0, 0, 0x30));
    }

    void DrawHeadsetCheck()
    {
        _drawTextFn("IN-APP HEADSET TRAINING", LogW / 2f, 50, 32, Accent, true, true);
        _drawTextFn("PUT ON YOUR HEADSET", LogW / 2f, 110, 28, Color.White, true, true);
        _drawTextFn("ADJUST UNTIL DOTS TURN GREEN", LogW / 2f, 150, 18, Muted, true, true);

        // Head outline box
        float headW = 360, headH = 320;
        float headX = (LogW - headW) / 2f;
        float headY = 200;
        // Simple oval-ish head silhouette using stacked rects
        for (int yy = 0; yy < headH; yy++)
        {
            float ty = yy / headH;
            // ellipse half-width
            float hw = MathF.Sqrt(MathF.Max(0, 1f - (2f * ty - 1f) * (2f * ty - 1f))) * (headW / 2f);
            _drawRect(headX + headW / 2f - hw, headY + yy, hw * 2, 1, new Color(0, 0, 0, 0x40));
        }

        var cq = _cortex?.GetHeadsetContactQuality();
        foreach (var (name, nx, ny) in Electrodes)
        {
            float ex = headX + nx * headW;
            float ey = headY + ny * headH;
            Color dot;
            if (cq != null && cq.TryGetValue(name, out var v))
            {
                dot = v switch { "good" => Good, "fair" => Fair, _ => Bad };
            }
            else dot = Bad;
            _drawCircle(ex, ey, 14, new Color(0, 0, 0, 0x60));
            _drawCircle(ex, ey, 11, dot);
            _drawTextFn(name, ex, ey + 22, 12, Color.White, true, true);
        }

        // Status line
        var col = _continueAvailable ? Good : (_cortex != null && _cortex.IsConnected ? Fair : Bad);
        _drawTextFn(_statusMessage.ToUpperInvariant(), LogW / 2f, headY + headH + 30, 18, col, true, true);

        if (_continueAvailable)
        {
            _drawTextFn("[SPACE] CONTINUE", LogW / 2f, headY + headH + 60, 22, Accent, true, true);
        }
        else if (_cortex == null || !_cortex.IsConnected)
        {
            _drawTextFn("[R] RETRY CONNECTION", LogW / 2f, headY + headH + 60, 20, Accent, true, true);
        }
    }

    void DrawNeutralBaseline()
    {
        _drawTextFn("PHASE 1 OF 3 - NEUTRAL", LogW / 2f, 60, 22, Accent, true, true);
        _drawTextFn("RELAX. CLEAR YOUR MIND.", LogW / 2f, 200, 36, Color.White, true, true);
        _drawTextFn("DONT THINK ABOUT ANYTHING SPECIFIC.", LogW / 2f, 260, 20, Muted, true, true);

        // Countdown bar
        float t = MathHelper.Clamp(_phaseTimer / NEUTRAL_DURATION, 0, 1);
        float barW = 480;
        float barX = (LogW - barW) / 2f;
        float barY = 380;
        _drawRect(barX, barY, barW, 20, new Color(0, 0, 0, 0x60));
        _drawRect(barX, barY, barW * t, 20, Accent);

        float secLeft = MathF.Max(0, NEUTRAL_DURATION - _phaseTimer);
        _drawTextFn($"{secLeft:F0}S", LogW / 2f, barY + 50, 28, Accent, true, true);
    }

    void DrawLiftTraining()
    {
        _drawTextFn("PHASE 2 OF 3 - LIFT", LogW / 2f, 50, 22, Accent, true, true);
        _drawTextFn($"REP {_currentRep + 1} / {TOTAL_REPS}", LogW / 2f, 90, 26, Color.White, true, true);

        switch (_repState)
        {
            case RepState.Countdown:
                int n = (int)MathF.Ceiling(REP_COUNTDOWN - _repTimer);
                if (n < 1) n = 1;
                _drawTextFn("GET READY", LogW / 2f, 200, 28, Color.White, true, true);
                // Big countdown circle
                _drawCircle(LogW / 2f, 330, 80, new Color(0, 0, 0, 0x60));
                _drawCircle(LogW / 2f, 330, 72, Accent);
                _drawTextFn(n.ToString(), LogW / 2f, 330, 64, new Color(0x1A, 0x0F, 0x08), true, false);
                break;

            case RepState.Prompt:
                // Flashing accent-coloured prompt
                bool on = ((int)(_repTimer * 10) % 2) == 0;
                if (on) _drawRect(0, 0, LogW, LogH, new Color(0xE8, 0xC4, 0x40, 0x40));
                _drawTextFn("IMAGINE PRESSING SPACEBAR NOW", LogW / 2f, 280, 32, Accent, true, true);
                _drawTextFn("HOLD THE THOUGHT", LogW / 2f, 340, 22, Color.White, true, true);
                break;

            case RepState.Relax:
            case RepState.AwaitingResult:
                _drawTextFn("RELAX...", LogW / 2f, 300, 36, Muted, true, true);
                break;

            case RepState.Accepted:
                _drawCircle(LogW / 2f, 320, 70, Good);
                _drawTextFn("+", LogW / 2f, 320, 56, Color.White, true, true);
                _drawTextFn("REP ACCEPTED", LogW / 2f, 420, 24, Good, true, true);
                break;

            case RepState.Rejected:
                _drawCircle(LogW / 2f, 320, 70, Bad);
                _drawTextFn("X", LogW / 2f, 320, 56, Color.White, true, true);
                _drawTextFn("RETRY THIS REP", LogW / 2f, 420, 24, Bad, true, true);
                if (_consecutiveRejects >= 3)
                {
                    _drawTextFn("TRY CLOSING YOUR EYES", LogW / 2f, 470, 18, Color.White, true, true);
                    _drawTextFn("AND IMAGINE PRESSING A KEY FIRMLY", LogW / 2f, 500, 18, Color.White, true, true);
                }
                break;
        }

        // Progress dots
        float dotsY = 540;
        float dotR = 8;
        float spacing = 28;
        float startX = LogW / 2f - (TOTAL_REPS - 1) * spacing / 2f;
        for (int i = 0; i < TOTAL_REPS; i++)
        {
            Color dot = i < _currentRep ? Good : (i == _currentRep ? Accent : new Color(80, 60, 40));
            _drawCircle(startX + i * spacing, dotsY, dotR, dot);
        }
    }

    void DrawComplete()
    {
        _drawTextFn("TRAINING COMPLETE", LogW / 2f, 140, 40, Accent, true, true);
        _drawTextFn("YOURE READY TO PLAY.", LogW / 2f, 210, 24, Color.White, true, true);

        if (_cortex != null)
        {
            _drawTextFn($"PROFILE SAVED  -  LAST POWER {_cortex.LastCommandPower:F2}", LogW / 2f, 260, 16, Muted, true, true);
        }

        // "Play Now" button
        float bx = LogW / 2f - 140, by = 340, bw = 280, bh = 64;
        _drawRect(bx, by, bw, bh, Accent);
        _drawTextFn("[SPACE] PLAY NOW", LogW / 2f, by + bh / 2f, 24, new Color(0x1A, 0x0F, 0x08), true, false);

        // "Retrain" button
        float rx = LogW / 2f - 100, ry = 430, rw = 200, rh = 46;
        _drawRect(rx, ry, rw, rh, new Color(0, 0, 0, 0x60));
        _drawRect(rx, ry, rw, 2, Accent);
        _drawRect(rx, ry + rh - 2, rw, 2, Accent);
        _drawTextFn("[R] RETRAIN", LogW / 2f, ry + rh / 2f, 20, Accent, true, false);
    }

    void DrawSkipFooter()
    {
        if (CurrentPhase == Phase.Complete) return;
        _drawTextFn("[S] SKIP - USE KEYBOARD", LogW / 2f, LogH - 22, 12, Muted, true, false);
    }
}
