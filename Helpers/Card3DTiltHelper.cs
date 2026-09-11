using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MinecraftLauncher.Helpers
{
    public static class Card3DTiltHelper
    {
        public static readonly DependencyProperty Enable3DTiltProperty =
            DependencyProperty.RegisterAttached(
                "Enable3DTilt",
                typeof(bool),
                typeof(Card3DTiltHelper),
                new PropertyMetadata(false, OnEnable3DTiltChanged));

        public static bool GetEnable3DTilt(DependencyObject obj) => (bool)obj.GetValue(Enable3DTiltProperty);
        public static void SetEnable3DTilt(DependencyObject obj, bool value) => obj.SetValue(Enable3DTiltProperty, value);

        private class ElementGlowState
        {
            public bool IsHovered;
            public double TargetGlowOpacity;
            public double CurrentGlowOpacity;
            public double TargetScale = 1.0;
            public double CurrentScale = 1.0;
            public RadialGradientBrush? LightGlow;
            public Point TargetGlowCenter;
            public Point CurrentGlowCenter;
        }

        private static readonly ConcurrentDictionary<FrameworkElement, ElementGlowState> States = new();
        private static bool _isHookedRendering;

        private static void OnEnable3DTiltChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.Loaded += Element_Loaded;
                    element.MouseMove += Element_MouseMove;
                    element.MouseEnter += Element_MouseEnter;
                    element.MouseLeave += Element_MouseLeave;
                    element.PreviewMouseDown += Element_MouseDown;
                    element.PreviewMouseUp += Element_MouseUp;
                }
                else
                {
                    element.Loaded -= Element_Loaded;
                    element.MouseMove -= Element_MouseMove;
                    element.MouseEnter -= Element_MouseEnter;
                    element.MouseLeave -= Element_MouseLeave;
                    element.PreviewMouseDown -= Element_MouseDown;
                    element.PreviewMouseUp -= Element_MouseUp;
                    States.TryRemove(element, out _);
                }

                EnsureRenderingHook();
            }
        }

        private static void EnsureRenderingHook()
        {
            if (!_isHookedRendering)
            {
                CompositionTarget.Rendering += OnRendering;
                _isHookedRendering = true;
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.RenderTransform is not ScaleTransform)
                {
                    var scale = new ScaleTransform(1, 1);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = scale;
                }

                var state = States.GetOrAdd(element, _ => new ElementGlowState());

                if (element is Border border && border.Child is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is Border b && b.Name == "__GlossGlowLayer__") return;
                    }

                    var glowLayer = new Border
                    {
                        Name = "__GlossGlowLayer__",
                        IsHitTestVisible = false,
                        CornerRadius = border.CornerRadius,
                        Margin = new Thickness(-border.Padding.Left, -border.Padding.Top, -border.Padding.Right, -border.Padding.Bottom),
                        Opacity = 0.0
                    };

                    var radialBrush = new RadialGradientBrush
                    {
                        Center = new Point(0.5, 0.5),
                        GradientOrigin = new Point(0.5, 0.5),
                        RadiusX = 1.4,
                        RadiusY = 1.4
                    };

                    radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(75, 255, 255, 255), 0.0));
                    radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(25, 255, 255, 255), 0.55));
                    radialBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));

                    glowLayer.Background = radialBrush;
                    state.LightGlow = radialBrush;

                    Grid.SetRowSpan(glowLayer, 99);
                    Grid.SetColumnSpan(glowLayer, 99);
                    grid.Children.Add(glowLayer);
                }
            }
        }

        private static void Element_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
            {
                state.IsHovered = true;
                state.TargetGlowOpacity = 1.0;
                state.TargetScale = 1.015;
            }
        }

        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
            {
                double width = element.ActualWidth;
                double height = element.ActualHeight;
                if (width <= 0 || height <= 0) return;

                Point pos = e.GetPosition(element);
                state.TargetGlowCenter = new Point(pos.X / width, pos.Y / height);
            }
        }

        private static void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
            {
                state.IsHovered = false;
                state.TargetGlowOpacity = 0.0;
                state.TargetScale = 1.0;
            }
        }

        private static void Element_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
            {
                state.TargetScale = 0.98;
            }
        }

        private static void Element_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
            {
                if (state.IsHovered)
                {
                    state.TargetScale = 1.015;
                }
            }
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            foreach (var kvp in States)
            {
                var element = kvp.Key;
                var state = kvp.Value;

                if (!element.IsLoaded) continue;

                const double smoothFactor = 0.15;
                state.CurrentScale += (state.TargetScale - state.CurrentScale) * smoothFactor;
                state.CurrentGlowOpacity += (state.TargetGlowOpacity - state.CurrentGlowOpacity) * smoothFactor;

                state.CurrentGlowCenter = new Point(
                    state.CurrentGlowCenter.X + (state.TargetGlowCenter.X - state.CurrentGlowCenter.X) * smoothFactor,
                    state.CurrentGlowCenter.Y + (state.TargetGlowCenter.Y - state.CurrentGlowCenter.Y) * smoothFactor
                );

                if (element.RenderTransform is ScaleTransform scale)
                {
                    scale.ScaleX = state.CurrentScale;
                    scale.ScaleY = state.CurrentScale;
                }

                if (state.LightGlow != null)
                {
                    state.LightGlow.Center = state.CurrentGlowCenter;
                    state.LightGlow.GradientOrigin = state.CurrentGlowCenter;
                }

                if (element is Border border && border.Child is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is Border glowLayer && glowLayer.Name == "__GlossGlowLayer__")
                        {
                            glowLayer.Opacity = state.CurrentGlowOpacity;
                            break;
                        }
                    }
                }
            }
        }
    }
}
