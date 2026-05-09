using FlappyBrain;
using System.Linq;

bool aiMode = args.Any(a => a == "--ai");
using var game = new FlappyBrainGameV2(aiMode);
game.Run();
