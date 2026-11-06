#!/usr/bin/env python3
"""Verify the real CI observer's export and Lokum output path before running the app."""
import ctypes
import os
from pathlib import Path
import sys
import tempfile

if sys.platform != 'win32':
    raise SystemExit('This check requires the Windows CI observer DLL.')
if len(sys.argv) != 2:
    raise SystemExit('Usage: test-native-trace.py PATH_TO_DLL')

library = ctypes.WinDLL(str(Path(sys.argv[1]).resolve(strict=True)))
install = library.InstallTrace  # Fails if the build accidentally compiled the EXE branch.
install.argtypes = []
install.restype = None
with tempfile.TemporaryDirectory(prefix='Lokum-Trace-') as directory:
    os.environ['LOKUM_SMOKE_DIRECTORY'] = directory
    install()
    trace = Path(directory) / 'native-exception-stack.txt'
    assert trace.is_file(), 'Observer did not use LOKUM_SMOKE_DIRECTORY.'
    assert 'Native exception observer armed' in trace.read_text(encoding='utf-8'), 'Observer did not initialize.'
print('PASS native observer: InstallTrace export and Lokum output directory')
