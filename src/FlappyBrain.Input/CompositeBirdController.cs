namespace FlappyBrain.Input;

public sealed class CompositeBirdController : IBirdController
{
    private readonly IBirdController[] _controllers;
    public string Name => "Composite";
    public CompositeBirdController(params IBirdController[] controllers) => _controllers = controllers;
    public bool ShouldFlap() { foreach (var c in _controllers) if (c.ShouldFlap()) return true; return false; }
}
