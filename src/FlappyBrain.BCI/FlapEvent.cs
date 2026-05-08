namespace FlappyBrain.BCI;
public readonly record struct FlapEvent(DateTimeOffset Timestamp, string Action, double Power);
