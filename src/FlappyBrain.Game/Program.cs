using FlappyBrain;
using System.Linq;

bool aiMode       = args.Any(a => a == "--ai");
bool learnedMode  = args.Any(a => a == "--ai-learned");
bool outbackTheme = args.Any(a => a == "--theme-outback");
bool fullscreen   = args.Any(a => a == "--fullscreen" || a == "-f");
bool slowGravity  = args.Any(a => a == "--slow-gravity");
bool enableBci    = args.Any(a => a == "--bci");
bool skipTraining = args.Any(a => a == "--skip-training");
if (learnedMode) aiMode = true;

// --theme <name> support: outback, safari, steampunk, postapoc, landmarks, spoons
string themeName = "";
int themeIdx = System.Array.IndexOf(args, "--theme");
if (themeIdx >= 0 && themeIdx + 1 < args.Length)
    themeName = args[themeIdx + 1];
if (string.IsNullOrEmpty(themeName) && outbackTheme)
    themeName = "outback";

using var game = new FlappyBrainGameV2(aiMode, learnedMode, outbackTheme, fullscreen, themeName, slowGravity, enableBci, skipTraining);
game.Run();
