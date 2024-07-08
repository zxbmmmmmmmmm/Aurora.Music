using System;
using LyricRenderer.Uwp.Abstraction;
using LyricRenderer.Uwp.Abstraction.Render;

namespace LyricRenderer.Uwp.RollingCalculators;

public class SyncRollingCalculator : LineRollingCalculator
{
    public const float Duration = 200;

    public override float CalculateCurrentY(float fromY, float targetY, RenderingLyricLine currentLine, RenderContext context)
    {
        var targetOffset = (targetY - fromY);
        var v = targetOffset / Duration;
        var t = Math.Clamp(context.CurrentLyricTime - context.CurrentKeyframe, 0, Duration);
        var y = v * t;
        return fromY + (y);
    }
}
