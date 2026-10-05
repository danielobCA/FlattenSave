# Known Bugs

## Paint.NET "Blue" theme switches the panel to dark mode
- **Affects:** 1.0.2
- **Description:** When Paint.NET's theme is set to "Blue", the FlattenSave panel switches to its dark palette instead of matching the Blue theme.
- **Likely cause:** The panel only recognises "Light" and "Dark" in the `UI/AeroColorScheme` registry value (`HKCU\Software\paint.net`). Any other value falls back to a dark/light guess that doesn't match the Blue theme.
- **Status:** Open
