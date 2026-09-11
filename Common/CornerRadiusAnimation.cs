using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace MinecraftLauncher.Common
{
    public class CornerRadiusAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(CornerRadius);

        protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(CornerRadius), typeof(CornerRadiusAnimation));

        public CornerRadius From
        {
            get => (CornerRadius)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(CornerRadius), typeof(CornerRadiusAnimation));

        public CornerRadius To
        {
            get => (CornerRadius)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(CornerRadiusAnimation));

        public IEasingFunction? EasingFunction
        {
            get => (IEasingFunction?)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            if (!animationClock.CurrentProgress.HasValue) return From;

            double progress = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
            {
                progress = EasingFunction.Ease(progress);
            }

            return new CornerRadius(
                From.TopLeft + (To.TopLeft - From.TopLeft) * progress,
                From.TopRight + (To.TopRight - From.TopRight) * progress,
                From.BottomRight + (To.BottomRight - From.BottomRight) * progress,
                From.BottomLeft + (To.BottomLeft - From.BottomLeft) * progress
            );
        }
    }
}
