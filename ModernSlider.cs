using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LuminaControl;

public class ModernSlider : UserControl
{
    private readonly Border _trackBg;
    private readonly Border _trackFill;
    private readonly Border _thumb;
    
    private readonly ScaleTransform _trackFillTransform;
    private readonly TranslateTransform _thumbTransform;
    
    private bool _isDragging;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ModernSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChangedInternal));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, 0, 100));
    }

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

    private static void OnValueChangedInternal(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = (ModernSlider)d;
        slider.UpdateVisuals();
        slider.ValueChanged?.Invoke(slider, new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue));
    }

    public ModernSlider()
    {
        Height = 28;
        Background = Brushes.Transparent;

        var grid = new Grid { Background = Brushes.Transparent };

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
            HorizontalAlignment = HorizontalAlignment.Left
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

        Content = grid;

        SizeChanged += (s, e) => UpdateVisuals();
        
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
            element.CaptureMouse();
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
            element.ReleaseMouseCapture();
            _thumb.Background = Brushes.White;
            _thumb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
        }
    }

    private void UpdateValueFromMouse(Point p)
    {
        double width = ActualWidth;
        double trackWidth = width - 16;
        if (trackWidth <= 0) return;

        double relativeX = p.X - 8;
        double percentage = (relativeX / trackWidth) * 100;
        Value = Math.Clamp(percentage, 0, 100);
    }

    private void UpdateVisuals()
    {
        double width = ActualWidth;
        double trackWidth = width - 16;
        if (trackWidth <= 0) return;

        double ratio = Value / 100.0;
        _trackFillTransform.ScaleX = ratio;
        _thumbTransform.X = trackWidth * ratio;
    }
}
