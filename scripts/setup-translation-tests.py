from pathlib import Path
import hashlib
import sys
import urllib.request
import zipfile

root=Path(sys.argv[1] if len(sys.argv)>1 else "build/translation-fixtures")
root.mkdir(parents=True,exist_ok=True)
archive=root/"m2m100.argosmodel"
digest="8a63b8e9228f985d081dd166e336acde5f9083f1f4d7b39e14f7f2d57e5405b4"
if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest()!=digest:
    request=urllib.request.Request("https://data.argosopentech.com/argospm/v2/translate-fairseq_m2m_100_418M.argosmodel",headers={"User-Agent":"Lokum-translation-verification"})
    with urllib.request.urlopen(request,timeout=60) as response,archive.open("wb") as file:
        while chunk:=response.read(1024*1024):file.write(chunk)
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=digest:raise RuntimeError("Translation model checksum mismatch")
with zipfile.ZipFile(archive) as zip:
    for relative in ["model/model.bin","model/shared_vocabulary.txt","sentencepiece.model"]:
        target=root/relative;target.parent.mkdir(parents=True,exist_ok=True)
        with zip.open("translate-fairseq_m2m_100_418M/"+relative) as source,target.open("wb") as file:
            while chunk:=source.read(1024*1024):file.write(chunk)
print("Verified offline translation model prepared")
