# Lokum for Windows

Start **Lokum.exe** to use the application. **Lokum.Verify.exe** is a developer
test runner, is not included in the portable application package, and should not
be used as the app launcher. Failed verification checks print diagnostics to
standard error and exit with code 1. `python scripts/test-verification-runner.py`
checks this behavior, including a real subprocess failure.

Open the repository root in VS Code. The checked-in `.vscode/settings.json`
selects `Lokum.sln`, which contains the Core, Verify and Windows projects.
Generated `bin`, `obj` and WPF temporary project files are excluded from the
Explorer and file watching. If the editor previously loaded temporary projects,
run **Developer: Reload Window** once. Build all three projects with
`dotnet build Lokum.sln -c Debug`.

Mac and Windows 0.7.5 are published with the shared 45-format / 27-tool catalog and aligned Home, Library, paste, preview and window-management workflows. See [the feature parity and verification matrix](FEATURE-PARITY.md) for implementation, evidence and platform differences.

Lokum uses WPF / .NET 10, the same pear artwork, file garden, rounded cards, gold actions and light/dark palette as macOS, with Windows keyboard conventions and native window controls. Build with `dotnet publish windows/Lokum.Windows -c Release -o build/windows/Lokum`. The output is self-contained; users do not install .NET separately. Keep the portable folder together in a writable location so updates can replace it safely.

The shared catalog contains 27 tools and 45 input formats. It includes photo editing, framing/collages, crop/annotation/redaction, QR/PDF tools, document/Pages/EPUB/spreadsheet/slides conversion, archives/subtitles, media tools and public web downloads. Runtime setup is available from Home. Download URLs and SHA-256 hashes are pinned in `Toolchain.cs`, `OfficeRuntime.cs` and `MetadataService.cs`; upstream licenses remain separate from Lokum's MIT license. See [third-party notices](THIRD-PARTY-NOTICES.txt).

Library and preferences live in `%LOCALAPPDATA%/Lokum`, outside the portable application folder. Library keeps the last 50 successful exports and clipboard imports. Text imports use UTF-8 and image imports use lossless PNG. No clipboard monitoring runs in the background. Signed public update staging verifies the manifest, archive hash, version and trusted signature before restart.

While dragging in Explorer or another application, **Ctrl** opens tools, **Shift** conversion and **Alt** file actions. Settings can change the three chords. **Ctrl+Shift+D** opens a copied public media link. The app can remain in the system tray.

**Ctrl+O** chooses files; **Ctrl+V** imports files/text/images/links outside text fields; **Ctrl+1/2** switches Home/Library; **Ctrl+F** searches; **Ctrl+U** checks updates. **Ctrl+Space** opens the selected files' tools wheel. Arrows/Tab choose a tool, Enter opens it and Escape closes it.

Media editors use **Space** for play/pause, **Left/Right** for one-second seeking, **Shift+Left/Right** for frame steps, **I/O** for range marks and **Shift+Space** to play the selected range. Crop supports drag, Shift ratio lock, selection move/resize, aspect presets and Enter save. Annotation supports Select/Move, Delete and Ctrl+Z. Normal text-field editing is preserved.

**Edit Background** is the single wheel entry for the two-step background editor.
Draw freehand around the area to keep or drag a rectangle; everything outside the
selection becomes transparent. Automatic selection is optional and runs locally;
manual regions can restore or subtract pixels afterward. Once the cutout is ready,
choose transparency, a solid color from the hue/saturation/value palette (or enter
`#RRGGBB`), or a replacement image, then save a new
PNG. Replacement images fill the original canvas with a centered cover crop.
For a batch, select each image independently before saving; the background setting
is shared. Originals remain unchanged. **Ctrl+Z**, **Ctrl+A**, **Ctrl+S** and **Esc**
undo, select all, save and cancel respectively, outside text fields.

**Edit Image** includes cropping alongside resize, rotate, flip and photo
adjustments. Choose **Crop image**, drag a selection and choose **Preview crop**
to see the actual cut image before saving. Re-enter cropping to adjust the same
selection; **Remove crop** restores the full canvas and retains other adjustments.
The separate image Crop wheel entry has been removed. Video cropping remains.
Related image tools are named **Redact Image** and **Read QR Code**.

`Lokum.Verify` checks the shared engine and security foundation. `Lokum --smoke-test` runs real Windows keyboard, clipboard, OLE drag, Library, editor and export checks and writes diagnostic JSON/PNG artifacts. See [verification details](FEATURE-PARITY.md#commands-and-evidence) before interpreting a CI artifact as a completed test or official release.
