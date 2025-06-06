﻿namespace LyricRenderer.Uwp.Abstraction.Render;

public abstract class LineRollingCalculator
{
    public abstract float CalculateCurrentY(float fromY, float targetY, RenderingLyricLine currentLine, RenderContext context);
}
