namespace FlappyBrain;

public struct RectangleF
{
    public float X, Y, Width, Height;
    public RectangleF(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
    public bool Intersects(RectangleF o) =>
        X < o.X + o.Width && X + Width > o.X &&
        Y < o.Y + o.Height && Y + Height > o.Y;
}
