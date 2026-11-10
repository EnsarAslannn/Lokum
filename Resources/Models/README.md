# Offline background removal

`u2netp.onnx` is the lightweight U²-Net salient-object segmentation model.
It is embedded in the Windows core assembly and the macOS core resource bundle,
and runs locally with ONNX Runtime. The shared model is stored at
`Sources/LokumCore/Resources/u2netp.onnx`.
No image upload, runtime model download, Python installation or API key is used.
Windows requires the Microsoft Visual C++ x64 runtime used by ONNX Runtime:
https://onnxruntime.ai/docs/install/#requirements

- Model authors/source: https://github.com/xuebinqin/U-2-Net
- ONNX distribution: https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx
- Upstream model selection and preprocessing reference: https://github.com/danielgatis/rembg/blob/main/rembg/sessions/u2netp.py
- Upstream published MD5: `8e83ca70e441ab06c318d82300c84806`
- Pinned SHA-256: `309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8`
- Size: 4,574,861 bytes
- Model license: Apache-2.0; complete text in `Resources/licenses/U-2-Net-LICENSE.txt` and `windows/licenses/U-2-Net-LICENSE.txt`.
- ONNX Runtime license: MIT; macOS text in `Resources/licenses/ONNX-Runtime-LICENSE.txt`; Windows text and dependency notices in `windows/licenses/ONNX-Runtime-*`.

Input: RGB, 320 × 320, channel-first, ImageNet mean/deviation after maximum-value
normalization. Output: foreground probability mask, resized to the original
oriented image dimensions. The mask multiplies existing alpha; RGB values and
existing transparent regions are preserved. Exports are fresh RGBA PNGs.

The lightweight model is approximate. Fine hair, transparent objects, multiple
subjects and busy backgrounds can require further editing. Inspect the preview.
Manual selections and background replacement use the same image canvas on both platforms.

## Offline subtitle translation

Qwen3.5-9B translates the reviewed source transcript directly into Turkish or
English. One shared model serves all eleven supported source languages. The
CPU helper bundles llama.cpp, llama-cpp-python and Python; users do not need
to install Python, run a server, provide an API key or upload their transcripts.
Speech recognition and automatic translation can both make mistakes.

- Original model: https://huggingface.co/Qwen/Qwen3.5-9B
- Q4_K_M GGUF conversion by Unsloth: https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/tree/3885219b6810b007914f3a7950a8d1b469d598a5
- Model: `Qwen3.5-9B-Q4_K_M.gguf`, 5,680,522,464 bytes (about 5.7 GB).
- SHA-256: `03b74727a860a56338e042c4420bb3f04b2fec5734175f4cb9fa853daf52b7e8`.
- Shared artifact manifest: `Sources/LokumCore/Resources/SubtitleTranslationModels.json`.
- Model license: Apache-2.0, retained in `Resources/licenses/Qwen3.5-LICENSE.txt` and the Windows licenses directory.
- llama.cpp and llama-cpp-python 0.3.36: MIT; full licenses are preserved alongside the model license and in the bundled helper.

The model is downloaded once into `Lokum/Translation/context-v3/qwen35-9b`,
with length and checksum validation before atomic publication. Changing source
or target languages reuses the same file. Same-language subtitles need no
translation model. Older language-pair caches remain separate. CPU inference
requires more memory and time than the earlier small translation models.

Translation uses bounded batches, the model's chat template with thinking disabled,
and a JSON grammar requiring one nonempty translation per timed group. Personal
names, forms of address, questions, negation and subjects are explicitly preserved.
Source text is tokenized without control-token interpretation. Invalid, missing or
truncated output blocks export rather than saving a partial result. There are no
network calls during inference. The context window is 4,096 tokens and at most
eight CPU threads are used. The macOS helper is built for CPU-only execution with
the application's minimum deployment target.

Source cues that form one sentence are translated together, then split into
readable timed captions. Long silent gaps are retained. Changing the source
language, source text or target language invalidates the previous result.
