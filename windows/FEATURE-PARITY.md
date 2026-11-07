# Windows feature parity work

Feature availability depends on the current source, installed tools and platform. The native adaptations and verification boundaries below apply.

`Resources/FeatureCatalog.json` is the shared availability contract: 45 input formats, 28 tools, single/multiple-selection rules and conversion intersections. `scripts/check-feature-parity.py` compares it with the Swift catalog on every source check. The catalog check establishes matching choices; engine and native desktop checks establish behavior.

Windows adds a **Convert** entry to the tools wheel when a common output format is
available, without duplicating it in the Fine-tune menu. Conversion previews generate
the real target file through the same pipeline as Save; all 39 target types have
own-window preview coverage. Office documents/HTML use rendered PDF, archives show
their file list, and images/text/audio/video use their corresponding viewer. Batch
navigation and target changes are covered. Preview files remain temporary and do
not write to Library or the user's output folder. Video compression reports actual
result/original KB after preview, including automatic targets.

Both platforms offer one **Edit Background** entry. Choose foreground areas with
freehand or rectangle selections, add or subtract areas, and optionally use local
automatic selection. Each image in a batch keeps its own selection. Preview a
transparent, palette-color or image background, then save fresh PNG copies.
The same U2NETP weights are bundled on both platforms and run offline with ONNX
Runtime CPU. Existing alpha is preserved; failed/cancelled batches publish nothing.
`dotnet run --project windows/Lokum.Verify -c Release -- --background-removal-only`
exercises a real public-domain NASA photograph, alpha preservation, batch naming
and cancellation. `--ui-audit` includes the new preview in both themes/locales.

macOS integrates image cropping into **Edit Image**, with a draft selection and
explicit crop preview. **Cut Video** accepts independent ranges, including gaps,
overlaps and a different source-time order, with separate or joined exports.
Whole-video and selected-range mute remain available. Video compression, crop,
speed and redaction provide processed previews and actual result/original KB.
Conversion previews own temporary outputs; Save publishes the approved bytes
without another encoding pass. Archive previews list entries without extraction.
File moves stage and publish all copies before removing originals and report any
original that could not be deleted.

| Area | Windows implementation | Verification |
| --- | --- | --- |
| Drag wheels | Ctrl tools, Shift convert, Alt file actions; configurable distinct chords, global copied-link shortcut, size/finish/animation/sound preferences | Real OLE drag with SendInput, file payload and browser URL/HTML fixtures |
| Home / Library / clipboard | Shared pear artwork, colors, cards and actions; System/Light/Dark; file/text/image/link paste; search, reuse, missing files, persistence | Native keyboard/clipboard, restart and rendered light/dark/compact views |
| Images | PNG/JPG/WebP/HEIC/AVIF/TIFF/BMP and SVG import; resize/rotate/flip, light/color/detail effects; original/result preview | Independent codec reads, orientation/color pixels and exported dimensions |
| Crop / annotate / redact | Drag/Shift/Return, move/resize/aspects; independent marker/shapes/text/arrows/images/magnifier and Select/Move; undo/delete; multi-area solid/blur/pixelate, OCR/face assistance | Native drawing/keyboard, saved pixels, independent marks and original hashes |
| Frame / collage / PDF | Solid/gradient/image backgrounds, padding/corners/shadow/position; grid/row/column/featured, fit and order; multi-image PDF | Export canvas sizes, repeated-image ordering, PDF page counts |
| Compression | Image/media/PDF quality, size or reduction targets; image dimension presets; preserves text/complex PDF features when a smaller safe copy is unavailable | Real output sizes, format signatures, unchanged original hashes, unreachable-target failures |
| Media | Conversion; In/Out and range drag, keyboard transport, filmstrip/waveform, codec proxy preview; Cut Video with independent clip ranges (gaps, overlaps, arbitrary source-time order) and joined preview; whole-video or selected-range mute with processed preview; fixed-footer Show Result for video redaction/compression/speed/crop, preserving the actual output pixels and compression size; visualizations, channel/loudness and sample-depth settings. Separate split/snapshot wheel entries are hidden; legacy engine operations remain available internally. | Real FFmpeg/ffprobe exports, audio-level checks outside/inside a mute interval, independent clip durations, fractional source-end regression, MP4/MOV/MKV preview/export pixel equality, speed duration, crop dimensions, compression size and compact-window result controls; native transport and long-range handle drags |
| PDF | Merge order; grouped/ranged split; thumbnail order, duplicate/delete/rotate, independent duplicate rotations, optional uniform widths; page image export and QR | Native PDF organizer and independent PDF inspection/render/QR decode |
| Documents | DOCX/Pages/RTF/RTFD/text clipping/EPUB/text/Markdown, slides and spreadsheets; editable/preserved PDF layout, fixed/reflow EPUB, image DPI | Private Office extraction, Pages read/write, PDF Office/image/EPUB and spreadsheet round trips; typography/link/header/page setup fixtures |
| Archives / subtitles | ZIP/TAR/GZIP/RAR extraction and supported conversions; folder ZIP; SRT/VTT/text | Round trips, Unicode/timing, nested folders, traversal/link/size/cancel protection |
| Metadata | Local ExifTool inspect/edit/remove, editable image/PDF/media fields; image conversion tag preservation without stale orientation/preview/profile | Encoded-pixel equality, tag round trips and removal, preserved conversion tags |
| Web | YouTube video/audio/full-size thumbnail; Instagram photo selection vs Reel video/audio; direct images; quality limits; cancel/progress | Classification, quality/DRM/live fixtures, real public image download with pinned SHA-256; service availability remains subject to the public source site |
| File actions / Float | Copy/move/path/terminal, native Windows Share, reusable Float windows with add/remove, multi-select, drag out, preview, collapse and edge pinning | Native Float input, files retained after removal, transactional move |
| Preferences / tray / update | Login option, background tray, output/download folders, format defaults; existing signed public updater and external Library/preferences storage | Preference persistence/collisions; updater trust and extraction regressions; public 0.7.5 update download, replacement, restart and saved-state verification are tracked in the release verification workflow |

