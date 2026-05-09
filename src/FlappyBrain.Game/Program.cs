using FlappyBrain;
using System.Linq;

bool aiMode       = args.Any(a => a == "--ai");
bool learnedMode  = args.Any(a => a == "--ai-learned");
bool outbackTheme = args.Any(a => a == "--theme-outback");
if (learnedMode) aiMode = true;

using var game = new FlappyBrainGameV2(aiMode, learnedMode, outbackTheme);
game.Run();
