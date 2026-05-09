using Xunit;

namespace FlappyBrain.Tests;

public class CollisionTests
{
    // Mirror the game constants
    private const float LogW    = 800f;
    private const float LogH    = 600f;
    private const float BirdW   = 40f;
    private const float BirdH   = 36f;
    private const float PipeW   = 90f;

    // ── BirdHitbox geometry ───────────────────────────────────────────────────

    [Fact]
    public void BirdHitbox_IsInsetBy8PixelsTotal()
    {
        // Visual bounds 40×36; hitbox should be 32×28 (8px narrower, 8px shorter)
        var hb = Collision.BirdHitbox(centerX: 200, centerY: 300, width: BirdW, height: BirdH);
        Assert.Equal(32f, hb.Width);
        Assert.Equal(28f, hb.Height);
        // Centered: hitbox X = cx - 16, Y = cy - 14
        Assert.Equal(184f, hb.X);
        Assert.Equal(286f, hb.Y);
    }

    // ── Pipe rectangle geometry ───────────────────────────────────────────────

    [Fact]
    public void PipeTopRect_BasicCase()
    {
        // gap centred at 300 with height 200 → top pipe = [0..200]
        var r = Collision.PipeTopRect(pipeX: 200, pipeWidth: PipeW, gapCenterY: 300, gapHeight: 200);
        Assert.Equal(200f, r.X);
        Assert.Equal(0f, r.Y);
        Assert.Equal(PipeW, r.Width);
        Assert.Equal(200f, r.Height);
    }

    [Fact]
    public void PipeBottomRect_BasicCase()
    {
        // gap centred at 300 with height 200 → bottom pipe starts at 400, height 200
        var r = Collision.PipeBottomRect(pipeX: 200, pipeWidth: PipeW, gapCenterY: 300, gapHeight: 200, screenH: LogH);
        Assert.Equal(200f, r.X);
        Assert.Equal(400f, r.Y);
        Assert.Equal(PipeW, r.Width);
        Assert.Equal(200f, r.Height);
    }

    // ── Core collision cases ──────────────────────────────────────────────────

