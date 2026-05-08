namespace FlappyBrain.Input;

public interface IBirdController
{
    bool ShouldFlap();
    string Name { get; }
}
