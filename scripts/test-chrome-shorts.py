"""Exercise an actual extension across a same-document YouTube Shorts navigation."""
import argparse,json,tempfile
from pathlib import Path
from playwright.sync_api import sync_playwright
parser=argparse.ArgumentParser();parser.add_argument('--browser');parser.add_argument('--output',type=Path,default=Path('build/chrome-shorts'))
args=parser.parse_args();root=Path(__file__).resolve().parents[1];extension=root/'browser/chrome';args.output.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='lokum-shorts-') as profile,sync_playwright() as playwright:
    context=playwright.chromium.launch_persistent_context(profile,headless=True,executable_path=args.browser or playwright.chromium.executable_path,args=[f'--disable-extensions-except={extension}',f'--load-extension={extension}'])
    try:
        worker=context.service_workers[0] if context.service_workers else context.wait_for_event('serviceworker')
        worker.evaluate("""() => {globalThis.nativeRequests=[];chrome.runtime.sendNativeMessage=async(host,request)=>{nativeRequests.push(request);return {ok:true};};}""")
        page=context.new_page();page.route('https://www.youtube.com/**',lambda route:route.fulfill(content_type='text/html',body='<video id="video" style="width:200px;height:150px;background:#333"></video>'))
        page.goto('https://www.youtube.com/');page.wait_for_load_state('networkidle')
        page.evaluate("history.pushState({},'', '/shorts/X31goIHSY3c');document.querySelector('video').src='blob:https://www.youtube.com/player-session'")
        page.locator('#video').click(modifiers=['Control']);page.wait_for_timeout(100)
        request=worker.evaluate('nativeRequests.at(-1)')
        print(json.dumps(request,indent=2))
        assert request and 'X31goIHSY3c' in request['url'],'A Shorts SPA click sent the original YouTube homepage instead of the current video'
        (args.output/'verification.json').write_text(json.dumps({'passed':True,'request':request},indent=2)+'\n',encoding='utf-8')
    finally:context.close()