    [Fact]
    public void BirdInsideGap_DoesNotCollide()
    {
        // gap=[200,400]; bird at y=300 → hitbox y=[286,314]; pipe at x=200
        bool top = Collision.HitsTopPipe(216, 300, BirdW, BirdH, 200, PipeW, 300, 200);
        bool bot = Collision.HitsBottomPipe(216, 300, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        Assert.False(top, "should not hit top pipe when in middle of gap");
        Assert.False(bot, "should not hit bottom pipe when in middle of gap");
    }

    [Fact]
    public void BirdHittingTopPipe_Collides()
    {
        // top pipe [0,200]; bird at y=100 → hitbox y=[86,114]; clearly inside top pipe
        bool top = Collision.HitsTopPipe(216, 100, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.True(top);
    }

    [Fact]
    public void BirdHittingBottomPipe_Collides()
    {
        // bottom pipe [400,600]; bird at y=500 → hitbox y=[486,514]; inside bottom pipe
        bool bot = Collision.HitsBottomPipe(216, 500, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        Assert.True(bot);
    }

    // ── Boundary conditions ───────────────────────────────────────────────────

    [Fact]
    public void BirdAtTopGapEdge_JustInsideGap_DoesNotCollide()
    {
        // top pipe ends at y=200. Bird hitbox top edge y = cy-14.
        // For just-inside: cy = 214 → hitbox y=[200, 228]. RectangleF.Intersects uses
        // strict `<`/`>` so 200 < 200 is false → no collision.
        bool top = Collision.HitsTopPipe(216, 214, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.False(top);
    }

    [Fact]
    public void BirdJustInsideTopPipe_Collides()
    {
        // 1px deeper into top pipe: cy=213 → hitbox y=[199,227]. 199 < 200 = true → collision.
        bool top = Collision.HitsTopPipe(216, 213, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.True(top);
    }

    [Fact]
    public void BirdAtBottomGapEdge_JustInsideGap_DoesNotCollide()
    {
        // bottom pipe starts at y=400. Bird hitbox bottom edge y = cy+14.
        // Just-inside: cy=386 → hitbox y=[372,400]. 400 > 400 is false → no collision.
        bool bot = Collision.HitsBottomPipe(216, 386, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        Assert.False(bot);
    }

    [Fact]
    public void BirdJustInsideBottomPipe_Collides()
    {
        // 1px deeper: cy=387 → hitbox y=[373,401]. 401 > 400 = true → collision.
        bool bot = Collision.HitsBottomPipe(216, 387, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        Assert.True(bot);
    }

    // ── Out-of-screen bird ────────────────────────────────────────────────────

    [Fact]
    public void BirdAboveScreen_GeometryStillSafe()
    {
        // Bird centre y < 0 — the game treats this as death (out of bounds), but Collision
        // should still produce sensible results without throwing.
        var hb = Collision.BirdHitbox(216, -100, BirdW, BirdH);
        Assert.Equal(-114f, hb.Y);
        // Should not collide with a pipe whose gap is far below
        bool top = Collision.HitsTopPipe(216, -100, BirdW, BirdH, 200, PipeW, 300, 200);
        // Top pipe is [0,200]; bird hitbox y=[-114,-86]; -86 > 0 is false → no collide
        Assert.False(top);
    }

    [Fact]
    public void BirdBelowScreen_GeometryStillSafe()
    {
        // Bird Y > screenH — death case; geometry should still be safe
        var hb = Collision.BirdHitbox(216, 700, BirdW, BirdH);
        Assert.Equal(686f, hb.Y);
        bool bot = Collision.HitsBottomPipe(216, 700, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        // Bottom pipe is [400,600]; bird hitbox y=[686,714]; 686 < 600 is false → no collide
        Assert.False(bot);
    }

    // ── Edge gaps (the bug case) ──────────────────────────────────────────────

    [Fact]
    public void GapAtTopOfScreen_TopPipeHasZeroHeight()
    {
        // gapCenterY=50, gapH=200 → unclamped top h = 50-100 = -50; must be clamped to 0
        var top = Collision.PipeTopRect(200, PipeW, gapCenterY: 50, gapHeight: 200);
        Assert.Equal(0f, top.Height);
        // Bottom pipe should still work: y = 150, h = 450
        var bot = Collision.PipeBottomRect(200, PipeW, gapCenterY: 50, gapHeight: 200, screenH: LogH);
        Assert.Equal(150f, bot.Y);
        Assert.Equal(450f, bot.Height);
    }

    [Fact]
    public void GapAtBottomOfScreen_BottomPipeHasZeroHeight_BugFix()
    {
        // The reported bug: gapCenterY=500, gapHeight=210
        // unclamped bottom h = 600 - (500+105) = -5 → must be clamped to 0
        var bot = Collision.PipeBottomRect(200, PipeW, gapCenterY: 500, gapHeight: 210, screenH: LogH);
        Assert.Equal(605f, bot.Y);
        Assert.Equal(0f, bot.Height);
        Assert.True(bot.Height >= 0, "Height must never be negative");
        // Top pipe should still be valid: h = 500 - 105 = 395
        var top = Collision.PipeTopRect(200, PipeW, gapCenterY: 500, gapHeight: 210);
        Assert.Equal(395f, top.Height);
    }

    [Fact]
    public void NegativeHeightProtection_ExtremeGapCenter()
    {
        // gapCenterY beyond screen — both heights must be non-negative
        var top1 = Collision.PipeTopRect(200, PipeW, gapCenterY: -100, gapHeight: 200);
        var bot1 = Collision.PipeBottomRect(200, PipeW, gapCenterY: -100, gapHeight: 200, screenH: LogH);
        var top2 = Collision.PipeTopRect(200, PipeW, gapCenterY: 1000, gapHeight: 200);
        var bot2 = Collision.PipeBottomRect(200, PipeW, gapCenterY: 1000, gapHeight: 200, screenH: LogH);
        Assert.True(top1.Height >= 0);
        Assert.True(bot1.Height >= 0);
        Assert.True(top2.Height >= 0);
        Assert.True(bot2.Height >= 0);
    }

    // ── Horizontal positioning ────────────────────────────────────────────────

    [Fact]
    public void PipeFarToTheRight_DoesNotCollide()
    {
        // Pipe at X=600 spans [600,690]; bird at X=216 → hitbox X=[200,232]; no horizontal overlap
        bool top = Collision.HitsTopPipe(216, 100, BirdW, BirdH, 600, PipeW, 300, 200);
        Assert.False(top);
    }

    [Fact]
    public void PipeAlreadyPassedLeft_DoesNotCollide()
    {
        // Pipe at X=-200 spans [-200,-110]; bird at X=216; no overlap
        bool top = Collision.HitsTopPipe(216, 100, BirdW, BirdH, -200, PipeW, 300, 200);
        Assert.False(top);
    }

    [Fact]
    public void BirdJustOnePixelLeftOfPipe_DoesNotCollide()
    {
        // Pipe at X=200 spans [200,290]. Bird hitbox right edge needs to be just below 200.
        // hitbox right = cx+16. cx=183 → right=199. 199 > 200 is false → no collide.
        bool top = Collision.HitsTopPipe(183, 100, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.False(top);
    }

    [Fact]
    public void BirdJustOnePixelIntoPipe_Collides()
    {
        // cx=184 → hitbox right=200. 200 > 200 is false. cx=185 → right=201. 201 > 200 → collide.
        bool top = Collision.HitsTopPipe(185, 100, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.True(top);
    }

    // ── 1-pixel miss tests ────────────────────────────────────────────────────

    [Fact]
    public void BirdBarelyMissesTopPipeBy1px_DoesNotCollide()
    {
        // Top pipe ends at y=200. Bird hitbox top y must be exactly 200 to miss.
        // cy=214 → hitbox y=[200,228]. 200 < 200 is false → no collide.
        bool top = Collision.HitsTopPipe(216, 214, BirdW, BirdH, 200, PipeW, 300, 200);
        Assert.False(top);
    }

    [Fact]
    public void BirdBarelyMissesBottomPipeBy1px_DoesNotCollide()
    {
        // Bottom pipe starts at y=400. Bird hitbox bottom y must be exactly 400 to miss.
        // cy=386 → hitbox y=[372,400]. 400 > 400 is false → no collide.
        bool bot = Collision.HitsBottomPipe(216, 386, BirdW, BirdH, 200, PipeW, 300, 200, LogH);
        Assert.False(bot);
    }
}
