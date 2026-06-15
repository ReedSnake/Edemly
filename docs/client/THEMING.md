# Client Theming

This document describes how the WPF client applies colors, brushes, styles, and theme changes.

Theme behavior should be resource-driven. Concrete pages and controls should use dynamic resources instead of manually repainting the UI whenever possible.

## Contents

* [Overview](#overview)
* [Main Components](#main-components)
* [Theme Lifecycle](#theme-lifecycle)
* [Resource Dictionaries](#resource-dictionaries)
* [Using Theme Resources](#using-theme-resources)
* [Adding a Theme](#adding-a-theme)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

The current theme system is built around:

```text
Application/Theme/ThemeService.cs
Presentation/Common/ThemedPage.cs
Presentation/Common/ThemedWindow.cs
Presentation/Common/ThemedUserControl.cs
Presentation/Resources
```

`ThemeService` stores available palettes and writes theme colors and brushes into `Application.Current.Resources`.

WPF UI should bind to those resources through `DynamicResource` or `SetResourceReference` so changes are applied without manual UI traversal.

## Main Components

| Component | Responsibility |
| --------- | -------------- |
| `ThemeService` | stores palettes, applies resources, persists selected theme |
| `ThemePalette` | contains raw palette colors |
| `ThemedPage` | subscribes/unsubscribes to theme changes for pages |
| `ThemedWindow` | subscribes/unsubscribes to theme changes for windows |
| `ThemedUserControl` | subscribes/unsubscribes to theme changes for controls |
| `Presentation/Resources/*.xaml` | shared colors, brushes, styles, fonts, controls, animations |

## Theme Lifecycle

The lifecycle is:

1. `ConfigService` loads the saved theme name.
2. `ThemeService.LoadAndApplyTheme` applies the palette to application resources.
3. The active page/window/control receives WPF resources through `DynamicResource`.
4. Settings can call `ThemeService.SetTheme(themeName)`.
5. `ThemeService` updates resources, saves the theme, and raises `ThemeChanged`.
6. `ThemedPage`, `ThemedWindow`, and `ThemedUserControl` handle the subscription lifecycle.

Concrete presentation components should inherit the themed base class when they need theme lifecycle behavior.

## Resource Dictionaries

Shared resources live under:

```text
Presentation/Resources
```

Important files:

| File | Purpose |
| ---- | ------- |
| `Colors.xaml` | color resources |
| `Brushes.xaml` | brush resources |
| `Themes.xaml` | theme-specific composite resources |
| `Fonts.xaml` | font resources |
| `Controls.xaml` | shared control resources |
| `Styles/*.xaml` | grouped WPF styles |
| `Animations.xaml` | shared animation resources |

`App.xaml` merges these resources for the application.

## Using Theme Resources

XAML should prefer:

```xml
Background="{DynamicResource ThemeSurfaceBrush}"
Foreground="{DynamicResource ThemeTextPrimaryBrush}"
```

Code-created controls should prefer:

```csharp
element.SetResourceReference(Control.BackgroundProperty, "ThemeSurfaceBrush");
```

This keeps controls connected to theme changes.

Common resource groups:

| Resource group | Examples |
| -------------- | -------- |
| core colors | `ThemePrimaryColor`, `ThemeSecondaryColor`, `ThemeBackgroundColor` |
| brushes | `ThemePrimaryBrush`, `ThemeSurfaceBrush`, `ThemeBorderBrush` |
| text | `ThemeTextPrimaryBrush`, `ThemeTextSecondaryBrush`, `ThemeDisabledTextBrush` |
| states | `ThemeDangerBrush`, `ThemeSuccessBrush`, `ThemeWarningBrush`, `ThemeOnlineBrush` |
| page backgrounds | `PageBackgroundBrush`, `AuthPageBackgroundBrush` |

## Adding a Theme

To add a theme:

1. Add a new `ThemePalette` entry in `ThemeService`.
2. Use the same resource keys already produced by `ApplyThemeToApplication`.
3. Verify pages that use `DynamicResource` update without being recreated.
4. Verify code-created controls use `SetResourceReference`.
5. Keep the saved theme name stable because it is persisted in `ConfigService`.

## Current Limitations

* Some UI is still built dynamically in code and needs careful `SetResourceReference` usage.
* Theme resources are global application resources, not scoped per window.
* The available theme list is hard-coded in `ThemeService`.
* Theme tests are not in place yet; regressions are usually caught by visual/manual checks and build verification.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [UI Structure](UI_STRUCTURE.md)
* [Caching](CACHING.md)
* [Testing](TESTING.md)
