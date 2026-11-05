"""Load the actual extension in disposable headless Chromium, not a personal profile.

Only the native OS transport is replaced. The manifest, isolated content script,
Chrome message sender and service-worker validation execute normally.
"""
import argparse
import json
from pathlib import Path
import tempfile
from playwright.sync_api import sync_playwright

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=Path,default=Path('build/chrome-extension'))
parser.add_argument('--browser',required=True,help='Full Chromium/Chrome for Testing executable, not headless-shell')
args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
extension=root/'browser/chrome';checks=[]
with tempfile.TemporaryDirectory(prefix='lokum-chrome-profile-') as profile, sync_playwright() as playwright:
    context=playwright.chromium.launch_persistent_context(profile,headless=True,executable_path=args.browser,
        args=[f'--disable-extensions-except={extension}',f'--load-extension={extension}'])
    try:
        worker=context.service_workers[0] if context.service_workers else context.wait_for_event('serviceworker')
        extension_id=worker.url.split('/')[2]
        assert extension_id=='djllfhjpfpmpcpfhnmnciacikoklceln',extension_id
        worker.evaluate("""() => { globalThis.nativeRequests=[]; chrome.runtime.sendNativeMessage=async(host,request)=>{ nativeRequests.push({host,...request});return {ok:true};}; }""")
        page=context.new_page()
        page.route('https://fixture.example/**',lambda route:route.fulfill(content_type='text/html',body='''<!doctype html><title>Lokum extension fixture</title>
            <style>img,video{width:200px;height:140px;background:#ead3df}</style>
            <a href="https://www.youtube.com/watch?v=aqz-KE-bpKQ"><img id="image" src="https://cdn.example/photo.png"></a>
            <video id="video" src="https://cdn.example/stream"></video>'''))
        page.goto('https://fixture.example/page');page.wait_for_load_state('networkidle')
        page.locator('#image').click(modifiers=['Control'])
        page.wait_for_timeout(100)
        first=worker.evaluate('nativeRequests.at(-1)')
        assert first['kind']=='image' and first['url']=='https://cdn.example/photo.png' and first['host']=='com.lokum.media',first
        assert len(context.pages)==2,'The image shortcut also opened a linked video tab'
        checks.append('The actual installed content script sends the clicked image through the real extension worker')
        page.locator('#video').click(modifiers=['Control']);page.wait_for_timeout(100)
        second=worker.evaluate('nativeRequests.at(-1)')
        assert second['kind']=='video' and second['url']=='https://cdn.example/stream' and second['pageUrl']=='https://fixture.example/page',second
        checks.append('A video without an extension reaches native messaging as a video with a Chrome-authenticated page URL')
        popup=context.new_page();popup.goto(f'chrome-extension://{extension_id}/popup.html');popup.locator('#check').click()
        popup.wait_for_function("document.querySelector('#status').textContent.includes('hazır')")
        assert worker.evaluate('nativeRequests.at(-1).operation')=='ping'
        checks.append('The shipped popup can validate the native connection through the real worker sender boundary')
        page.screenshot(path=str(args.output/'extension-fixture.png'))
    finally:
        context.close()
(args.output/'verification.json').write_text(json.dumps({'passed':True,'checks':checks},indent=2)+'\n',encoding='utf-8')
print('PASS actual Chrome extension:',len(checks))
