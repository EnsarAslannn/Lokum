# Chrome media shortcut

The default extension-free web flow is **Ctrl + right-click → Download Media** in
Lokum's small native popup. It saves the image/video to the configured Downloads
folder, records it in Library and selects the local file for normal tools. No
editor opens automatically. If the browser hides the media address, the download
dialog asks for a link instead of silently reusing another source. Current public
YouTube/Shorts pages can resolve from the native address bar. DRM/private media can
still prevent downloading.

The extension below is optional, for users who additionally want a direct web
editing wheel on Ctrl-left-click.

The Chrome extension identifies the actual clicked `img` or `video` in the page.
**Ctrl + left-click** opens the matching native Lokum wheel. A linked thumbnail is
an image; a video behind a player overlay remains a video. Native browser context
menus, clipboard contents and filename extensions are not used for this decision.
Normal clicks, ordinary Ctrl-clicked links and local-file shortcuts keep their usual
behavior. Pages cannot trigger the shortcut by dispatching synthetic click events.

## One-time setup

1. Open the current portable Lokum, then **Settings → Chrome extension (optional)**.
2. Choose **Prepare Chrome connection**. It registers `com.lokum.media` for this
   Windows user only, pointing to this `Lokum.exe`. No administrator is required.
3. Open `chrome://extensions`, enable Developer mode, choose **Load unpacked**, and
   select the portable application's **ChromeExtension** folder.
4. Refresh the web page. Ctrl-left-click a picture/video and choose a Lokum tool.
   The extension popup's **Bağlantıyı dene** button checks the native connection.

The public manifest key keeps the unpacked extension ID stable:
`djllfhjpfpmpcpfhnmnciacikoklceln`. The native manifest allows only that origin.
The connection can also be prepared with `Lokum.exe --install-chrome-bridge`.
If the portable directory moves, prepare the connection again from the new copy.

## Behavior and limits

The extension has website content-script and native-messaging access. It sends only
the Ctrl-clicked media URL/type and the Chrome-authenticated source page to the local
application. It has no analytics, external backend, clipboard access or continuous
media collection. Standard HTTP(S) web pages, open shadow roots and page frames are
supported. Chrome's own pages, the Web Store and pages denied extension site access
are restricted by Chrome.

Browser-internal video/blob addresses use the actual source page for downloading;
the wheel still offers video tools. Downloading depends on the site's public media
support. DRM, sign-in requirements and non-downloadable streams may prevent exporting.
YouTube videos use their canonical video page, including Shorts and temporary
player streams. Same-document Shorts navigation uses the current location, checked
against the authenticated Chrome frame origin, rather than a stale initial tab URL.
Version 1.0.1 fixes this routing. When updating an unpacked extension, reload its
card in `chrome://extensions` and refresh any already-open YouTube tabs.
Inline/blob image addresses are reported as unsupported instead of being mistaken
for videos. Saved web outputs use the configured output preference; the default is
the configured Downloads folder, while imported originals remain in Lokum's data.

## Development checks

Install `scripts/requirements-browser-tests.txt` and Playwright Chromium, then run:

```powershell
python scripts/test-chrome-media.py
node scripts/test-chrome-protocol.cjs
python scripts/test-chrome-native-host.py build/windows/Lokum/Lokum.exe
build/windows/Lokum/Lokum.exe --chrome-bridge-checks build/chrome-bridge-checks
```

`scripts/test-chrome-extension.py --browser PATH_TO_FULL_CHROMIUM` additionally
loads the actual extension in a disposable headless profile and tests real isolated
content-script/service-worker communication. The native transport is replaced only
at that test boundary; the actual portable executable's binary protocol and real
current-user named-pipe server are checked independently.

The Windows preview workflow packages the extension and checks media clicks,
message validation, native stdio, pipe routing and selectable native wheels.
The bridge follows Chrome's [native messaging protocol](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging).
