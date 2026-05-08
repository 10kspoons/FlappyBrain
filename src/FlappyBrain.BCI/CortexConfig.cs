namespace FlappyBrain.BCI;
public sealed record CortexConfig
{
    public string ClientId       { get; init; } = "";
    public string ClientSecret   { get; init; } = "";
    public string ProfileName    { get; init; } = "flappybrain";
    public string ActionMap      { get; init; } = "push";
    public double PowerThreshold { get; init; } = 0.6;
}
