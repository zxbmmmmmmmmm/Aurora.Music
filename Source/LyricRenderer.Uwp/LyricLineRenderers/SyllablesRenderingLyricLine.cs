﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;
using LyricRenderer.Uwp.Abstraction;
using LyricRenderer.Uwp.Abstraction.Render;
using LyricRenderer.Uwp.Animator;
using LyricRenderer.Uwp.Animator.EaseFunctions;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;

namespace LyricRenderer.Uwp.LyricLineRenderers
{
    public class RenderingSyllable
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。还我required字段
        public string Syllable { get; set; }
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public string? Transliteration { get; set; }
    }

    public class SyllablesRenderingLyricLine : RenderingLyricLine
    {
        public string? Text { get; set; }
        private CanvasTextFormat? textFormat;
        private CanvasTextFormat? translationFormat;
        private CanvasTextFormat? transliterationFormat;
        private CanvasTextLayout? textLayout;

        private bool _isRomajiSyllable = false;

        private CanvasTextLayout? tl;
        private CanvasTextLayout? tll;
        public EaseFunctionBase EaseFunction { get; set; } = new CustomCircleEase { EasingMode = EasingMode.EaseOut };

        private bool _isFocusing;
        private float _canvasWidth;
        private float _canvasHeight;
        public bool IsSyllable = false;

        public List<RenderingSyllable> Syllables { get; set; } = [];
        public string? Transliteration { get; set; }
        public string? Translation { get; set; }

        public override void GoToReactionState(ReactionState state, RenderContext context)
        {
            _lastReactionTime = context.CurrentLyricTime;
            _reactionState = state;
        }

        private const long ReactionDurationTick = 2000000;

        private const long ScaleAnimationDuration = 500;

        public override bool Render(CanvasDrawingSession session, LineRenderOffset offset, RenderContext context)
        {
            if (textLayout is null) return true;

            var drawingTop = offset.Y + _drawingOffsetY;
            // 画背景
            if (_reactionState == ReactionState.Enter)
            {
                var color = new Color
                {
                    A = 40,
                    R = 0,
                    G = 0,
                    B = 0
                };
                session.FillRoundedRectangle((float)textLayout.LayoutBounds.Left, offset.Y,
                    RenderingWidth, RenderingHeight, 6, 6, color);
            }

            float actualX = offset.X;

            switch (TypographySelector(t => t?.Alignment, context)!.Value)
            {
                case TextAlignment.Left:
                    actualX += 4;
                    break;
                case TextAlignment.Right:
                    actualX -= 4;
                    break;
            }

            var totalCommand = new CanvasCommandList(session);
            var actualTop = 0.0f;
            using (CanvasDrawingSession targetDrawingSession = totalCommand.CreateDrawingSession())
            {
                var cl = new CanvasCommandList(targetDrawingSession);
                using (var clds = cl.CreateDrawingSession())
                {
                    //罗马字
                    var idleColor = TypographySelector(t => t?.FocusingColor, context)!.Value;
                    idleColor.A = (byte)(idleColor.A * 0.3);
                    if (tll != null)
                    {
                        if (_isFocusing && context.Effects.TransliterationScanning)
                        {
                            if (_isRomajiSyllable)
                            {
                                clds.DrawTextLayout(tll, actualX, actualTop, idleColor);
                                var highlightGeometry = CreateHighlightGeometries(context.CurrentLyricTime, tll,
                                    session, Syllables, false, true);
                                var matrix = Matrix3x2.CreateTranslation(actualX, actualTop);
                                using (clds.CreateLayer(1, highlightGeometry.geo1, matrix))
                                {
                                    clds.DrawTextLayout(tll, actualX, actualTop,
                                        TypographySelector(t => t?.FocusingColor, context)!.Value);
                                }
                                if (highlightGeometry.geo2 is not null) //填充渐变矩形
                                {
                                    using (clds.CreateLayer(highlightGeometry.currentPrecentage, highlightGeometry.geo2, matrix))
                                    {
                                        var color = TypographySelector(t => t?.FocusingColor, context)!.Value;
                                        color.A = (byte)(255 * highlightGeometry.currentPrecentage);
                                        clds.DrawTextLayout(tll, actualX, actualTop, color);
                                    }
                                }
                            }
                            else
                            {
                                clds.DrawTextLayout(tll, actualX, actualTop,
                                    TypographySelector(t => t?.FocusingColor, context)!.Value);
                            }
                        }
                        else
                        {
                            clds.DrawTextLayout(tll, actualX, actualTop,
                                TypographySelector(t => t?.IdleColor, context)!.Value);
                        }

                        actualTop += (float)tll.LayoutBounds.Height;
                    }

                    //歌词
                    clds.DrawTextLayout(textLayout, actualX, actualTop,
                        TypographySelector(t => t?.IdleColor, context)!.Value);
                    var textTop = actualTop;
                    if (_isFocusing)
                    {
                        if (IsSyllable || context.Effects.SimpleLineScanning)
                        {
                            var highlightGeometry = CreateHighlightGeometries(context.CurrentLyricTime, textLayout,
                                session, Syllables);
                            var matrix = Matrix3x2.CreateTranslation(actualX, textTop);
                            using (clds.CreateLayer(1, highlightGeometry.geo1, matrix))
                            {
                                clds.DrawTextLayout(textLayout, actualX, textTop,
                                    TypographySelector(t => t?.FocusingColor, context)!.Value);
                            }
                            if (highlightGeometry.geo2 is not null) //填充渐变矩形
                            {
                                using (clds.CreateLayer(1, highlightGeometry.geo2, matrix))
                                {

                                    var color = TypographySelector(t => t?.FocusingColor, context)!.Value;
                                    color.A = (byte)(128 * highlightGeometry.currentPrecentage);
                                    clds.DrawTextLayout(textLayout, actualX, textTop, color);
                                }
                            }

                        }
                        else
                        {
                            clds.DrawTextLayout(textLayout, actualX, textTop,
                                TypographySelector(t => t?.FocusingColor, context)!.Value);
                        }
                    }

                    actualTop += (float)textLayout.LayoutBounds.Height;

                    //翻译
                    if (tl != null)
                    {
                        clds.DrawTextLayout(tl, actualX, actualTop,
                            _isFocusing
                                ? TypographySelector(t => t?.FocusingColor, context)!.Value
                                : idleColor);
                    }
                }

                if (_isFocusing && context.Effects.FocusHighlighting)
                {
                    //画发光效果
                    var highlightOpacityEffect = new Microsoft.Graphics.Canvas.Effects.OpacityEffect
                    {
                        Source = new GaussianBlurEffect
                        {
                            Source = cl,
                            BlurAmount = 6,
                        },
                        Opacity = 0.4f
                    };
                    targetDrawingSession.DrawImage(highlightOpacityEffect);
                    targetDrawingSession.DrawImage(cl);
                }
                else
                {
                    targetDrawingSession.DrawImage(cl);
                }
            }

            var gap = Id - context.CurrentLyricLineIndex;
            ICanvasImage finalEffect = totalCommand;
            if (context.Effects.ScaleWhenFocusing)
            {
                // 计算 Progress
                var progress = 0f;
                if (context.CurrentLyricTime - EndTime >= 0 && context.CurrentLyricTime - EndTime <= ScaleAnimationDuration)
                {
                    progress = 1 - ((float)EaseFunction.Ease(Math.Clamp(
                        (context.CurrentLyricTime - EndTime) * 1.0f / ScaleAnimationDuration, 0, 1)));
                }
                else if (_isFocusing && context.CurrentLyricTime - StartTime >= 0)
                {
                    progress = (float)EaseFunction.Ease(Math.Clamp(
                        (context.CurrentLyricTime - StartTime) * 1.0f / ScaleAnimationDuration, 0, 1));
                }


                var scaling = 0.8F + progress * 0.2F;
                var transformEffect = new Transform2DEffect
                {
                    Source = totalCommand,
                    TransformMatrix = GetCenterMatrix(0, 0, _scalingCenterX,
                        (float)textLayout.LayoutBounds.Height / 2, scaling, scaling),
                };
                finalEffect = new Microsoft.Graphics.Canvas.Effects.OpacityEffect
                {
                    Source = transformEffect,
                    Opacity = 0.5f + progress * 0.5f,
                };
            }
            else
            {
                if (context.Effects.ScaleWhenFocusing)
                {
                    finalEffect = new Transform2DEffect
                    {
                        Source = totalCommand,
                        TransformMatrix = _unfocusMatrix,
                    };
                }


            }

            var blurEffect = new GaussianBlurEffect
            {
                Source = finalEffect,
                BlurAmount = 0,
            };
            var opacityEffect = new OpacityEffect
            {
                Source = blurEffect,
                Opacity = 1 - Math.Clamp(Math.Abs(gap) / (10f - (5 / 10f)), 0, 0.9f),
            };

            if (context.Effects.Blur && !_isFocusing)
            {
                blurEffect.BlurAmount = Math.Clamp(Math.Abs(gap), 0, 250);
            }

            session.DrawImage(opacityEffect, actualX, drawingTop);


            if (context.Debug)
            {
                session.DrawText($"({offset.X},{drawingTop})", offset.X, drawingTop, Colors.Red);
                session.DrawText(Id.ToString(), offset.X, drawingTop + 15, Colors.Red);
                session.DrawRectangle(offset.X, drawingTop, RenderingWidth, RenderingHeight, Colors.Yellow);
            }

            return true;
        }


        /// <summary>
        /// 根据中心点放大
        /// </summary>
        public Matrix3x2 GetCenterMatrix(float X, float Y, float XCenter, float YCenter, float XScle, float YScle)
        {
            return Matrix3x2.CreateTranslation(-XCenter, -YCenter)
                   * Matrix3x2.CreateScale(XScle, YScle)
                   * Matrix3x2.CreateTranslation(X, Y)
                   * Matrix3x2.CreateTranslation(XCenter, YCenter);
        }

        /// <summary>
        /// 获取高亮矩形
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <param name="targetLayout"></param>
        /// <param name="resourceCreator"></param>
        /// <param name="syllables">目标歌词</param>
        /// <param name="isScan">是否为扫描式（否则为渐变）</param>
        /// <param name="isTransliteration">是否选择音译</param>
        /// <returns></returns>
        private (CanvasGeometry geo1, CanvasGeometry? geo2, float currentPrecentage)
            CreateHighlightGeometries(long currentTime, CanvasTextLayout targetLayout,
                ICanvasResourceCreator resourceCreator, List<RenderingSyllable>? syllables, bool isScan = true,
                bool isTransliteration = false)
        {
            var geos = new HashSet<CanvasGeometry>();
            CanvasGeometry? geo2 = null; //渐变矩形
            var currentPercentage = 0.0f;
            if (IsSyllable && syllables is not null)
            {
                if (syllables.Count <= 0)
                    return (CanvasGeometry.CreateGroup(resourceCreator, geos.ToArray()), geo2, currentPercentage);
                var index = syllables.FindLastIndex(t => t.EndTime <= currentTime);
                var letterPosition = syllables.GetRange(0, index + 1).Sum(p =>
                    isTransliteration ? p.Transliteration?.Length ?? 0 : p.Syllable.Length);
                if (index >= 0)
                {
                    // 获取高亮的字符区域集合
                    var regions = targetLayout.GetCharacterRegions(0, letterPosition);
                    foreach (var region in regions)
                    {
                        // 对每个字符创建矩形, 并加入到 geos
                        geos.Add(CanvasGeometry.CreateRectangle(resourceCreator, region.LayoutBounds));
                    }
                }

                if (index <= syllables.Count - 2)
                {
                    var currentLyric = syllables[index + 1];

                    if (currentLyric.StartTime <= currentTime)
                    {
                        // 获取当前字符的 Bound
                        var currentRegions =
                            targetLayout.GetCharacterRegions(letterPosition,
                                isTransliteration
                                    ? currentLyric.Transliteration?.Length ?? 0
                                    : currentLyric.Syllable.Length);
                        if (currentRegions is { Length: > 0 })
                        {
                            // 加个保险措施
                            // 计算当前字符的进度
                            currentPercentage = (currentTime - currentLyric.StartTime) * 1.0f /
                                                (currentLyric.EndTime - currentLyric.StartTime);
                            // 创建矩形
                            if (isScan)
                            {
                                var lastRect = CanvasGeometry.CreateRectangle(
                                    resourceCreator, (float)currentRegions[0].LayoutBounds.Left,
                                    (float)currentRegions[0].LayoutBounds.Top,
                                    (float)(currentRegions.Sum(t => t.LayoutBounds.Width) * currentPercentage),
                                    (float)currentRegions.Sum(t => t.LayoutBounds.Height));
                                geos.Add(lastRect);
                            }


                            // 高亮矩形
                            geo2 = CanvasGeometry.CreateRectangle(
                                resourceCreator, (float)currentRegions[0].LayoutBounds.Left,
                                (float)currentRegions[0].LayoutBounds.Top,
                                (float)(currentRegions.Sum(t => t.LayoutBounds.Width)),
                                (float)currentRegions.Sum(t => t.LayoutBounds.Height));
                        }
                    }
                }
            }
            else
            {
                var progress = Math.Clamp((currentTime - StartTime) * 1.0 / (EndTime - StartTime), 0, 1);
                var targetWidth = progress * _theoryFlatLineWidth;
                var accumulatedWidth = 0.0;
                var i = 0;
                for (; i < _lineRectangle.Count; i++)
                {
                    if (accumulatedWidth + _lineRectangle[i].Width < targetWidth)
                    {
                        geos.Add(CanvasGeometry.CreateRectangle(resourceCreator, _lineRectangle[i]));
                        accumulatedWidth += _lineRectangle[i].Width;
                    }
                    else
                        break;
                }

                // 扫描当前行
                if (_lineRectangle.Count > i)
                {
                    var currentLineRect = _lineRectangle[i];
                    var currentRect = CanvasGeometry.CreateRectangle(resourceCreator, (float)currentLineRect.Left,
                        (float)currentLineRect.Top, (float)(targetWidth - accumulatedWidth),
                        (float)currentLineRect.Height);
                    geos.Add(currentRect);
                }
            }

            // 拼合所有矩形
            return (CanvasGeometry.CreateGroup(resourceCreator, geos.ToArray()), geo2, currentPercentage);
        }

        public override void OnKeyFrame(CanvasDrawingSession session, RenderContext context)
        {
            _isFocusing = (context.CurrentKeyframe >= StartTime) && (context.CurrentKeyframe < EndTime);
            Hidden = HiddenOnBlur && !_isFocusing;

            if (_canvasWidth == 0.0f) return;
            if (textFormat is null)
                OnTypographyChanged(session, context);
        }

        public bool HiddenOnBlur { get; set; }
        private string _text = "";
        private bool _sizeChanged = true;
        private long _lastReactionTime;
        private ReactionState _reactionState = ReactionState.Leave;
        private float _scalingCenterX;
        private Matrix3x2 _unfocusMatrix = Matrix3x2.Identity;

        public override void OnRenderSizeChanged(CanvasDrawingSession session, RenderContext context)
        {
            if (HiddenOnBlur && !_isFocusing)
            {
                Hidden = true;
            }

            _sizeChanged = true;
            _canvasWidth = context.ItemWidth;
            _canvasHeight = context.ViewHeight;
            OnKeyFrame(session, context);
            OnTypographyChanged(session, context);
        }

        private List<Rect> _lineRectangle = [];
        private float _theoryFlatLineWidth;
        private float _drawingOffsetY;
        private bool _isInitialized = false;
        private string? _transliterationActual;

        public override void OnTypographyChanged(CanvasDrawingSession session, RenderContext context)
        {
            var add = 0.0f;
            var renderW = 0.0f;
            textFormat = new CanvasTextFormat
            {
                FontSize = HiddenOnBlur
                    ? TypographySelector(t => t?.LyricFontSize, context)!.Value / 2
                    : TypographySelector(t => t?.LyricFontSize, context)!.Value,
                HorizontalAlignment =
                    TypographySelector(t => t?.Alignment, context)!.Value switch
                    {
                        TextAlignment.Right => CanvasHorizontalAlignment.Right,
                        TextAlignment.Center => CanvasHorizontalAlignment.Center,
                        _ => CanvasHorizontalAlignment.Left
                    },
                VerticalAlignment = CanvasVerticalAlignment.Top,
                WordWrapping = CanvasWordWrapping.Wrap,
                Direction = CanvasTextDirection.LeftToRightThenTopToBottom,
                FontFamily = TypographySelector(t => t?.Font, context),
                FontWeight = HiddenOnBlur ? FontWeights.Normal : FontWeights.SemiBold
            };
            if (!_isInitialized)
                _isRomajiSyllable = Syllables?.Any(t => t.Transliteration is not null) ?? false;
            if (!string.IsNullOrWhiteSpace(Transliteration) || !string.IsNullOrWhiteSpace(Translation) ||
                _isRomajiSyllable)
            {
                if (!string.IsNullOrWhiteSpace(Transliteration) && context.EnableTransliteration)
                {
                    transliterationFormat = new CanvasTextFormat
                    {
                        FontSize = HiddenOnBlur
                            ? TypographySelector(t => t?.TransliterationFontSize, context)!.Value / 2
                            : TypographySelector(t => t?.TransliterationFontSize, context)!.Value,
                        HorizontalAlignment = TypographySelector(t => t?.Alignment, context)!.Value switch
                        {
                            TextAlignment.Right => CanvasHorizontalAlignment.Right,
                            TextAlignment.Center => CanvasHorizontalAlignment.Center,
                            _ => CanvasHorizontalAlignment.Left
                        },
                        VerticalAlignment = CanvasVerticalAlignment.Top,
                        WordWrapping = CanvasWordWrapping.Wrap,
                        Direction = CanvasTextDirection.LeftToRightThenTopToBottom,
                        FontFamily = TypographySelector(t => t?.Font, context),
                        FontWeight = FontWeights.Normal
                    };
                    if (!_isInitialized)
                        _transliterationActual = _isRomajiSyllable
                            ? string.Join("", Syllables!.Select(s => s.Transliteration))
                            : Transliteration;
                    tll = new CanvasTextLayout(session, _transliterationActual, transliterationFormat,
                        Math.Clamp(context.ItemWidth - 16, 0, int.MaxValue),
                        _canvasHeight);
                    add += 10;
                }
                else
                {
                    if (tll != null)
                    {
                        tll = null;
                    }
                    if (transliterationFormat != null)
                    {
                        transliterationFormat = null;
                    }
                }

                if (!string.IsNullOrWhiteSpace(Translation) && context.EnableTranslation)
                {
                    translationFormat = new CanvasTextFormat
                    {
                        FontSize = HiddenOnBlur
                            ? TypographySelector(t => t?.TranslationFontSize, context)!.Value / 2
                            : TypographySelector(t => t?.TranslationFontSize, context)!.Value,
                        HorizontalAlignment = TypographySelector(t => t?.Alignment, context)!.Value switch
                        {
                            TextAlignment.Right => CanvasHorizontalAlignment.Right,
                            TextAlignment.Center => CanvasHorizontalAlignment.Center,
                            _ => CanvasHorizontalAlignment.Left
                        },
                        VerticalAlignment = CanvasVerticalAlignment.Top,
                        WordWrapping = CanvasWordWrapping.Wrap,
                        Direction = CanvasTextDirection.LeftToRightThenTopToBottom,
                        FontFamily = TypographySelector(t => t?.Font, context),
                        FontWeight = FontWeights.Normal
                    };
                    string? trimmedText = Translation?.ToString().TrimEnd();
                    tl = new CanvasTextLayout(session, trimmedText, translationFormat,
                        Math.Clamp(context.ItemWidth - 16, 0, int.MaxValue), _canvasHeight);
                    add += 30;

                }
                else
                {
                    if (tl != null)
                    {
                        tl = null;
                    }
                    if (translationFormat != null)
                    {
                        translationFormat = null;
                    }
                }
                add += (float)(tll?.LayoutBounds.Height ?? 0f);
                add += (float)(tl?.LayoutBounds.Height ?? 0);
            }


            if (textLayout is null || _sizeChanged)
            {
                _sizeChanged = false;
                _text = IsSyllable ? string.Join("", Syllables.Select(t => t.Syllable)) : Text ?? "";
                textLayout = new CanvasTextLayout(session, _text, textFormat,
                    Math.Clamp(context.ItemWidth - 16, 0, int.MaxValue), _canvasHeight);
                // 创建所有行矩形
                if (!IsSyllable && Text is not null)
                {
                    _lineRectangle.Clear();
                    _theoryFlatLineWidth = 0;
                    var regions = textLayout.GetCharacterRegions(0, Text.Length);
                    if (regions is not null)
                    {
                        foreach (var canvasTextLayoutRegion in regions)
                        {
                            _lineRectangle.Add(new Rect(canvasTextLayoutRegion.LayoutBounds.Left,
                                canvasTextLayoutRegion.LayoutBounds.Top,
                                canvasTextLayoutRegion.LayoutBounds.Width,
                                canvasTextLayoutRegion.LayoutBounds.Height));
                            _theoryFlatLineWidth += (float)canvasTextLayoutRegion.LayoutBounds.Width;
                        }
                    }
                }
            }
            if (textLayout is null) return;
            _scalingCenterX = (float)(TypographySelector(t => t?.Alignment, context)!.Value switch
            {
                TextAlignment.Center => textLayout.LayoutBounds.Left + textLayout.LayoutBounds.Width / 2,
                TextAlignment.Right => textLayout.LayoutBounds.Left + textLayout.LayoutBounds.Width,
                TextAlignment.Left => 0,
                _ => throw new ArgumentOutOfRangeException()
            });
            _unfocusMatrix = GetCenterMatrix(0, 0, _scalingCenterX,
                (float)textLayout.LayoutBounds.Height / 2, 0.8F, 0.8F);
            _drawingOffsetY =
                (HiddenOnBlur
                    ? TypographySelector(t => t?.LyricFontSize, context)!.Value / 2
                    : TypographySelector(t => t?.LyricFontSize, context)!.Value) / 8f;
            RenderingHeight = (float)textLayout.LayoutBounds.Height + _drawingOffsetY + add;
            renderW = (float)Math.Max(textLayout.LayoutBounds.Width,
                Math.Max(tll?.LayoutBounds.Width ?? 0, tl?.LayoutBounds.Width ?? 0));
            RenderingWidth = renderW + 16;
            _isInitialized = true;
        }
    }
}
