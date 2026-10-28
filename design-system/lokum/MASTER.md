# Lokum design system

Preserve the inherited desktop layout, native controls, file selection, wheel geometry,
keyboard commands and editor behavior. The visual identity is Turkish delight:
rose, ivory, plum and a small pistachio accent.

| Token | Light | Dark |
| --- | --- | --- |
| Canvas | #FFF7F4 | #201A25 |
| Surface | #FFFCFA | #2B2332 |
| Inset | #F7E9E7 | #251E2B |
| Text | #3D2736 | #F9EDF3 |
| Secondary text | #745C6D | #C5ADBD |
| Border | #E8D3DA | #4B3C50 |
| Pistachio selection | #E1EBDD | #344735 |
| Rose wash | #F7DEE5 | #49303F |
| Focus / accent text | #A52F5A | #F0A2BF |

Primary controls use #B63F68 → #BE4A72 with ivory/white text, providing at least
4.5:1 contrast. #CB567B is a decorative mascot color, not a small-text background.
Use native Segoe UI Variable/Segoe UI on Windows and the rounded system face on macOS.
Keep visible keyboard focus, system/light/dark appearance, native localization and
the inherited reduced-motion preference.

The canonical editable mark is Resources/Brand/LokumMark.svg. It is a rounded rose
delight cube with a pale sugar top, darker right facet, small face and pistachio sliver.
The native macOS renderer lives in Sources/LokumApp/LokumArtwork.swift.
