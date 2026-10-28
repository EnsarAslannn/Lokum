#!/bin/zsh
set -euo pipefail
cd "${0:A:h:h}"
if (( $# != 2 )); then print -u2 'Usage: scripts/prepare-public-release.sh VERSION BUILD'; exit 1; fi
if [[ ! "$1" =~ '^[0-9]+\.[0-9]+\.[0-9]+$' || ! "$2" =~ '^[0-9]+$' ]]; then print -u2 'Use numeric release versions.'; exit 1; fi
export LOKUM_APP_VERSION=$1 LOKUM_BUILD_NUMBER=$2
export LOKUM_UPDATE_CONFIG="$PWD/Configuration/Updates.public.plist"
if ! repository=$(plutil -extract PublicRepository raw -o - "$LOKUM_UPDATE_CONFIG"); then
  print -u2 'Connect the Lokum release repository and public signing key in Configuration/Updates.public.plist before preparing an update.'; exit 1
fi
export LOKUM_APP_OUTPUT=${LOKUM_APP_OUTPUT:-"$PWD/build/public-$1-$2/Lokum.app"}
feed_directory="$PWD/build/public-feed-$1-$2"
mkdir -p "$feed_directory"
archive="$feed_directory/Lokum-macOS-arm64-$1-$2.zip"
if [[ -e "$archive" ]]; then print -u2 'This release archive already exists. Keep it immutable and increment the version.'; exit 1; fi
scripts/build-app.sh release
python3 scripts/audit-release.py "$LOKUM_APP_OUTPUT"
python3 scripts/archive-app.py "$LOKUM_APP_OUTPUT" "$archive"
key_options=(--account "${LOKUM_UPDATE_KEY_ACCOUNT:-local.lokum.app}")
if [[ -n "${LOKUM_UPDATE_KEY_FILE:-}" ]]; then key_options=(--ed-key-file "$LOKUM_UPDATE_KEY_FILE"); fi
sparkle="$PWD/.build/artifacts/sparkle/Sparkle/bin"
prefix="https://github.com/$repository/releases/download/v$1-$2/"
"$sparkle/generate_appcast" "${key_options[@]}" --download-url-prefix "$prefix" "$feed_directory"
"$sparkle/sign_update" "${key_options[@]}" --verify "$feed_directory/appcast.xml"
python3 scripts/validate-update-feed.py "$feed_directory/appcast.xml" "$2"
upload_directory="$PWD/build/public-upload-$1-$2"
mkdir -p "$upload_directory"
cp "$archive" "$upload_directory/${archive:t}"
cp "$archive" "$upload_directory/Lokum-macOS-arm64.zip"
cp "$feed_directory/appcast.xml" "$upload_directory/appcast.xml"
print "Signed public archive and feed: $feed_directory"
