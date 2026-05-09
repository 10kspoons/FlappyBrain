using FlappyBrain;
using System.Linq;

bool aiMode = args.Any(a => a == "--ai");
bool learnedMode = args.Any(a => a == "--ai-learned");
if (learnedMode) aiMode = true; // learned implies AI control
using var game = new FlappyBrainGameV2(aiMode, learnedMode);
game.Run();
