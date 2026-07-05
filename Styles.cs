using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LuminaControl;

public static class Styles
{
    public static readonly Brush BrushBg = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14));
    public static readonly Brush BrushCard = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x21));
    public static readonly Brush BrushBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x35));
    public static readonly Brush BrushBorderHover = new SolidColorBrush(Color.FromRgb(0x3E, 0x44, 0x4D));
    public static readonly Brush BrushText = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
    public static readonly Brush BrushTextMuted = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    public static readonly Brush BrushAccent = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
    public static readonly Brush BrushAccentHover = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));

    public static readonly FontFamily MainFont = new("Segoe UI Semibold, Segoe UI, Arial");

    public static void StyleCloseButton(Button btn)
    {
        btn.Background = Brushes.Transparent;
        btn.Foreground = BrushTextMuted;
        btn.BorderThickness = new Thickness(0);
        btn.Width = 44;
        btn.Height = 42;
        btn.FontFamily = MainFont;
        btn.FontSize = 11;
        btn.FontWeight = FontWeights.Bold;
        btn.Focusable = false;

        ControlTemplate template = new(typeof(Button));
        FrameworkElementFactory border = new(typeof(Border));
        border.Name = "btnBorder";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        
        FrameworkElementFactory contentPresenter = new(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        btn.Template = template;

        btn.MouseEnter += (s, e) =>
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red
            btn.Foreground = Brushes.White;
        };
        btn.MouseLeave += (s, e) =>
        {
            btn.Background = Brushes.Transparent;
            btn.Foreground = BrushTextMuted;
        };
    }

    public static void StyleMinButton(Button btn)
    {
        btn.Background = Brushes.Transparent;
        btn.Foreground = BrushTextMuted;
        btn.BorderThickness = new Thickness(0);
        btn.Width = 44;
        btn.Height = 42;
        btn.FontFamily = MainFont;
        btn.FontSize = 11;
        btn.Focusable = false;

        ControlTemplate template = new(typeof(Button));
        FrameworkElementFactory border = new(typeof(Border));
        border.Name = "btnBorder";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        
        FrameworkElementFactory contentPresenter = new(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        btn.Template = template;

        btn.MouseEnter += (s, e) =>
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x33, 0x3D));
            btn.Foreground = BrushText;
        };
        btn.MouseLeave += (s, e) =>
        {
            btn.Background = Brushes.Transparent;
            btn.Foreground = BrushTextMuted;
        };
    }

    public static void StyleActionButton(Button btn)
    {
        btn.Background = BrushCard;
        btn.Foreground = BrushText;
        btn.BorderBrush = BrushBorder;
        btn.BorderThickness = new Thickness(1);
        btn.Padding = new Thickness(14, 8, 14, 8);
        btn.FontFamily = MainFont;
        btn.FontSize = 11;
        btn.FontWeight = FontWeights.SemiBold;
        btn.Focusable = false;

        ControlTemplate template = new(typeof(Button));
        FrameworkElementFactory border = new(typeof(Border));
        border.Name = "btnBorder";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

        FrameworkElementFactory contentPresenter = new(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        btn.Template = template;

        btn.MouseEnter += (s, e) =>
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x25, 0x2A));
            btn.BorderBrush = BrushBorderHover;
        };
        btn.MouseLeave += (s, e) =>
        {
            btn.Background = BrushCard;
            btn.BorderBrush = BrushBorder;
        };
    }

    public static void StyleCheckBoxAsSwitch(CheckBox cb)
    {
        cb.Focusable = false;
        cb.Cursor = Cursors.Hand;
        
        ControlTemplate template = new(typeof(CheckBox));
        FrameworkElementFactory grid = new(typeof(Grid));
        
        // Switch track
        FrameworkElementFactory track = new(typeof(Border));
        track.Name = "track";
        track.SetValue(Border.WidthProperty, 32.0);
        track.SetValue(Border.HeightProperty, 16.0);
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        track.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x35)));
        track.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x3E, 0x44, 0x4D)));
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        track.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        grid.AppendChild(track);

        // Switch thumb (circle)
        FrameworkElementFactory thumb = new(typeof(Border));
        thumb.Name = "thumb";
        thumb.SetValue(Border.WidthProperty, 10.0);
        thumb.SetValue(Border.HeightProperty, 10.0);
        thumb.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        thumb.SetValue(Border.BackgroundProperty, BrushTextMuted);
        thumb.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        thumb.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
        thumb.SetValue(Border.MarginProperty, new Thickness(3, 0, 3, 0));
        grid.AppendChild(thumb);

        // Label Content
        FrameworkElementFactory content = new(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.MarginProperty, new Thickness(40, 0, 0, 0));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        grid.AppendChild(content);

        template.VisualTree = grid;
        cb.Template = template;

        Action updateSwitch = () =>
        {
            cb.ApplyTemplate();
            var t = cb.Template.FindName("track", cb) as Border;
            var th = cb.Template.FindName("thumb", cb) as Border;
            if (t != null && th != null)
            {
                if (cb.IsChecked == true)
                {
                    t.Background = BrushAccent;
                    t.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
                    th.Background = Brushes.White;
                    th.Margin = new Thickness(17, 0, 3, 0); // Slide right
                }
                else
                {
                    t.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x35));
                    t.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x44, 0x4D));
                    th.Background = BrushTextMuted;
                    th.Margin = new Thickness(3, 0, 3, 0); // Slide left
                }
            }
        };

        cb.Loaded += (s, e) => updateSwitch();
        cb.Checked += (s, e) => updateSwitch();
        cb.Unchecked += (s, e) => updateSwitch();
    }
}
