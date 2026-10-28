#!/bin/zsh
set -euo pipefail
cd "${0:A:h:h}"
configuration=${1:-release}
source scripts/signing-configuration.sh
lokum_load_signing_configuration
lokum_selected_update_config=${LOKUM_UPDATE_CONFIG:-Configuration/Updates.plist}
# Portable runtime setup is independent of whether a release feed exists.
# Set LOKUM_EXTERNAL_TOOLS=false only for a deliberately bundled-tools build.
lokum_public_build=${LOKUM_EXTERNAL_TOOLS:-true}
if [[ "$lokum_public_build" != true && "$lokum_public_build" != false ]]; then print -u2 "LOKUM_EXTERNAL_TOOLS must be true or false"; exit 1; fi
if [[ "$lokum_signing_mode" != ad-hoc && "$lokum_public_build" == false ]]; then python3 scripts/build-credential-broker.py; fi
# Release binaries do not embed the builder's home-directory/source paths.
swift build -c "$configuration" --product Lokum -Xswiftc -file-prefix-map -Xswiftc "$PWD=/lokum" -Xswiftc -file-compilation-dir -Xswiftc /lokum
binary_dir=$(swift build -c "$configuration" --show-bin-path)
application=${LOKUM_APP_OUTPUT:-"$PWD/build/Lokum.app"}
sparkle_root="$PWD/.build/artifacts/sparkle/Sparkle"
sparkle_framework="$sparkle_root/Sparkle.xcframework/macos-arm64_x86_64/Sparkle.framework"
if [[ ! -d "$sparkle_framework" ]]; then print -u2 "Missing Sparkle framework; run swift package resolve"; exit 1; fi
if [[ -f "$application/Contents/Info.plist" ]]; then
  existing_identifier=$(plutil -extract CFBundleIdentifier raw -o - "$application/Contents/Info.plist")
  expected_identifier=$(plutil -extract CFBundleIdentifier raw -o - Resources/Info.plist)
  if [[ "$existing_identifier" != "$expected_identifier" ]]; then print -u2 "Refusing to overwrite a different application at $application"; exit 1; fi
fi
mkdir -p "$application/Contents/MacOS" "$application/Contents/Resources/Tools"
cp "$binary_dir/Lokum" "$application/Contents/MacOS/Lokum"
if [[ "$configuration" == release ]]; then /usr/bin/strip -S "$application/Contents/MacOS/Lokum"; fi
cp Resources/Info.plist "$application/Contents/Info.plist"
python3 scripts/generate-localizations.py
ditto Resources/Localizations "$application/Contents/Resources"
if [[ -d "$binary_dir/Lokum_LokumCore.bundle" ]]; then ditto "$binary_dir/Lokum_LokumCore.bundle" "$application/Contents/Resources/Lokum_LokumCore.bundle"; fi
python3 scripts/configure-updates.py "$application/Contents/Info.plist" "${LOKUM_UPDATE_CONFIG:-Configuration/Updates.plist}"
if [[ "$lokum_signing_mode" != ad-hoc && "$lokum_public_build" == false ]]; then
  python3 scripts/build-credential-broker.py --app "$application"
elif [[ -e "$application/Contents/XPCServices/local.lokum.app.credentials.v1.xpc" || -e "$application/Contents/XPCServices/local.tangerine.replica.credentials.v1.xpc" ]]; then
  print -u2 "Use a separate output directory for an ad-hoc build; this output contains a frozen signed credential broker."
  exit 1
fi
mkdir -p "$application/Contents/Frameworks"
if [[ -d "$application/Contents/Frameworks/Sparkle.framework" ]]; then rm -rf "$application/Contents/Frameworks/Sparkle.framework"; fi
ditto "$sparkle_framework" "$application/Contents/Frameworks/Sparkle.framework"
if [[ "$lokum_public_build" == true ]]; then
  if [[ -e "$application/Contents/Resources/Tools/ffmpeg" || -d "$application/Contents/Resources/Tools/LibreOffice.app" ]]; then
    print -u2 'Public packages require a fresh output directory without bundled third-party runtimes.'; exit 1
  fi
  plutil -replace LokumExternalTools -bool true "$application/Contents/Info.plist"
else
for component in ffmpeg ffprobe yt-dlp deno; do
  if [[ ! -x "Tools/$component" ]]; then print -u2 "Missing Tools/$component; see scripts/setup-tools.sh and scripts/setup-web-download-tools.py"; exit 1; fi
  cp "Tools/$component" "$application/Contents/Resources/Tools/$component"
done
fi
if [[ -f Tools/ExifTool/exiftool ]]; then
  ditto Tools/ExifTool "$application/Contents/Resources/Tools/ExifTool"
else
  print -u2 "Missing Tools/ExifTool; run scripts/setup-exiftool.sh"
  exit 1
fi
if [[ "$lokum_public_build" == false ]]; then
if [[ -d Tools/LibreOffice.app ]]; then
  ditto Tools/LibreOffice.app "$application/Contents/Resources/Tools/LibreOffice.app"
else
  print -u2 "Missing Tools/LibreOffice.app; see scripts/setup-tools.sh"
  exit 1
fi
fi
ditto Resources/Sounds "$application/Contents/Resources/Sounds"
cp Resources/PagesInterop.js "$application/Contents/Resources/PagesInterop.js"
cp Resources/PDFLib.js Resources/PDFInterop.js "$application/Contents/Resources/"
if [[ -f Resources/LokumIcon.icns ]]; then cp Resources/LokumIcon.icns "$application/Contents/Resources/LokumIcon.icns"; fi
cp Resources/THIRD-PARTY-NOTICES.txt "$application/Contents/Resources/THIRD-PARTY-NOTICES.txt"
ditto Resources/licenses "$application/Contents/Resources/licenses"
cp "$sparkle_root/LICENSE" "$application/Contents/Resources/licenses/Sparkle-LICENSE.txt"
sparkle_embedded="$application/Contents/Frameworks/Sparkle.framework/Versions/B"
# Sign inside out. The downloader's sandbox entitlements must be retained.
codesign "${lokum_signing_flags[@]}" "$sparkle_embedded/XPCServices/Installer.xpc"
codesign "${lokum_signing_flags[@]}" --preserve-metadata=entitlements "$sparkle_embedded/XPCServices/Downloader.xpc"
codesign "${lokum_signing_flags[@]}" "$sparkle_embedded/Autoupdate"
codesign "${lokum_signing_flags[@]}" "$sparkle_embedded/Updater.app"
codesign "${lokum_signing_flags[@]}" "$application/Contents/Frameworks/Sparkle.framework"
if [[ "$lokum_public_build" == false ]]; then
codesign "${lokum_signing_flags[@]}" "$application/Contents/Resources/Tools/ffmpeg"
codesign "${lokum_signing_flags[@]}" "$application/Contents/Resources/Tools/ffprobe"
codesign "${lokum_signing_flags[@]}" "$application/Contents/Resources/Tools/yt-dlp"
codesign "${lokum_signing_flags[@]}" --entitlements Configuration/Deno.entitlements "$application/Contents/Resources/Tools/deno"
fi
codesign "${lokum_signing_flags[@]}" "$application"
codesign --verify --deep --strict "$application"
lokum_verify_signing_identity "$application"
if [[ "$lokum_signing_mode" != ad-hoc && "$lokum_public_build" == false ]]; then python3 scripts/build-credential-broker.py --verify-app "$application"; fi
print "$application"
