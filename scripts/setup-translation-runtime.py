from pathlib import Path
import os
import json
import shutil
import subprocess
import sys
import venv

root=Path(__file__).resolve().parents[1]
environment=root/"build/translation-env"
python=environment/("Scripts/python.exe" if sys.platform=="win32" else "bin/python")
if not python.exists():venv.create(environment,with_pip=True)
packages=["numpy==2.2.6","pyinstaller==6.16.0","typing_extensions==4.16.0","diskcache==5.6.3","Jinja2==3.1.6","MarkupSafe==3.0.4"]
build_environment=os.environ.copy()
if sys.platform=="win32":
    runtime="https://github.com/abetlen/llama-cpp-python/releases/download/v0.3.36/llama_cpp_python-0.3.36-py3-none-win_amd64.whl#sha256=ae5e88a2cf464e5a2dde7c8898c2d5c71cc24d03cac6396bf7509456bde0b9fb"
elif sys.platform=="darwin":
    runtime="https://github.com/abetlen/llama-cpp-python/releases/download/v0.3.36/llama_cpp_python-0.3.36.tar.gz#sha256=6eed0d1931e1b6ee6ea43bd320ea438c6494bddac4e46e5133afc3b10594964f"
    build_environment["CMAKE_ARGS"]="-DGGML_NATIVE=OFF -DGGML_METAL=OFF -DGGML_ACCELERATE=OFF -DGGML_OPENMP=OFF -DCMAKE_OSX_DEPLOYMENT_TARGET=13.3"
    build_environment["MACOSX_DEPLOYMENT_TARGET"]="13.3"
    build_environment["CMAKE_BUILD_PARALLEL_LEVEL"]="3"
else:raise RuntimeError("Build the desktop translation runtime on Windows or macOS.")
subprocess.run([str(python),"-m","pip","install","--disable-pip-version-check",*packages,runtime],check=True,env=build_environment)
subprocess.run([str(python),str(root/"scripts/test-subtitle-translation.py")],check=True)
command=[str(python),"-m","PyInstaller","--noconfirm","--clean","--onedir","--name","translate","--distpath",str(root/"build/translation-dist"),"--workpath",str(root/"build/translation-freeze"),"--specpath",str(root/"build/translation-freeze")]
for package in ["llama_cpp","numpy","jinja2","markupsafe","diskcache"]:command.extend(["--collect-all",package])
metadata_packages=["llama-cpp-python","numpy","pyinstaller","typing_extensions","diskcache","Jinja2","MarkupSafe","setuptools"]
installed=json.loads(subprocess.check_output([str(python),"-m","pip","list","--format=json","--disable-pip-version-check"],text=True))
if any(p["name"].lower()=="pyyaml" for p in installed):metadata_packages.append("PyYAML")
for package in metadata_packages:command.extend(["--copy-metadata",package])
subprocess.run([*command,str(root/"Resources/SubtitleTranslate/worker.py")],check=True,cwd=root)
destination=root/"Tools/Translate"
if destination.resolve()!=root.resolve()/"Tools/Translate":raise RuntimeError("The generated runtime directory is outside the repository.")
if destination.exists():shutil.rmtree(destination)
shutil.copytree(root/"build/translation-dist/translate",destination,dirs_exist_ok=True,symlinks=True)
reported=metadata_packages
report="import importlib.metadata as m; import sys; print('Python=='+sys.version.split()[0]); print('\\n'.join(name+'=='+m.version(name) for name in "+repr(reported)+"))"
with (destination/"runtime-versions.txt").open("w",encoding="utf-8") as output:subprocess.run([str(python),"-c",report],stdout=output,check=True)
for name in ["Qwen3.5-LICENSE.txt","llama-cpp-python-LICENSE.txt","llama.cpp-LICENSE.txt"]:shutil.copy2(root/"Resources/licenses"/name,destination/name)
for name in ["LICENSE.txt","LICENSE"]:
    license=Path(sys.base_prefix)/name
    if license.is_file():shutil.copy2(license,destination/"Python-LICENSE.txt");break
print("Local subtitle translation runtime prepared")
