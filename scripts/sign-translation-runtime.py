from pathlib import Path
import subprocess
import sys

root=Path(sys.argv[1]);flags=sys.argv[2:]
magic={b"\xcf\xfa\xed\xfe",b"\xfe\xed\xfa\xcf",b"\xca\xfe\xba\xbe",b"\xbe\xba\xfe\xca"}
for path in root.rglob("*"):
    if not path.is_file() or path.is_symlink():continue
    with path.open("rb") as file:header=file.read(4)
    if header in magic:subprocess.run(["codesign",*flags,str(path)],check=True)
for path in sorted(root.rglob("*.framework"),key=lambda p:len(p.parts),reverse=True):
    if not path.is_symlink():subprocess.run(["codesign",*flags,str(path)],check=True)
