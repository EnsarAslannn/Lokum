from pathlib import Path
import hashlib
import json
import shutil
import sys
import urllib.request
import zipfile

repository=Path(__file__).resolve().parents[1]
root=Path(sys.argv[1] if len(sys.argv)>1 else "build/translation-fixtures")
root.mkdir(parents=True,exist_ok=True)
models=json.loads((repository/"Sources/LokumCore/Resources/SubtitleTranslationModels.json").read_text(encoding="utf-8"))
for model in models:
    destination=root/model["id"];destination.mkdir(parents=True,exist_ok=True)
    for index,asset in enumerate(model["assets"]):
        cache=root/"downloads"/(model["id"]+"-"+str(index));cache.parent.mkdir(parents=True,exist_ok=True)
        def verified(path):
            if not path.is_file() or path.stat().st_size!=asset["size"]:return False
            with path.open("rb") as file:return hashlib.file_digest(file,"sha256").hexdigest()==asset["sha256"]
        if not asset.get("entries") and verified(destination/asset["path"]):continue
        if not verified(cache):
            request=urllib.request.Request(asset["url"],headers={"User-Agent":"Lokum-translation-verification"})
            with urllib.request.urlopen(request,timeout=60) as response,cache.open("wb") as file:shutil.copyfileobj(response,file)
            if not verified(cache):raise RuntimeError("Translation model checksum mismatch")
        if asset.get("entries"):
            with zipfile.ZipFile(cache) as archive:
                for entry,relative in asset["entries"].items():
                    output=destination/relative;output.parent.mkdir(parents=True,exist_ok=True)
                    with archive.open(entry) as source,output.open("wb") as target:shutil.copyfileobj(source,target)
        elif not verified(destination/asset["path"]):cache.replace(destination/asset["path"])
    print("Verified translation model: "+model["id"],flush=True)
