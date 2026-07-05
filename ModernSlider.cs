using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LuminaControl
{
    public class ModernSlider : UserControl
    {
        private Border _trackBg;
        private Border _trackFill;
        private Border _thumb;
        
        private ScaleTransform _trackFillTransform;
        private TranslateTransform _thumbTransform;
        
        private bool _isDragging = false;

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(ModernSlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChangedInternal));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, Math.Min(100, Math.Max(0, value))); }
        }

        public event RoutedPropertyChangedEventHandler<double> ValueChanged;

        private static void OnValueChangedInternal(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var slider = (ModernSlider)d;
            slider.UpdateVisuals();
            if (slider.ValueChanged != null)
            {
                slider.ValueChanged(slider, new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue));
            }
        }

        public ModernSlider()
        {
            this.Height = 28;
            this.Background = Brushes.Transparent;

            var grid = new Grid();
            grid.Background = Brushes.Transparent; // For hit testing

            _trackBg = new Border
            {
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x35)),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };

            _trackFill = new Border
            {
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(8, 0, 8, 0),
                RenderTransformOrigin = new Point(0, 0.5)
            };

            _trackFillTransform = new ScaleTransform(0, 1);
            _trackFill.RenderTransform = _trackFillTransform;

            _thumb = new Border
            {
                Width = 16,
                Height = 16,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)),
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _thumbTransform = new TranslateTransform(0, 0);
            _thumb.RenderTransform = _thumbTransform;

            var shadow = new DropShadowEffect
            {
                Color = Color.FromRgb(0x63, 0x66, 0xF1),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.5
            };
            _thumb.Effect = shadow;

            grid.Children.Add(_trackBg);
            grid.Children.Add(_trackFill);
            grid.Children.Add(_thumb);

            this.Content = grid;

            this.SizeChanged += (s, e) => UpdateVisuals();
            
            grid.MouseDown += Grid_MouseDown;
            grid.MouseMove += Grid_MouseMove;
            grid.MouseUp += Grid_MouseUp;

            grid.MouseEnter += (s, e) =>
            {
                _thumb.Background = Brushes.White;
                _thumb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
            };
            grid.MouseLeave += (s, e) =>
            {
                if (!_isDragging)
                {
                    _thumb.Background = Brushes.White;
                    _thumb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
                }
            };
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var element = (UIElement)sender;
                _isDragging = true;
                element.CaptureMouse(); // Capture mouse on the Grid itself
                UpdateValueFromMouse(e.GetPosition(this));
            }
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateValueFromMouse(e.GetPosition(this));
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                var element = (UIElement)sender;
                _isDragging = false;
                element.ReleaseMouseCapture(); // Release mouse capture on the Grid itself
                _thumb.Background = Brushes.White;
                _thumb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
            }
        }

        private void UpdateValueFromMouse(Point p)
        {
            double width = this.ActualWidth;
            double trackWidth = width - 16;
            if (trackWidth <= 0) return;

            double relativeX = p.X - 8;
            double percentage = (relativeX / trackWidth) * 100;
            this.Value = Math.Min(100, Math.Max(0, percentage));
        }

        private void UpdateVisuals()
        {
            double width = this.ActualWidth;
            double trackWidth = width - 16;
            if (trackWidth <= 0) return;

            double ratio = this.Value / 100.0;
            _trackFillTransform.ScaleX = ratio;
            _thumbTransform.X = trackWidth * ratio;
        }
    }
}
