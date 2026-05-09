using System;

namespace FlappyBrain;

/// <summary>Pure collision geometry — no MonoGame dependency.</summary>
public static class Collision
{
    public static RectangleF BirdHitbox(float centerX, float centerY, float width, float height)
        => new(centerX - width / 2 + 4, centerY - height / 2 + 4, width - 8, height - 8);

    public static RectangleF PipeTopRect(float pipeX, float pipeWidth, float gapCenterY, float gapHeight)
    {
        float h = gapCenterY - gapHeight / 2f;
        return new(pipeX, 0, pipeWidth, MathF.Max(0, h));
    }

    public static RectangleF PipeBottomRect(float pipeX, float pipeWidth, float gapCenterY, float gapHeight, float screenH)
    {
        float botY = gapCenterY + gapHeight / 2f;
        float h = screenH - botY;
        return new(pipeX, botY, pipeWidth, MathF.Max(0, h));
    }

    public static bool HitsTopPipe(float birdCX, float birdCY, float birdW, float birdH,
                                    float pipeX, float pipeW, float gapCenterY, float gapHeight)
    {
        var bird = BirdHitbox(birdCX, birdCY, birdW, birdH);
        var pipe = PipeTopRect(pipeX, pipeW, gapCenterY, gapHeight);
        return bird.Intersects(pipe);
    }

    public static bool HitsBottomPipe(float birdCX, float birdCY, float birdW, float birdH,
                                       float pipeX, float pipeW, float gapCenterY, float gapHeight, float screenH)
    {
        var bird = BirdHitbox(birdCX, birdCY, birdW, birdH);
        var pipe = PipeBottomRect(pipeX, pipeW, gapCenterY, gapHeight, screenH);
        return bird.Intersects(pipe);
    }
}
