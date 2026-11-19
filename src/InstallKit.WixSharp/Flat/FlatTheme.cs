using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;

namespace InstallKit.WixSharp.Flat
{
    /// <summary>
    /// The look of the flat dialogs: brushes in the user's colours and flat templates for buttons, check boxes, text boxes and
    /// the progress bar. Written as XAML text and parsed at install time, so the project needs no XAML build step.
    /// </summary>
    internal static class FlatTheme
    {
        public static ResourceDictionary Create(Palette palette)
        {
            var text = Template
                .Replace("{BG}", Hex(palette.Background))
                .Replace("{SURFACE}", Hex(palette.Surface))
                .Replace("{HOVER}", Hex(palette.Surface.Mix(palette.Text, 0.10)))
                .Replace("{TEXT}", Hex(palette.Text))
                .Replace("{MUTED}", Hex(palette.Muted))
                .Replace("{ACCENT}", Hex(palette.Accent))
                .Replace("{ACCENTHOVER}", Hex(palette.Accent.Mix(new Rgb(255, 255, 255), 0.15)))
                .Replace("{ACCENTTEXT}", palette.Accent.Luminance > 0.6 ? "#18181B" : "#FFFFFF")
                .Replace("{LINE}", Hex(palette.Background.Mix(palette.Text, 0.18)));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                return (ResourceDictionary)XamlReader.Load(stream);
        }

        static string Hex(Rgb color) => "#" + color.Hex;

        const string Template = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='Bg' Color='{BG}'/>
  <SolidColorBrush x:Key='Surface' Color='{SURFACE}'/>
  <SolidColorBrush x:Key='Hover' Color='{HOVER}'/>
  <SolidColorBrush x:Key='Text' Color='{TEXT}'/>
  <SolidColorBrush x:Key='Muted' Color='{MUTED}'/>
  <SolidColorBrush x:Key='Accent' Color='{ACCENT}'/>
  <SolidColorBrush x:Key='AccentHover' Color='{ACCENTHOVER}'/>
  <SolidColorBrush x:Key='AccentText' Color='{ACCENTTEXT}'/>
  <SolidColorBrush x:Key='Line' Color='{LINE}'/>

  <Style TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Background' Value='{StaticResource Surface}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Padding' Value='22,9'/>
    <Setter Property='MinWidth' Value='96'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Face' Background='{TemplateBinding Background}' CornerRadius='3' Padding='{TemplateBinding Padding}'
                  BorderThickness='1' BorderBrush='{StaticResource Line}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='False'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Face' Property='Background' Value='{StaticResource Hover}'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'>
              <Setter TargetName='Face' Property='BorderBrush' Value='{StaticResource Accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.4'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='Foreground' Value='{StaticResource AccentText}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Face' Background='{StaticResource Accent}' CornerRadius='3' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='False'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Face' Property='Background' Value='{StaticResource AccentHover}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.4'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <Grid Background='Transparent'>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width='Auto'/>
              <ColumnDefinition Width='*'/>
            </Grid.ColumnDefinitions>
            <Border x:Name='Box' Width='18' Height='18' CornerRadius='3' BorderThickness='1.5' VerticalAlignment='Top' Margin='0,1,0,0'
                    BorderBrush='{StaticResource Muted}' Background='Transparent'>
              <Path x:Name='Tick' Data='M3,9 L7,13 L15,4' Stroke='{StaticResource AccentText}' StrokeThickness='2'
                    StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Visibility='Collapsed'/>
            </Border>
            <ContentPresenter Grid.Column='1' Margin='10,0,0,0' VerticalAlignment='Top' RecognizesAccessKey='False'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Box' Property='Background' Value='{StaticResource Accent}'/>
              <Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource Accent}'/>
              <Setter TargetName='Tick' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource Accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='CaretBrush' Value='{StaticResource Text}'/>
    <Setter Property='SelectionBrush' Value='{StaticResource Accent}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Padding' Value='10,8'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='Frame' Background='{StaticResource Surface}' CornerRadius='3' BorderThickness='1' BorderBrush='{StaticResource Line}'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocused' Value='True'>
              <Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource Accent}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ScrollPage' TargetType='RepeatButton'>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RepeatButton'>
          <Border Background='Transparent'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ScrollThumb' TargetType='Thumb'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Thumb'>
          <Border Background='{StaticResource Hover}' CornerRadius='3'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='10'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Grid Background='Transparent'>
            <Track x:Name='PART_Track' IsDirectionReversed='True'>
              <Track.DecreaseRepeatButton>
                <RepeatButton Style='{StaticResource ScrollPage}' Command='ScrollBar.PageUpCommand'/>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb Style='{StaticResource ScrollThumb}'/>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Style='{StaticResource ScrollPage}' Command='ScrollBar.PageDownCommand'/>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ProgressBar'>
    <Setter Property='Height' Value='6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ProgressBar'>
          <Grid>
            <Border Background='{StaticResource Surface}' CornerRadius='3'/>
            <Border x:Name='PART_Track'>
              <Border x:Name='PART_Indicator' Background='{StaticResource Accent}' CornerRadius='3' HorizontalAlignment='Left'/>
            </Border>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='TextBlock'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='TextWrapping' Value='Wrap'/>
  </Style>
</ResourceDictionary>";
    }
}
