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

Language-specific models translate the reviewed source transcript into Turkish
or English. Turkish ↔ English uses Helsinki-NLP's OPUS-MT TC-big models.
Other supported sources use the corresponding Argos source → English model;
Turkish output then uses the English → Turkish model. The CPU helper bundles CTranslate2, SentencePiece and Python; users
do not need to install Python or provide an API key. Media and transcripts stay
on the computer. Speech recognition and translation can both make mistakes.

- Original models: https://huggingface.co/Helsinki-NLP/opus-mt-tc-big-en-tr and https://huggingface.co/Helsinki-NLP/opus-mt-tc-big-tr-en
- Pinned CTranslate2 float16 conversions: https://huggingface.co/ooeoeo/opus-mt-tc-big-en-tr-ct2-float16/tree/4729dd2c625df6a5782d0a2225a36b14d52925b0 and https://huggingface.co/ooeoeo/opus-mt-tc-big-tr-en-ct2-float16/tree/22e01c5dbf6da0e9ac7f49ec4a25266ff28effe6
- Turkish/English model download: about 475 MB per direction. CPU inference uses int8 computation; source and target SentencePiece vocabularies are kept separate and Marian input includes its required end token.
- Other source models: https://github.com/argosopentech/argospm-index ; pinned HTTPS artifacts use the publisher's `data.argosopentech.com` mirror. Downloads are approximately 67–285 MB per source language.
- Exact URLs, revisions, lengths, SHA-256 hashes and extracted-file allowlists: `Sources/LokumCore/Resources/SubtitleTranslationModels.json`, shared by both applications and verification scripts.
- OPUS-MT model authors: Jörg Tiedemann and Santhosh Thottingal, University of Helsinki; https://github.com/Helsinki-NLP/Opus-MT . The upstream TC-big model license is CC-BY-4.0; this takes precedence over conversion-repository metadata. Full license: `Resources/licenses/OPUS-MT-CC-BY-4.0.txt` and the Windows licenses directory.
- Argos Open Technologies packages retain their model README/metadata during installation. MIT license: `Resources/licenses/Argos-Translate-LICENSE.txt`; OPUS-derived models retain their original CC-BY-4.0 attribution. Stanza resources present in upstream archives are neither installed nor loaded.

Only required language pairs download on first use and are cached under
`Lokum/Translation/language-pairs-v2`. Older M2M-100 caches are never used as a fallback.
Selecting the same source/target language needs no translation download. For example,
French → Turkish downloads French → English and English → Turkish, not all models.

Source cues that form one sentence are translated together, then split into
readable timed captions. Long silent gaps are retained. Changing the source
language, source text or target language invalidates the previous result.
