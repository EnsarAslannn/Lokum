"""Check the actual portable WPF executable's Chrome binary stdio entrypoint."""
import argparse
import json
from pathlib import Path
import struct
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('executable', type=Path)
parser.add_argument('--output', type=Path, default=Path('build/chrome-native-host'))
args = parser.parse_args()
origin = 'chrome-extension://djllfhjpfpmpcpfhnmnciacikoklceln/'
checks = []

def run(message, ok, caller=origin, raw=None):
    body = json.dumps(message, ensure_ascii=False).encode('utf-8')
    framed = raw if raw is not None else struct.pack('<I', len(body))+body
    result = subprocess.run([str(args.executable.resolve()), caller], input=framed,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    assert len(result.stdout) >= 4, ('Native host did not return a binary reply', result.stderr)
    length, = struct.unpack('<I', result.stdout[:4])
    assert length == len(result.stdout)-4, 'stdout must contain exactly one Chrome frame and no unrelated text'
    reply = json.loads(result.stdout[4:])
    assert reply['ok'] is ok and result.returncode == (0 if ok else 1), reply
    return reply

assert run({'version':1, 'operation':'ping', 'diagnostic':'Türkçe 🍐'}, True)['version']
checks.append('Chrome can ping the actual portable WinExe over redirected UTF-8 binary stdio without opening the main app')
run({'version':1, 'operation':'ping'}, False, caller='chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/')
checks.append('An unapproved extension origin is rejected')
for message in [
    {'version':2, 'operation':'ping'},
    {'version':1, 'operation':'execute'},
    {'version':1, 'operation':'open', 'kind':'file', 'url':'https://example.com/file', 'pageUrl':'https://example.com/'},
    {'version':1, 'operation':'open', 'kind':'image', 'url':'javascript:alert(1)', 'pageUrl':'https://example.com/'},
    {'version':1, 'operation':'open', 'kind':'video', 'url':'https://example.com/video', 'pageUrl':'file:///private/file'},
]:
    run(message, False)
checks.append('Unknown versions, operations, media kinds and non-web addresses never launch a tool')
run({}, False, raw=struct.pack('<I', 131073))
run({}, False, raw=struct.pack('<I', 40)+b'{}')
checks.append('Oversized and truncated messages return a failure frame instead of hanging or starting Lokum')
args.output.mkdir(parents=True, exist_ok=True)
(args.output/'verification.json').write_text(json.dumps({'passed':True,'checks':checks},indent=2)+'\n',encoding='utf-8')
print('PASS Chrome native host:',len(checks))
