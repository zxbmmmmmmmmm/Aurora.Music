﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using LyricRenderer.Uwp.Abstraction;
using LyricRenderer.Uwp.Abstraction.Render;
using LyricRenderer.Uwp.Animator.EaseFunctions;
using Microsoft.Graphics.Canvas;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace LyricRenderer.Uwp
{
    public sealed partial class LyricRenderView : UserControl
    {
        public RenderContext Context { get; } = new();

        private const float Epsilon = 0.001f;

        public delegate void BeforeRenderDelegate(LyricRenderView view);

        public event BeforeRenderDelegate OnBeforeRender;

        public delegate void RequestSeekDelegate(long time);

        public event RequestSeekDelegate OnRequestSeek;

        private readonly CustomCircleEase _circleEase = new() { EasingMode = EasingMode.EaseOut };

        private bool _pointerPressed;
        private bool _jumpedLyrics = false;
        private double? _lastPointerPressedYValue;

        public bool EnableTranslation
        {
            get => Context.EnableTransliteration;
            set
            {
                Context.EnableTranslation = value;
                _isTypographyChanged = true;
                _needRecalculate = true;
            }
        }

        public bool EnableTransliteration
        {
            get => Context.EnableTransliteration;
            set
            {
                Context.EnableTransliteration = value;
                _isTypographyChanged = true;
                _needRecalculate = true;
            }
        }
        public bool HasJumpedLyrics
        {
            get => _jumpedLyrics;
        }

        public LyricRenderView()
        {
            InitializeComponent();
            this.Loaded += LyricRenderView_Loaded;
        }

        private void LyricRenderView_Loaded(object sender, RoutedEventArgs e)
        {
            Redesign((float)LyricView.Size.Width,(float)LyricView.Size.Height);
        }

        private bool _isTypographyChanged = true;

        public void ChangeFps(int fps)
        {
            LyricView.TargetElapsedTime = TimeSpan.FromMilliseconds(16.6 * (60d / fps));
        }

        public void ChangeRenderColor(Color idleColor, Color focusingColor)
        {
            Context.PreferTypography.IdleColor = idleColor;
            Context.PreferTypography.FocusingColor = focusingColor;
            _isTypographyChanged = true;
        }

        public void ChangeRenderFontSize(float lyricSize, float translationSize, float transliterationSize)
        {
            Context.PreferTypography.LyricFontSize = lyricSize;
            Context.PreferTypography.TranslationFontSize = translationSize;
            Context.PreferTypography.TransliterationFontSize = transliterationSize;
            _isTypographyChanged = true;
        }

        public void ChangeAlignment(TextAlignment alignment)
        {
            Context.PreferTypography.Alignment = alignment;
            _isTypographyChanged = true;
        }

        public void ChangeBeatPerMinute(float beatPerMinute)
        {
            Context.BeatPerMinute = beatPerMinute;
            _isTypographyChanged = true;
        }

        public void ReflowTime(long time)
        {
            var keys = _keyFrameRendered.Keys.ToArray();
            foreach (var key in keys)
            {
                if (key >= time) _keyFrameRendered[key] = false;
            }

            _needRecalculate = true;
        }

        private bool _needRecalculate = false;
        private bool _needRecalculateSize = false;
        private long _lastRedesignedTime;

        private bool _initializing = true;

        public void SetLyricLines(List<RenderingLyricLine> lines)
        {
            _initializing = true;
            Context.LyricLines.Clear();
            if (lines is null) return;
            Context.LyricLines.AddRange(lines);
            _keyFrameRendered.Clear();
            // 将 Id 换为 Index, 方便后续读取
            for (var i = 0; i < Context.LyricLines.Count; i++)
            {
                Context.LyricLines[i].Id = i;
            }

            _keyFrameRendered.Clear();
            _targetingKeyFrames.Clear();
            Context.RenderOffsets.Clear();
            _keyFrameRendered[0] = false; // 将 0 时刻添加到 KeyFrame, 以便初始化时渲染           
            // 初始化位置
            float topleftPosition = Context.ViewHeight * Context.LyricPaddingTopRatio;

            foreach (var renderingLyricLine in Context.LyricLines)
            {
                Context.RenderOffsets[renderingLyricLine.Id] = new LineRenderOffset
                {
                    X = 4,
                    Y = topleftPosition
                };
                Context.SnapshotRenderOffsets[renderingLyricLine.Id] = new LineRenderOffset();
                topleftPosition += renderingLyricLine.RenderingHeight + Context.LineSpacing;
                // 获取 Keyframe
                _keyFrameRendered[renderingLyricLine.StartTime] = false;
                _keyFrameRendered[renderingLyricLine.EndTime] = false;
                // 设置对照表
                if (!_targetingKeyFrames.ContainsKey(renderingLyricLine.StartTime))
                    _targetingKeyFrames[renderingLyricLine.StartTime] = new List<RenderingLyricLine>();
                if (!_targetingKeyFrames.ContainsKey(renderingLyricLine.EndTime))
                    _targetingKeyFrames[renderingLyricLine.EndTime] = new List<RenderingLyricLine>();
                _targetingKeyFrames[renderingLyricLine.StartTime].Add(renderingLyricLine);
                _targetingKeyFrames[renderingLyricLine.EndTime].Add(renderingLyricLine);
                if (renderingLyricLine.KeyFrames is not { Count: > 0 }) continue;

                // 添加自定义 KeyFrame
                foreach (var renderOptionsKey in renderingLyricLine.KeyFrames)
                {
                    if (!_targetingKeyFrames.ContainsKey(renderOptionsKey))
                        _targetingKeyFrames[renderOptionsKey] = new List<RenderingLyricLine>();
                    _targetingKeyFrames[renderOptionsKey].Add(renderingLyricLine);
                    _keyFrameRendered[renderOptionsKey] = false;
                }
            }

            _initializing = false;
        }

        private void RecalculateItemsSize(CanvasDrawingSession session)
        {
            try
            {
                Context.ItemWidth = Context.ViewWidth * Context.LyricWidthRatio;
                foreach (var renderingLyricLine in Context.LyricLines)
                {
                    renderingLyricLine.OnRenderSizeChanged(session, Context);
                }
            }
            catch
            {
                //Ignore
            }

        }

        private readonly Dictionary<long, bool> _keyFrameRendered = new();
        private readonly Dictionary<long, List<RenderingLyricLine>> _targetingKeyFrames = new();

        private void RecalculateRenderOffset(CanvasDrawingSession session)
        {
            if (Context.LyricLines is { Count: <= 0 }) return;
            Context.CurrentLyricLineIndex =
                Context.LyricLines.FindIndex(x =>
                    x.StartTime <= Context.CurrentLyricTime && x.EndTime >= Context.CurrentLyricTime);
            if (Context.CurrentLyricLineIndex < 0)
                Context.CurrentLyricLineIndex =
                    Context.LyricLines.FindIndex(x => x.StartTime >= Context.CurrentLyricTime);
            if (Context.CurrentLyricLineIndex < 0) Context.CurrentLyricLineIndex = Context.LyricLines.Count - 1;
            Context.CurrentLyricLine = Context.LyricLines[Context.CurrentLyricLineIndex];
            Context.RenderingLyricLines.Clear();
            var theoryRenderAfterPosition = Context.LyricPaddingTopRatio * Context.ViewHeight + Context.ScrollingDelta;
            var theoryRenderBeforePosition = theoryRenderAfterPosition;
            var renderedAfterStartPosition = theoryRenderAfterPosition;
            var renderedBeforeStartPosition = theoryRenderAfterPosition;

            for (var i = Context.CurrentLyricLineIndex; i < Context.LyricLines.Count; i++)
            {
                var currentLine = Context.LyricLines[i];
                if (currentLine.Hidden || Context.CurrentKeyframe == 0)
                {
                    Context.RenderOffsets[currentLine.Id].Y = theoryRenderAfterPosition;
                }

                if (currentLine.Hidden)
                    continue;
                if (renderedAfterStartPosition <= Context.ViewHeight && (Context.IsPlaying || !Context.IsScrolling) && !Context.IsSeek) // 在可视区域, 需要缓动
                    if (Context.SnapshotRenderOffsets.ContainsKey(currentLine.Id) &&
                        Math.Abs(theoryRenderAfterPosition - Context.RenderOffsets[currentLine.Id].Y) >
                        Epsilon)
                    {

                        renderedAfterStartPosition = Context.LineRollingEaseCalculator.CalculateCurrentY(
                            Context.SnapshotRenderOffsets[currentLine.Id].Y, theoryRenderAfterPosition,
                            currentLine, Context);
                        if (Context.Debug)
                        {
                            session.DrawText(renderedAfterStartPosition.ToString(), 0, renderedAfterStartPosition,
                                Colors.Green);
                        }

                        _needRecalculate = true; // 滚动中, 下一帧继续渲染
                    }

                Context.RenderOffsets[currentLine.Id].Y = renderedAfterStartPosition;
                if (renderedAfterStartPosition <= Context.ViewHeight) Context.RenderingLyricLines.Add(currentLine);
                theoryRenderAfterPosition += currentLine.RenderingHeight + Context.LineSpacing;
                renderedAfterStartPosition += currentLine.RenderingHeight + Context.LineSpacing;
            }

            // 算之前的
            for (var i = Context.CurrentLyricLineIndex - 1; i >= 0; i--)
            {
                var currentLine = Context.LyricLines[i];
                if (currentLine.Hidden || Context.CurrentKeyframe == 0)
                {
                    Context.RenderOffsets[currentLine.Id].Y = renderedBeforeStartPosition;
                    renderedBeforeStartPosition -= currentLine.Hidden ? 0 : currentLine.RenderingHeight;
                    theoryRenderBeforePosition -= currentLine.Hidden ? 0 : currentLine.RenderingHeight;
                }

                if (!currentLine.Hidden && Context.CurrentKeyframe != 0)
                {
                    // 行前也要算一下
                    renderedBeforeStartPosition -= currentLine.RenderingHeight + Context.LineSpacing;
                    theoryRenderBeforePosition -= currentLine.RenderingHeight + Context.LineSpacing;
                    if (renderedBeforeStartPosition + currentLine.RenderingHeight > 0) // 可见区域, 需要判断缓动
                    {
                        if (Context.SnapshotRenderOffsets.ContainsKey(currentLine.Id) &&
                            Math.Abs(Context.RenderOffsets[currentLine.Id].Y - theoryRenderBeforePosition) >
                            Epsilon &&
                            (Context.IsPlaying ||
                            !Context.IsScrolling)
                            && !Context.IsSeek)
                        {
                            renderedBeforeStartPosition = Context.LineRollingEaseCalculator.CalculateCurrentY(
                                Context.SnapshotRenderOffsets[currentLine.Id].Y, theoryRenderBeforePosition,
                                currentLine, Context);
                            if (Context.Debug)
                            {
                                session.DrawText(renderedBeforeStartPosition.ToString(), 0, renderedBeforeStartPosition,
                                    Colors.Green);
                            }

                            _needRecalculate = true; // 滚动中, 下一帧继续渲染
                        }

                        if (renderedBeforeStartPosition + currentLine.RenderingHeight > 0)
                            Context.RenderingLyricLines.Add(currentLine);
                        Context.RenderingLyricLines.Add(currentLine);
                    }
                }
                else
                {
                    renderedBeforeStartPosition = theoryRenderBeforePosition;
                }


                Context.RenderOffsets[currentLine.Id].Y = renderedBeforeStartPosition;
            }
        }


        private void LyricView_Draw(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender,
            Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedDrawEventArgs args)
        {
            Context.RenderTick = args.Timing.TotalTime.Ticks;
            if (_initializing || Context.ViewHeight == 0 || Context.ViewWidth == 0) return;
            OnBeforeRender?.Invoke(this);
            // 鼠标滚轮时间 5 s 清零
            if ((Context.ScrollingDelta != 0 || (Context.IsScrolling && !_pointerPressed)) && Context.RenderTick - _lastWheelTime > 50000000 && Context.IsPlaying || Context.IsSeek)
            {
                // 缓动来一下吧
                // 0.5 秒缓动到 0
                Context.IsScrolling = false;
                var progress = Math.Clamp((Context.RenderTick - _lastWheelTime - 50000000) / 5000000.0, 0, 1);
                Context.ScrollingDelta = (int)(Context.ScrollingDelta * _circleEase.Ease(1 - progress));
                if (Math.Abs(progress - 1) < Epsilon || Context.IsSeek)
                {
                    _lastWheelTime = 0;
                    Context.ScrollingDelta = 0;
                }

                _needRecalculate = true;
            }

            if (_isTypographyChanged)
            {
                _isTypographyChanged = false;
                try
                {
                    foreach (var renderingLyricLine in Context.LyricLines)
                    {
                        renderingLyricLine.OnTypographyChanged(args.DrawingSession, Context);
                    }
                }
                catch
                {
                    //Ignore
                }
            }

            foreach (var key in _keyFrameRendered.Keys.ToArray())
            {
                if (_keyFrameRendered[key]) continue;
                if (key >= Context.CurrentLyricTime && key != 0) continue;
                // 该 KeyFrame 尚未渲染
                _keyFrameRendered[key] = true;
                //if (!_needRecalculate)
                Context.CurrentKeyframe = key;
                // 视图快照
                //if (!_needRecalculate)
                foreach (var (i, value) in Context.RenderOffsets)
                {
                    Context.SnapshotRenderOffsets[i].Y = value.Y;
                }


                // 0 时刻渲染所有, 也就是初始化
                var targets = key == 0 ? Context.LyricLines : _targetingKeyFrames[key];
                foreach (var renderingLyricLine in targets)
                {
                    renderingLyricLine.OnKeyFrame(args.DrawingSession, Context);
                }

                _needRecalculate = true;
                _needRecalculateSize = true;
            }

            if (_needRecalculateSize)
            {
                _needRecalculateSize = false;
                RecalculateItemsSize(args.DrawingSession);
            }

            if (_needRecalculate)
            {
                _needRecalculate = false;
                RecalculateRenderOffset(args.DrawingSession);
            }

            foreach (var renderingLyricLine in Context.RenderingLyricLines)
            {
                if (Context.RenderOffsets.GetValueOrDefault(renderingLyricLine.Id) is { } offset)
                {
                    var doRender = renderingLyricLine.Render(args.DrawingSession, offset, Context);
                    if (doRender == false) break;
                }
            }

            if (Context.Debug)
            {
                args.DrawingSession.DrawText($"绘制时间: {args.Timing.ElapsedTime}", 0, 0, Colors.Yellow);
                args.DrawingSession.DrawText($"滚动偏移: {Context.ScrollingDelta}", 0, 15, Colors.Yellow);
                args.DrawingSession.DrawText($"歌词时间: {Context.CurrentLyricTime}", 0, 30, Colors.Yellow);
                args.DrawingSession.DrawText($"绘制行数: {Context.RenderingLyricLines.Count}", 0, 45, Colors.Yellow);
                // 绘制绘制边框
                args.DrawingSession.DrawRectangle(0, 0, Context.ViewWidth, Context.ViewHeight, Colors.Red, 5);
            }

            args.DrawingSession.Dispose();
        }

        public void Redesign(float width, float height)
        {
            Context.ViewWidth = width;
            Context.ViewHeight = height;
            _needRecalculateSize = true;
            _needRecalculate = true;
            _lastRedesignedTime = Context.RenderTick;
        }
        private void LyricView_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Context.RenderTick - _lastRedesignedTime > 5000000)
            {
                Redesign((float)e.NewSize.Width, (float)e.NewSize.Height);
            }
        }

        private long _lastWheelTime;

        private void LyricView_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            var min = -(long)Context.LyricLines.Where(p => Context.LyricLines.IndexOf(p) >= Context.CurrentLyricLineIndex)
                .Sum(p => p.RenderingHeight);
            var max = (long)Context.LyricLines.Where(p => Context.LyricLines.IndexOf(p) < Context.CurrentLyricLineIndex)
                .Sum(p => p.RenderingHeight);
            Context.ScrollingDelta = Math.Clamp(Context.ScrollingDelta + delta, min, max); //限制滚动范围
            Context.IsScrolling = true;
            _lastWheelTime = Context.RenderTick;
            _needRecalculate = true;
        }


        private void LyricView_OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // 指针事件
            // 获取在指针范围的行
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var focusingLine = -1;
                foreach (var renderOffsetsKey in Context.RenderOffsets.Keys)
                {
                    if (Context.LyricLines[renderOffsetsKey].Hidden)
                        continue;
                    if (Context.RenderOffsets[renderOffsetsKey].Y <= e.GetCurrentPoint(this).Position.Y &&
                        Context.RenderOffsets[renderOffsetsKey].Y + Context.LyricLines[renderOffsetsKey].RenderingHeight >=
                        e.GetCurrentPoint(this).Position.Y)
                    {
                        if (Context.PointerFocusingIndex == renderOffsetsKey) return;
                        Context.LyricLines[renderOffsetsKey].GoToReactionState(ReactionState.Enter, Context);
                        focusingLine = renderOffsetsKey;
                        break;
                    }
                }

                if (Context.PointerFocusingIndex != focusingLine)
                {
                    if (Context.PointerFocusingIndex != -1 && Context.LyricLines.Count > (Context.PointerFocusingIndex))
                        Context.LyricLines[Context.PointerFocusingIndex].GoToReactionState(ReactionState.Leave, Context);
                    Context.PointerFocusingIndex = focusingLine;
                }
            }
            else if (_pointerPressed == true && _lastPointerPressedYValue != null)
            {
                var yValue = e.GetCurrentPoint(this).Position.Y;
                var delta = (long)(yValue - _lastPointerPressedYValue);
                if (Math.Abs(delta) > 20)
                {
                    _lastPointerPressedYValue = yValue;
                    return;
                }
                var min = -(long)Context.LyricLines.Where(p => p.StartTime > Context.CurrentLyricTime)
                    .Sum(p => p.RenderingHeight);
                var max = (long)Context.LyricLines.Where(p => p.EndTime < Context.CurrentLyricTime)
                    .Sum(p => p.RenderingHeight);
                Context.ScrollingDelta = Math.Clamp(Context.ScrollingDelta + 2 * delta, min, max); //限制滚动范围
                //Debug.WriteLine(Context.ScrollingDelta);
                Context.IsScrolling = true;
                _lastWheelTime = Context.RenderTick;
                _needRecalculate = true;
                _lastPointerPressedYValue = yValue;
            }
            else if (_lastPointerPressedYValue == null)
            {
                _lastPointerPressedYValue = e.GetCurrentPoint(this).Position.Y;
            }
        }

        private void LyricRenderView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            LyricView.RemoveFromVisualTree();
            LyricView = null;
        }

        private void LyricView_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (Context.PointerFocusingIndex != -1 && Context.LyricLines.Count > Context.PointerFocusingIndex)
                Context.LyricLines[Context.PointerFocusingIndex].GoToReactionState(ReactionState.Leave, Context);
            Context.PointerFocusingIndex = -1;
            _pointerPressed = false;
            _lastPointerPressedYValue = null;
        }


        private void LyricView_OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = true;
        }

        private void LyricView_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = false;
            _lastPointerPressedYValue = null;
        }

        private void LyricView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {

            foreach (var renderOffsetsKey in Context.RenderOffsets.Keys)
            {
                if (Context.LyricLines[renderOffsetsKey].Hidden)
                    continue;
                if (Context.RenderOffsets[renderOffsetsKey].Y <= e.GetPosition(this).Y &&
                    Context.RenderOffsets[renderOffsetsKey].Y + Context.LyricLines[renderOffsetsKey].RenderingHeight >=
                    e.GetPosition(this).Y)
                {
                    Context.LyricLines[renderOffsetsKey].GoToReactionState(ReactionState.Press, Context);
                    OnRequestSeek?.Invoke(Context.LyricLines[renderOffsetsKey].StartTime);
                    _jumpedLyrics = true;
                    break;
                }

            }
            Context.ScrollingDelta = 0;
            _pointerPressed = true;

        }
        private void LyricView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _jumpedLyrics = false;
        }
        public void PauseLyricRender(bool targetPauseMode)
        {
            LyricView.Paused = targetPauseMode;
        }
    }
}
