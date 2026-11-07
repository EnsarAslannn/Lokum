# Mac and Windows desktop parity

Both implementations use the same 45 input-format / 28-tool catalog. `scripts/check-feature-parity.py` checks conversion destinations, tool names and single/multi-file eligibility against the Swift source; it is not a substitute for running the tools.

| Workflow | Shared behavior |
| --- | --- |
| Home | Pear artwork, local-file drop card, selected-file chips with individual removal, three action tiles, quick conversion picks and recent history |
| Library | 50 persistent jobs/imports, search by file or action, missing-file status, preview, reuse and reveal; searching/previewing never deletes originals |
| Clipboard | Files **and folders**, Unicode text with line breaks, full-size PNG images, HTTP(S) links into the download flow; editable fields retain ordinary paste |
| Preview | Library and Float batch navigation, images, paged PDFs/documents, selectable text, audio/video; media Space play/pause and one-second seeking; Escape closes |
| File wheels | Convert/tools/actions while dragging, centered complete captions, 320-point/DIP default diameter, adjustable size, sound feedback, subtle animation and keyboard selection |
| Images | Format conversion, resize/rotate, crop, adjustments/effects, annotation, backgrounds, collages, redaction, image PDFs and metadata |
| Crop | Integrated into Edit Image with explicit crop preview before save; drag/move/resize, aspect presets and proportional batch selection |
| Media | Conversion, compression, crop, speed, whole-video or selected-range mute; independent clip ranges with separate/joined export, normalization, channels, visualization and timed redaction; processed video previews and actual result KB. Legacy split/snapshot operations remain internal, with no duplicate wheel entries. |
| Precise media keys | Arrows seek 1 second; Shift+arrows use the source video FPS or 0.1 seconds for audio; I/O set trim range; Shift+Space plays the selected range |
| PDF/documents | Merge, split, reorder/duplicate/rotate pages, uniform widths, compression, QR reading, editable/preserved document layouts, spreadsheets, EPUB and Pages interoperability |
| Archives/subtitles | ZIP/TAR/GZIP/RAR workflows and SRT/VTT conversion; original files retained and output collisions handled safely |
| Float/actions | Compact paperclip stack; hover/click opens a slim thumbnail shelf; stack carries all files, clip moves the shelf; multi-selection, preview, copy/path, reveal/share, icon/list modes and edge attachment; removal never deletes originals |
| Window management | Eight layouts: thirds, halves, two-thirds and Maximize; actual title-bar/resize drag, target preview, release placement and Escape cancellation; usable display work area; app minimum widths keep the selected left/center/right anchor |
| Progress/results | Borderless progress/success notices, cancellable work, real progress where measurable and indeterminate state otherwise; success fades after three seconds |
| Settings/updates | Appearance, feedback, motion, drag chords, format profiles, output/download locations, login launch, automatic-check preference, visible update banner and verified public GitHub updates; Library/imports live outside the app bundle |

## Shortcut mapping

| Action | macOS | Windows |
| --- | --- | --- |
| Home / Library | Command+1 / Command+2 | Ctrl+1 / Ctrl+2 |
| Find in Library | Command+F | Ctrl+F |
| Choose files | Command+O | Ctrl+O |
| Paste content | Command+V | Ctrl+V |
| Open selected-file tools | Return / Command+Return | Enter / Ctrl+Enter; Ctrl+Space remains supported |
| Library result | Up/Down, Space preview, Return reuse | Up/Down, Space preview, Enter reuse |
| Download copied link | Control+Shift+D | Ctrl+Shift+D |
| File / web context | Control-click or Control+right-click: Finder tools / browser Download Media | Ctrl-click local files / Ctrl+right-click browser Download Media |
| Drag: convert / tools / actions | Shift / Command / Control | Shift / Ctrl / Alt |
| Arrange a dragged window | Shift or Command | Shift or Alt |
| Preview batch / media | Arrows navigate files; for media arrows seek, Option+arrows change files | Arrows navigate files; for media arrows seek, Alt+arrows change files |
| Settings / updates | Command+, / Command+U | Ctrl+, / Ctrl+U |

Drag chords are configurable. Text entry keeps normal editing shortcuts; tool shortcuts are scoped to their own active editor. Library search Down moves focus into its matching results.

## Native platform differences

- AirDrop is an Apple service. Windows uses the native Windows Share dialog; available destinations depend on the computer's installed share providers. File sharing is available on both platforms.
- Mac uses Quick Look/AVFoundation and Sparkle; Windows uses WPF, a pinned CPU PDFium renderer, Windows media APIs, local conversion previews and an Ed25519-verified update helper. PDF form appearances remain visible in read-only previews; previews do not execute document scripts or open link actions. Unsupported Windows media codecs use a local FFmpeg preview proxy. Office previews require the same optional private office runtime as Windows document conversion; originals stay unchanged.
- macOS window control needs the user's system permission. Windows normally uses WinEvent/window APIs, but elevated/protected applications can reject resizing. Both platforms report refused placements.
- macOS context integration uses Accessibility only after an explicit Control-click in Finder or a supported browser. It reads media-copy menu actions and preserves clipboard items/formats when the clipboard is unchanged. Switching apps or interacting again cancels resolution. No extension is required for Download Media; unavailable media addresses fall back to the paste-link screen. External browser/Finder permission behavior needs verification in the user's signed app; own-window CI checks cover real native menu copy and private clipboard restoration without granting desktop permissions.
- Native fonts, title-bar controls, file pickers and system sharing dialogs follow the operating system. App page structure, palette, actions and keyboard behavior are shared.
- Authenticated/protected web media and upstream service availability are not guaranteed by either version.

## Reproducible verification

- `python3 scripts/check-feature-parity.py`
- `swift test` (includes real image/document/media exports; live web-service checks are opt-in)
- Source checks run the full macOS engine suite with FFmpeg, LibreOffice and ExifTool installed, plus isolated native editor checks using real generated video and image fixtures.
- `Lokum --desktop-feature-smoke <output-directory>` opens actual conversion/media workspaces with isolated defaults, verifies processed files, publication and temporary-file cleanup. It uses only its own windows and private clipboard fixtures; external input permissions are not needed.
- Installed signed Mac app: `--desktop-parity-smoke <output-directory>` runs real keyboard input against a disposable Home/Library fixture, a private pasteboard and isolated defaults. It does not replace the user's Library or clipboard. It records screenshots and a JSON report.
- Installed Mac app: `--window-management-smoke <manager-pid> <output-directory>` tests real window drags against the bounded cross-process fixture described in [WINDOW-MANAGEMENT.md](WINDOW-MANAGEMENT.md).
- `dotnet run --project windows/Lokum.Verify -c Release` checks engine safety, file transactions, formats, source FPS and real FFmpeg operations when tools are configured.
- Windows executable: `--smoke-test` runs native SendInput/OLE/WPF checks, real OS clipboard imports, Library restart/search/reuse, preview navigation, crop/annotation/export, media controls and processed playback, original-file integrity, Explorer gestures and all eight window layouts. GitHub's Windows runner uploads screenshots, errors and JSON results.

Windows native verification runs on an isolated Windows runner. This is not a claim of a physical Windows Computer Use session or listening to audio on every hardware configuration. Regression tests cover additional display geometries; not every display/codec/share-provider configuration has been exercised on physical hardware.

## Verification scope

Run the current workflows and native fixtures for this repository. macOS runtime checks require a Mac; Windows own-window renders do not demonstrate physical pointer input or hardware behavior.
