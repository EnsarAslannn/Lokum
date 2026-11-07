"""Use the real scanner to verify history coverage and the narrow false-positive exception."""
from pathlib import Path
from tempfile import TemporaryDirectory
import hashlib
import json
import subprocess
import sys

root=Path(__file__).resolve().parents[1]
scanner=Path(sys.argv[1]).resolve(strict=True)
with TemporaryDirectory(prefix='lokum-secret-scan-') as directory:
    fixture=Path(directory)/'repository';fixture.mkdir()
    def git(*args):
        return subprocess.check_output(['git','-C',str(fixture),*args],stderr=subprocess.PIPE,text=True).strip()
    git('init','-q','-b','main');git('config','user.name','Lokum scanner fixture');git('config','user.email','fixture@example.invalid')
    path=fixture/'browser/chrome/manifest.json';path.parent.mkdir(parents=True)
    value=hashlib.sha256(b'Lokum synthetic credential regression fixture').hexdigest()
    path.write_text(json.dumps({'key':value})+'\n',encoding='utf-8')
    git('add','.');git('commit','-qm','Synthetic finding');introduced=git('rev-parse','HEAD')
    path.write_text('{}\n',encoding='utf-8');git('add','.');git('commit','-qm','Remove fixture from worktree')
    report=Path(directory)/'redacted.json'
    result=subprocess.run([str(scanner),'git','--redact=100','--no-banner','--log-opts=--all',
        '--gitleaks-ignore-path',str(root/'.gitleaksignore'),'--report-format=json','--report-path',str(report),str(fixture)],capture_output=True,text=True)
    assert result.returncode==1,'The scanner must reject a credential fixture removed from the worktree'
    findings=json.loads(report.read_text(encoding='utf-8'))
    assert any(row['Commit']==introduced and row['File']=='browser/chrome/manifest.json' for row in findings),'The reviewed public key must not exempt a different finding in the same path'
    assert all(value not in json.dumps(row) for row in findings),'All finding values must remain redacted'
print('PASS real scanner: full-history detection, narrow fingerprint exception and redaction')
