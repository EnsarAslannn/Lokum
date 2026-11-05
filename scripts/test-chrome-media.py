"""Real DOM regression for the Chrome Ctrl-left-click media shortcut.

The native transport is the only boundary replaced in this test; selection,
trusted click events, propagation, shadow DOM and actual image/video tags run in
a real disposable headless Chromium. No personal browser profile is used.
"""
from pathlib import Path
import argparse
import json
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--output', default='build/chrome-media-dom')
parser.add_argument('--browser', help='Optional existing headless Chromium executable')
options = parser.parse_args()
output = Path(options.output).resolve()
output.mkdir(parents=True, exist_ok=True)
script = root / 'browser/chrome/content.js'
checks = []

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(headless=True, **({'executable_path': options.browser} if options.browser else {}))
    try:
        page = browser.new_page(viewport={'width': 1000, 'height': 800})
        page.route('https://fixture.example/**', lambda route: route.fulfill(
            content_type='text/html', body='''<!doctype html><title>Lokum media fixture</title>
            <style>img,video{width:180px;height:120px;background:#d8c8dd}#player{position:relative;width:180px;height:120px}#overlay{position:absolute;inset:0}button{margin:8px}</style>
            <a href="https://www.youtube.com/watch?v=aqz-KE-bpKQ" id="wrapped"><img id="image" alt="Fixture image" src="https://cdn.example/photo.png"></a>
            <video id="video" src="https://cdn.example/movie.mp4"></video>
            <div id="player"><video id="underlay" src="https://cdn.example/stream"></video><div id="overlay">Video overlay</div></div>
            <a id="ordinary" href="https://fixture.example/ordinary">Ordinary link</a><input id="field" value="Text field"><div id="shadow"></div>
            <script>document.querySelector('#shadow').attachShadow({mode:'open'}).innerHTML='<img id="shadow-image" src="https://cdn.example/shadow.webp">';</script>'''))
        page.goto('https://fixture.example/page')
        page.wait_for_load_state('networkidle')
        page.evaluate("""() => {
          window.requests=[];
          window.chrome={runtime:{sendMessage:async request=>{window.requests.push(request);return {ok:true};}}};
        }""")
        page.add_script_tag(content=script.read_text(encoding='utf-8') if script.exists() else '/* No media bridge installed. */')
        page.locator('#image').click(modifiers=['Control'])
        page.wait_for_timeout(100)
        requests = page.evaluate('window.requests')
        assert len(requests) == 1, 'Ctrl-left-click on a web image must request its image tools wheel'
        assert requests[-1]['kind'] == 'image' and requests[-1]['url'] == 'https://cdn.example/photo.png', requests
        assert len(page.context.pages) == 1, 'The media shortcut must not also open the linked video in a new tab'
        checks.append('A video-linked image opens image tools with its actual image URL and prevents the extra tab')
        page.locator('#video').click(modifiers=['Control'])
        page.wait_for_timeout(50)
        request = page.evaluate('window.requests.at(-1)')
        assert request['kind'] == 'video' and request['url'] == 'https://cdn.example/movie.mp4', request
        checks.append('A real video element opens video tools')
        page.locator('#overlay').click(modifiers=['Control'])
        page.wait_for_timeout(50)
        request = page.evaluate('window.requests.at(-1)')
        assert request['kind'] == 'video' and request['url'] == 'https://cdn.example/stream', request
        checks.append('A player overlay resolves the video beneath it')
        page.locator('#shadow-image').click(modifiers=['Control'])
        page.wait_for_timeout(50)
        request = page.evaluate('window.requests.at(-1)')
        assert request['kind'] == 'image' and request['url'] == 'https://cdn.example/shadow.webp', request
        checks.append('An image inside an open shadow root retains its image tools')
        count = page.evaluate('window.requests.length')
        page.locator('#video').click()
        page.locator('#field').click(modifiers=['Control'])
        page.evaluate("document.querySelector('#video').dispatchEvent(new MouseEvent('click',{bubbles:true,ctrlKey:true,button:0}))")
        assert page.evaluate('window.requests.length') == count, 'Normal clicks, text fields and page-generated fake clicks must not trigger Lokum'
        checks.append('Normal clicks, text editing and untrusted script clicks do not launch a tool')
        with page.context.expect_page() as popup:
            page.locator('#ordinary').click(modifiers=['Control'])
        popup.value.close()
        assert page.evaluate('window.requests.length') == count
        checks.append('Ctrl-clicking an ordinary link keeps the normal new-tab behavior')
        page.screenshot(path=str(output / 'fixture.png'), full_page=True)
    finally:
        browser.close()

(output / 'verification.json').write_text(json.dumps({'passed': True, 'checks': checks}, indent=2)+'\n', encoding='utf-8')
print('PASS Chrome DOM media shortcut:', len(checks))
