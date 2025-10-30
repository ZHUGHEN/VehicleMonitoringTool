# Avalonia UI Learning Guide

## Overview
This guide walks you through the key concepts of Avalonia UI using your telemetry dashboard as an example. Avalonia is a cross-platform .NET UI framework that allows you to build beautiful, modern applications.

## Key Concepts Demonstrated

### 1. MVVM Pattern (Model-View-ViewModel)
Your application follows the MVVM pattern, which separates concerns:

- **Model**: Your telemetry data (`Telemetry.cs` in core project)
- **View**: XAML files (`MainWindow.axaml`, `AlternativeMainWindow.axaml`)
- **ViewModel**: `MainViewModel.cs` with data binding and `INotifyPropertyChanged`

```csharp
// ViewModel example - automatic property change notification
public double Rpm 
{ 
    get => _rpm; 
    private set { _rpm = value; OnChanged(); } 
}
```

### 2. Data Binding
Binding connects your UI to data in the ViewModel:

```xml
<!-- One-way binding with string formatting -->
<TextBlock Text="{Binding Rpm, StringFormat='{0:N0}'}" />

<!-- Binding to converter for conditional display -->
<TextBlock Text="{Binding IsConnected, Converter={x:Static BoolToStatusConverter.Instance}}" />
```

### 3. Layout Controls
Different layout controls serve different purposes:

- **Grid**: For structured layouts with rows/columns
- **StackPanel**: For sequential arrangement
- **UniformGrid**: For equal-sized items
- **Border**: For visual grouping and styling

```xml
<!-- Grid with row definitions -->
<Grid RowDefinitions="Auto,*,Auto">
  <!-- Header -->
  <!-- Content -->
  <!-- Footer -->
</Grid>
```

### 4. Styling and Theming

#### Resource Dictionaries
Define reusable styles in resources:

```xml
<Window.Resources>
  <!-- Reusable control theme -->
  <ControlTheme x:Key="TelemetryCard" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}" />
    <Setter Property="CornerRadius" Value="8" />
  </ControlTheme>
</Window.Resources>
```

#### Dynamic Resources
Use dynamic resources for theme-aware colors:

```xml
<Border Background="{DynamicResource CardBackgroundBrush}" />
```

### 5. Value Converters
Convert data between ViewModel and View:

```csharp
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Brushes.Green : Brushes.Red;
    }
}
```

### 6. Custom UserControls
Create reusable components:

```xml
<!-- Using the custom TelemetryGauge control -->
<controls:TelemetryGauge 
  Icon="⚡"
  DisplayValue="{Binding Rpm, StringFormat='{0:N0}'}"
  Unit="RPM"
  Value="{Binding Rpm}"
  Maximum="8000"
  AccentColor="#FF6B35" />
```

### 7. Animations and Transitions
Add smooth animations:

```xml
<Setter Property="Transitions">
  <Transitions>
    <DoubleTransition Property="Value" Duration="0:0:0.3" />
    <BoxShadowsTransition Property="BoxShadow" Duration="0:0:0.2" />
  </Transitions>
</Setter>
```

## File Structure Explained

```
cartelemetry.desktop/
├── MainWindow.axaml              # Main application window (enhanced version)
├── MainWindow.axaml.cs           # Code-behind for main window
├── AlternativeMainWindow.axaml   # Version using custom controls
├── MainViewModel.cs              # ViewModel with data binding
├── Converters.cs                 # Value converters for data transformation
├── Controls/
│   ├── TelemetryGauge.axaml     # Custom UserControl definition
│   └── TelemetryGauge.axaml.cs  # Custom UserControl code-behind
├── Themes/
│   └── AppTheme.axaml           # Custom theme and styles
└── App.axaml                    # Application-level resources and themes
```

## Best Practices Demonstrated

### 1. Separation of Concerns
- UI logic in XAML
- Business logic in ViewModel
- Data access in Model/Services

### 2. Responsive Design
- Use of `MinWidth`/`MinHeight`
- `ScrollViewer` for content overflow
- Grid definitions that adapt to content

### 3. Modern UI Design
- Card-based layouts
- Consistent spacing and margins
- Appropriate use of colors and typography
- Hover effects and animations

### 4. Accessibility
- Proper semantic structure
- Meaningful text and labels
- Good color contrast

## Next Steps for Learning

### 1. Explore More Controls
Try adding these controls to your dashboard:
- `Button` for user interactions
- `ComboBox` for settings
- `Slider` for adjusting values
- `DataGrid` for tabular data
- `Chart` controls for data visualization

### 2. Advanced Data Binding
- `MultiBinding` for complex scenarios
- `RelativeSource` binding
- `ElementName` binding between controls

### 3. Commands and MVVM
Implement commands for user actions:

```csharp
public ICommand ResetCommand { get; }
public ICommand SaveDataCommand { get; }
```

### 4. Navigation
- Multiple windows
- User controls as pages
- Navigation between views

### 5. Platform-Specific Features
- Platform detection
- Native integrations
- Platform-specific styling

## Resources for Further Learning

1. **Official Documentation**: https://docs.avaloniaui.net/
2. **Avalonia Samples**: https://github.com/AvaloniaUI/Avalonia.Samples
3. **FluentAvalonia**: Enhanced Fluent Design controls
4. **Avalonia Community**: Discord and GitHub discussions

## Common Patterns You Should Know

### Data Templates
For custom data display:

```xml
<DataTemplate x:Key="TelemetryItemTemplate">
  <Border>
    <StackPanel>
      <TextBlock Text="{Binding Name}" FontWeight="Bold" />
      <TextBlock Text="{Binding Value}" />
    </StackPanel>
  </Border>
</DataTemplate>
```

### Styles with Selectors
Target specific scenarios:

```xml
<Style Selector="Button:pointerover">
  <Setter Property="Background" Value="LightBlue" />
</Style>
```

### Reactive UI
For advanced scenarios with reactive programming:

```csharp
// Using ReactiveUI with Avalonia
this.WhenAnyValue(x => x.Rpm)
    .Where(rpm => rpm > 7000)
    .Subscribe(rpm => ShowWarning());
```

This guide should give you a solid foundation for building beautiful Avalonia applications!