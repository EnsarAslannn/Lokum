#!/usr/bin/env python3
"""A failed verification must exit 1, rather than become a CLR/Windows crash."""
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
subprocess.run([
    'dotnet', 'run', '--project', 'windows/Lokum.Verify', '-c', 'Release',
    '--no-restore', '--', '--runner-only',
], cwd=root, check=True, timeout=120)
environment = dict(os.environ)
environment.pop('LOKUM_VERIFY_METADATA_TOOLS', None)
environment.pop('LOKUM_VERIFY_DOWNLOAD_METADATA', None)
runner = root / 'windows/Lokum.Verify/bin/Release/net10.0/Lokum.Verify.dll'
result = subprocess.run([
    'dotnet', str(runner), '--metadata-unicode-only',
], cwd=root, env=environment, capture_output=True, text=True, encoding='utf-8', timeout=30)
assert result.returncode == 1, f'Expected handled exit 1, got {result.returncode}'
assert result.stderr.startswith('FAIL verification: '), 'Missing handled failure diagnostic'
assert 'Set LOKUM_VERIFY_METADATA_TOOLS' in result.stderr, 'Unexpected failure reason'
print('PASS verification entry point: missing configuration produces a handled diagnostic and exit 1')
