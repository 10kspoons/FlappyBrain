using FlappyBrain.Input;
namespace FlappyBrain.BCI;

public sealed class BCIController : IBirdController
{
    private readonly CortexClient _cortex;
    public string Name => $"BCI (Emotiv Epoc X) — {_cortex.SignalQuality}";
    public SignalQuality SignalQuality => _cortex.SignalQuality;
    public bool IsConnected => _cortex.IsConnected;
    public BCIController(CortexClient cortex) => _cortex = cortex;
    public bool ShouldFlap()
    {
        bool flap = false;
        while (_cortex.FlapEvents.TryRead(out _)) flap = true;
        return flap;
    }
}
