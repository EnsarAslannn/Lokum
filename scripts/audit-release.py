#!/usr/bin/env python3
"""Fail a public macOS bundle on accidental credentials or builder identity."""
import os, re, sys, plistlib, base64
from pathlib import Path
root=Path(sys.argv[1]).resolve()
info=plistlib.loads((root/'Contents/Info.plist').read_bytes())
assert info.get('CFBundleIdentifier')=='local.lokum.app', 'The bundle must have an independent Lokum identity'
repository=info.get('LokumPublicUpdateRepository')
if repository:
    assert re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}',repository), 'Invalid release repository'
    assert info.get('SUFeedURL')=='https://github.com/'+repository+'/releases/latest/download/appcast.xml', 'The release channel and feed must match'
    assert len(base64.b64decode(info.get('SUPublicEDKey',''),validate=True))==32, 'Connect the publisher signing key'
    assert info.get('SURequireSignedFeed') is True and info.get('SUVerifyUpdateBeforeExtraction') is True, 'Updates must verify signed feeds and archives'
else:
    assert not info.get('SUFeedURL') and not info.get('SUPublicEDKey'), 'An unconfigured build must not inherit another publisher\'s update trust'
assert info.get('LokumExternalTools') is True
assert not (root/'Contents/XPCServices').exists(), 'Public archive must not include a private credential broker'
assert not (root/'Contents/Resources/Tools/ffmpeg').exists(), 'Upstream media tools use direct first-use acquisition'
assert not (root/'Contents/Resources/Tools/LibreOffice.app').exists()
username=os.environ.get('USER','').encode()
issues=[];count=0
for path in root.rglob('*'):
    if not path.is_file() or path.is_symlink(): continue
    count+=1;data=path.read_bytes()
    if path.name.startswith('.env') or path.suffix.lower() in {'.p12','.pfx','.key','.mobileprovision'}:issues.append(str(path.relative_to(root)))
    patterns=[rb'-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----',rb'github_pat_[A-Za-z0-9_]{20,}',rb'gh[pousr]_[A-Za-z0-9]{20,}']
    if any(re.search(pattern,data) for pattern in patterns) or username and len(username)>5 and username in data:issues.append(str(path.relative_to(root)))
if issues:sys.exit('Release audit failed: '+', '.join(sorted(set(issues))))
print(f'PASS public app boundary: {count} files; no local account, signing artifacts or token patterns')
