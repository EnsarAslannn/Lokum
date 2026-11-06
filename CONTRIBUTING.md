# Contributing

Please open an issue describing the problem, platform, app version and expected
behavior before proposing a large change. Small fixes can go directly to a PR.
Use synthetic, redistributable fixtures; never attach private documents or keys.

macOS: Xcode with Swift 6, macOS 13+, Apple Silicon. Run `swift test`.
Windows source and build instructions live under `windows/`.

For native UI changes, use the [isolated UI audit](docs/testing/UI-AUDIT.md) to
check light/dark and English/Turkish scenes without dragging personal files.
It complements the native gesture, keyboard and export tests; it does not replace them.

New output formats and tools must preserve originals, choose unique output names,
clean partial exports on cancellation, and avoid shell interpolation. Test these
boundaries and actual output metadata rather than only checking file existence.

Only contribute code and assets that can be legally distributed with the project.
Third-party licenses and required copyright notices must be preserved.

## Repository and pull requests

Open issues and pull requests at [EnsarAslannn/Lokum](https://github.com/EnsarAslannn/Lokum).
All pull requests are reviewed and merged manually. Automatic merging is disabled.
Keep unfinished work in draft and run the
checks relevant to your change before requesting review.

Keep required copyright notices, component attribution and the MIT license. Do not commit build outputs,
downloaded tools, local settings, personal test files or signing credentials.
