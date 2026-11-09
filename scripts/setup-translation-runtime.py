from pathlib import Path
import shutil
import subprocess
import sys
import venv

root=Path(__file__).resolve().parents[1]
environment=root/"build/translation-env"
python=environment/("Scripts/python.exe" if sys.platform=="win32" else "bin/python")
if not python.exists():venv.create(environment,with_pip=True)
packages=["ctranslate2==4.8.4","sentencepiece==0.2.1","numpy==2.2.6","PyYAML==6.0.3","pyinstaller==6.16.0","sacremoses==0.1.1","subword-nmt==0.3.8","regex==2026.11.9","click==8.5.1","joblib==1.6.0","tqdm==4.70.1","mock==5.2.0","cloudpickle==3.1.2","colorama==0.4.6"]
subprocess.run([str(python),"-m","pip","install","--disable-pip-version-check",*packages],check=True)
subprocess.run([str(python),str(root/"scripts/test-subtitle-translation.py")],check=True)
command=[str(python),"-m","PyInstaller","--noconfirm","--clean","--onedir","--name","translate","--distpath",str(root/"build/translation-dist"),"--workpath",str(root/"build/translation-freeze"),"--specpath",str(root/"build/translation-freeze")]
for package in ["ctranslate2","sentencepiece","numpy","yaml","sacremoses","subword_nmt"]:command.extend(["--collect-all",package])
for package in ["ctranslate2","sentencepiece","numpy","PyYAML","pyinstaller","sacremoses","subword-nmt","regex","click","joblib","tqdm","mock","cloudpickle","colorama"]:command.extend(["--copy-metadata",package])
subprocess.run([*command,str(root/"Resources/SubtitleTranslate/worker.py")],check=True,cwd=root)
destination=root/"Tools/Translate"
shutil.copytree(root/"build/translation-dist/translate",destination,dirs_exist_ok=True,symlinks=True)
subprocess.run([str(python),"-m","pip","freeze"],stdout=(destination/"runtime-versions.txt").open("w"),check=True)
for name in ["OPUS-MT-CC-BY-4.0.txt","Argos-Translate-LICENSE.txt"]:shutil.copy2(root/"Resources/licenses"/name,destination/name)
for name in ["LICENSE.txt","LICENSE"]:
    license=Path(sys.base_prefix)/name
    if license.is_file():shutil.copy2(license,destination/"Python-LICENSE.txt");break
print("Local subtitle translation runtime prepared")
