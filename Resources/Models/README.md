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

M2M-100 418M translates the reviewed source transcript directly between selected
languages. The CPU helper bundles CTranslate2, SentencePiece and Python; users
do not need to install Python or provide an API key. Media and transcripts stay
on the computer. Speech recognition and translation can both make mistakes.

- Model: https://github.com/facebookresearch/fairseq/tree/main/examples/m2m_100
- Verified conversion: https://data.argosopentech.com/argospm/v2/translate-fairseq_m2m_100_418M.argosmodel
- Download: 458,624,432 bytes (about 459 MB), acquired on first translation.
- SHA-256: `8a63b8e9228f985d081dd166e336acde5f9083f1f4d7b39e14f7f2d57e5405b4`
- License: MIT, preserved in `Resources/licenses/M2M100-LICENSE.txt` and the Windows licenses directory.

Source cues that form one sentence are translated together, then split into
readable timed captions. Long silent gaps are retained. Changing the source
language, source text or target language invalidates the previous result.
