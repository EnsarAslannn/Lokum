import hashlib
import io
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

root=Path(__file__).resolve().parents[1]/"Tools/Speech"
digest="49dcc16de826f20bd53d44f947a1ae49dfa81f86cad67a64d80820cb192d674a"
request=urllib.request.Request("https://api.github.com/repos/ggml-org/whisper.cpp/releases/assets/501504923",headers={"User-Agent":"Lokum-build","Accept":"application/octet-stream"})
try:
    with urllib.request.urlopen(request,timeout=30) as response:data=response.read(32*1024*1024+1)
except Exception:
    if not shutil.which("gh"):raise
    data=subprocess.check_output(["gh","api","-H","Accept: application/octet-stream","repos/ggml-org/whisper.cpp/releases/assets/501504923"],timeout=120)
if hashlib.sha256(data).hexdigest()!=digest:raise RuntimeError("Speech runtime checksum mismatch")
with zipfile.ZipFile(io.BytesIO(data)) as archive:
    chosen={}
    for item in archive.infolist():
        name=Path(item.filename).name
        if name in {"whisper-cli.exe","whisper.dll","ggml.dll","ggml-base.dll"} or (name.startswith("ggml-cpu-") and name.endswith(".dll")):
            if name in chosen or item.file_size>64*1024*1024:raise RuntimeError("Unexpected speech runtime archive")
            chosen[name]=item
    if not {"whisper-cli.exe","whisper.dll","ggml.dll","ggml-base.dll","ggml-cpu-x64.dll"}.issubset(chosen):raise RuntimeError("Incomplete speech runtime")
    root.mkdir(parents=True,exist_ok=True)
    for name,item in chosen.items():(root/name).write_bytes(archive.read(item))
print("Verified whisper.cpp v1.9.2 CPU runtime prepared")
