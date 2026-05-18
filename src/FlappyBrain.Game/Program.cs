using FlappyBrain;
using System.Linq;

bool aiMode       = args.Any(a => a == "--ai");
bool learnedMode  = args.Any(a => a == "--ai-learned");
bool outbackTheme = args.Any(a => a == "--theme-outback");
bool fullscreen   = true;   // always fullscreen
bool slowGravity  = args.Any(a => a == "--slow-gravity");
bool skipTrainingFlag = args.Any(a => a == "--skip-training");
if (learnedMode) aiMode = true;

// BCI / training are chosen at runtime via the launch menu.
// Defaults: BCI off, training skipped (the menu sets them).
// --skip-training CLI override: skip menu, go to BCI + existing-training mode.
bool enableBci    = skipTrainingFlag;
bool skipTraining = skipTrainingFlag;

// --theme <name> support: outback, safari, steampunk, postapoc, landmarks, spoons
string themeName = "";
int themeIdx = System.Array.IndexOf(args, "--theme");
if (themeIdx >= 0 && themeIdx + 1 < args.Length)
    themeName = args[themeIdx + 1];
if (string.IsNullOrEmpty(themeName) && outbackTheme)
    themeName = "outback";

using var game = new FlappyBrainGameV2(aiMode, learnedMode, outbackTheme, fullscreen, themeName, slowGravity, enableBci, skipTraining);
game.Run();
