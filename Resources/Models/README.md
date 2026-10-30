# Offline background removal

`u2netp.onnx` is the lightweight U²-Net salient-object segmentation model.
It is embedded in the Windows core assembly and runs locally with ONNX Runtime.
No image upload, runtime model download, Python installation or API key is used.
Windows requires the Microsoft Visual C++ x64 runtime used by ONNX Runtime:
https://onnxruntime.ai/docs/install/#requirements

- Model authors/source: https://github.com/xuebinqin/U-2-Net
- ONNX distribution: https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx
- Upstream model selection and preprocessing reference: https://github.com/danielgatis/rembg/blob/main/rembg/sessions/u2netp.py
- Upstream published MD5: `8e83ca70e441ab06c318d82300c84806`
- Pinned SHA-256: `309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8`
- Size: 4,574,861 bytes
- Model license: Apache-2.0; complete text in `windows/licenses/U-2-Net-LICENSE.txt`.
- ONNX Runtime license: MIT; complete text and bundled dependency notices in `windows/licenses/ONNX-Runtime-*`.

Input: RGB, 320 × 320, channel-first, ImageNet mean/deviation after maximum-value
normalization. Output: foreground probability mask, resized to the original
oriented image dimensions. The mask multiplies existing alpha; RGB values and
existing transparent regions are preserved. Exports are fresh RGBA PNGs.

The lightweight model is approximate. Fine hair, transparent objects, multiple
subjects and busy backgrounds can require further editing. Inspect the preview.
This Windows-only capability is separate from the shared macOS tool catalog.
