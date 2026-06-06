#nullable disable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public static partial class VoiceMessageHelper
    {
        private static ControlTemplate _cachedSliderTemplate;
        private static readonly object _templateLock = new object();

        private static Button CreateCircularButton(Color bgColor, Brush foreground)
        {
            var playButton = new Button
            {
                Content = "\u25B6",
                Width = 40,
                Height = 40,
                FontSize = 16,
                Background = new SolidColorBrush(bgColor),
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                Tag = "play",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var btnTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(20));

            var backgroundBinding = new Binding("Background")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            };
            var borderBrushBinding = new Binding("BorderBrush")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            };

            borderFactory.SetBinding(Border.BackgroundProperty, backgroundBinding);
            borderFactory.SetBinding(Border.BorderBrushProperty, borderBrushBinding);
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenter);
            btnTemplate.VisualTree = borderFactory;
            playButton.Template = btnTemplate;

            return playButton;
        }

        private static Slider CreateCustomSlider()
        {
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Height = 24,
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                FocusVisualStyle = null
            };

            if (_cachedSliderTemplate == null)
            {
                lock (_templateLock)
                {
                    if (_cachedSliderTemplate == null)
                    {
                        var sliderTemplateXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
               xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
               TargetType='Slider'> <Grid Height='24' VerticalAlignment='Center'> <Border x:Name='BaseTrack' Height='4' VerticalAlignment='Center' CornerRadius='2'
         Background='#D0D0D0' BorderThickness='0' Focusable='False'/> <Grid> <Border x:Name='ProgressTrack' Height='4' VerticalAlignment='Center' CornerRadius='2'
           Background='#808080' HorizontalAlignment='Left' Width='0' BorderThickness='0' Focusable='False'/> <Track x:Name='PART_Track' VerticalAlignment='Center' Focusable='False'>
<Track.DecreaseRepeatButton> <RepeatButton Command='Slider.DecreaseLarge' Background='Transparent'
                     BorderThickness='0' BorderBrush='Transparent' IsTabStop='False'/>
</Track.DecreaseRepeatButton>
<Track.IncreaseRepeatButton> <RepeatButton Command='Slider.IncreaseLarge' Background='Transparent'
                     BorderThickness='0' BorderBrush='Transparent' IsTabStop='False'/>
</Track.IncreaseRepeatButton>
<Track.Thumb> <Thumb Width='14' Height='14' Focusable='False'>
<Thumb.Template> <ControlTemplate TargetType='Thumb'> <Ellipse Width='14' Height='14' Fill='{TemplateBinding Background}' StrokeThickness='0'/> </ControlTemplate>
</Thumb.Template> </Thumb>
</Track.Thumb> </Track> </Grid> </Grid> </ControlTemplate>";

                        _cachedSliderTemplate = (ControlTemplate)XamlReader.Parse(sliderTemplateXaml);
                    }
                }
            }

            if (_cachedSliderTemplate != null)
            {
                slider.Template = _cachedSliderTemplate;
            }

            return slider;
        }

        private static void ApplySliderColors(Slider slider, Brush progressBrush)
        {
            if (slider == null)
            {
                return;
            }

            slider.Foreground = progressBrush;

            try
            {
                if (slider.Template != null)
                {
                    var progressTrack = slider.Template.FindName("ProgressTrack", slider) as Border;
                    if (progressTrack != null)
                    {
                        progressTrack.Background = progressBrush;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
