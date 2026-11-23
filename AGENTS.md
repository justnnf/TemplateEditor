# ArcGIS Pro Add-in Development

## Applicable skills
- Use ui-ux-pro-max for interface design and usability.
- Use desktop-wpf for XAML, styles, bindings, and MVVM.
- Use official Esri ArcGIS Pro SDK documentation
  as the authority on SDK implementation.

## UI requirements
- Target the existing ArcGIS Pro SDK version.
- Use WPF/XAML, not WinUI or MAUI.
- Prefer Esri's native styles and theme resources.
- Support ArcGIS Pro light and dark themes.
- Use DockPane and ProWindow where appropriate.
- Preserve Config.daml registrations.
- Follow existing MVVM patterns.
- Avoid unnecessary external UI dependencies.
- Keep GIS interfaces compact and data-dense.
- Maintain keyboard navigation and accessibility.
- Do not replace existing business logic during UI redesign.
- Follow ArcGIS Pro UI/MCT threading requirements.
- Never assume generic WPF examples are directly
  compatible with the ArcGIS Pro add-in host.

## Visual verification
- Build after XAML modifications.
- Check bindings and runtime resource references.
- Inspect the rendered interface where possible.
- Validate light and dark theme behavior.
- Preserve existing functionality.