Windows uses Explorer, Windows keyboard conventions and Windows Share. Apple AirDrop has no Windows implementation; Windows Share is the corresponding file action. OCR requires a Windows OCR language. Optional media, Office and metadata runtimes are downloaded from pinned upstream packages into the user's Lokum tools directory without administrator installation. Native playback uses Windows codecs, with an FFmpeg preview proxy fallback.

## Commands and evidence

- `dotnet run --project windows/Lokum.Verify -c Release` runs platform-independent engine/security tests; media tests require the explicit `LOKUM_VERIFY_FFMPEG` / `LOKUM_VERIFY_FFPROBE` paths or CI tool setup.
- `Lokum --smoke-test` exercises a real isolated Windows desktop. Set `LOKUM_SMOKE_DIRECTORY` for JSON, failure diagnostics, export fixtures and PNG render artifacts. Extended parity checks run on GitHub Actions.
- Set `LOKUM_VERIFY_TOOLS_DIRECTORY` to reuse a separate, already verified media-tool installation in local native tests. This does not change the application's tool directory.
- `parity-progress.json` records the last completed assertion; `parity.json` and `smoke.json` are written only after their complete suites pass. Failure diagnostics must not be presented as a successful suite.
- Native tests use Windows SendInput and OLE plus WPF render captures. They are not a physical Windows computer-use session or a claim that every third-party media site is always available.

Run the current Windows workflow and isolated native fixtures for this repository; old release results are not evidence for a current build.

The combined desktop branch also includes the eight-layout window wheel (including Maximize) and the follow-up Home/Library/preview/clipboard/media alignment. See [the desktop comparison and shortcut map](../docs/DESKTOP-PARITY.md) for the current behavior and native platform adaptations.

## Editor reliability regressions

The Windows smoke suite also covers invalid/non-finite/out-of-duration playback ranges, changing a range field while playback is running, invalid time fields with keyboard marking, focused scrubber keyboard seeking, and Reset restoring media options and saved conversion profiles. Invalid preview input remains in the editor for correction; playback timers use the validated range captured when playback starts.

Codec fallback is exercised with an actual FFmpeg proxy, followed by processed playback and a return to Original. Original reuses the working proxy while exports continue to use the source file. Reset restores choices, source order, backgrounds, annotation state, compression presets, zoom and the current format's saved defaults.
