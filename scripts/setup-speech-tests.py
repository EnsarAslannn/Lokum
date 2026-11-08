import hashlib
from pathlib import Path
import sys
import urllib.request

root=Path(sys.argv[1] if len(sys.argv)>1 else "build/speech-fixtures")
root.mkdir(parents=True,exist_ok=True)
for name,url,digest in [
    ("ggml-base.bin","https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.bin","60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe"),
    ("jfk.wav","https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.2/samples/jfk.wav","59dfb9a4acb36fe2a2affc14bacbee2920ff435cb13cc314a08c13f66ba7860e")
]:
    if "--sample-only" in sys.argv and name!="jfk.wav":continue
    path=root/name
    if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest()==digest:continue
    pending=path.with_suffix(".download")
    try:
        request=urllib.request.Request(url,headers={"User-Agent":"Lokum-speech-verification"})
        with urllib.request.urlopen(request,timeout=60) as response,pending.open("wb") as target:
            while data:=response.read(1024*1024):target.write(data)
        if hashlib.sha256(pending.read_bytes()).hexdigest()!=digest:raise RuntimeError("Speech fixture checksum mismatch")
        pending.replace(path)
    finally:pending.unlink(missing_ok=True)
print("Verified multilingual speech model and known speech fixture")
