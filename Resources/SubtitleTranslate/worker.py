import argparse
import json
import os
from pathlib import Path
import re
import sys
import textwrap

LANGUAGES={"tr","en","de","fr","es","it","ar","ru","ja","ko","zh"}

def group_cues(cues):
    groups=[];previous=-1
    for cue in cues:
        a,b,text=cue["start"],cue["end"]," ".join(cue["text"].split())
        if not isinstance(a,int) or not isinstance(b,int) or a<0 or b<=a or a<previous or not text:
            raise ValueError("Invalid source transcript timing or text.")
        previous=a
        if groups and a-groups[-1]["end"]<=750 and b-groups[-1]["start"]<=15000 and len(groups[-1]["text"])+len(text)<700 and not re.search(r'[.!?…。！？][\"\u201d\u2019\u00bb)]*$',groups[-1]["text"]):
            groups[-1]["text"]+=" "+text;groups[-1]["end"]=max(groups[-1]["end"],b)
        else:groups.append({"start":a,"end":b,"text":text})
    if not groups:raise ValueError("The source transcript is empty.")
    return groups

def split_caption(cue,text):
    lines=textwrap.wrap(" ".join(text.split()),width=42,break_long_words=True,break_on_hyphens=False)
    if not lines:raise ValueError("The translation is empty.")
    chunks=["\n".join(lines[n:n+2]) for n in range(0,len(lines),2)]
    if cue["end"]-cue["start"]<len(chunks):raise ValueError("The subtitle interval is too short.")
    total=sum(len(c) for c in chunks);used=0;start=cue["start"];result=[]
    for n,chunk in enumerate(chunks):
        used+=len(chunk)
        end=cue["end"] if n==len(chunks)-1 else max(start+1,min(cue["end"]-(len(chunks)-n-1),cue["start"]+round((cue["end"]-cue["start"])*used/total)))
        result.append({"start":start,"end":end,"text":chunk});start=end
    return result

def translate_request(request,translate):
    source,target=request.get("sourceLanguage"),request.get("targetLanguage")
    if source not in LANGUAGES or target not in LANGUAGES:raise ValueError("Choose the source and subtitle languages.")
    groups=group_cues(request["cues"])
    texts=[c["text"] for c in groups]
    translated=texts if source==target else translate(texts,source,target)
    if len(translated)!=len(groups) or any(not isinstance(t,str) or not t.strip() for t in translated):raise ValueError("The translation was incomplete. No subtitles were saved.")
    cues=[part for group,text in zip(groups,translated) for part in split_caption(group,text)]
    return {"sourceLanguage":source,"targetLanguage":target,"cues":cues}

def local_translator(model):
    import ctranslate2
    import sentencepiece
    root=Path(model)
    sp=sentencepiece.SentencePieceProcessor(model_file=str(root/"sentencepiece.model"))
    engine=ctranslate2.Translator(str(root/"model"),device="cpu",compute_type="int8",inter_threads=1,intra_threads=min(8,max(1,(os.cpu_count() or 2)//2)))
    def run(texts,source,target):
        output=[]
        for offset in range(0,len(texts),8):
            batch=texts[offset:offset+8];tokens=[["__"+source+"__"]+sp.encode(text,out_type=str) for text in batch]
            if any(len(t)>512 for t in tokens):raise ValueError("A transcript paragraph is too long. Split it into shorter subtitles.")
            results=engine.translate_batch(tokens,target_prefix=[["__"+target+"__"] for _ in tokens],beam_size=4,max_decoding_length=512,max_input_length=0,no_repeat_ngram_size=3)
            output.extend(sp.decode(result.hypotheses[0][1:]) for result in results)
            print(json.dumps({"fraction":min(1,(offset+len(batch))/len(texts))}),flush=True)
        return output
    return run

def main():
    parser=argparse.ArgumentParser();parser.add_argument("--request",required=True);parser.add_argument("--response",required=True);parser.add_argument("--model",required=True)
    args=parser.parse_args()
    request=json.loads(Path(args.request).read_text(encoding="utf-8-sig"))
    output=translate_request(request,local_translator(args.model))
    target=Path(args.response)
    with target.open("x",encoding="utf-8") as file:json.dump(output,file,ensure_ascii=False)

if __name__=="__main__":
    try:main()
    except Exception as error:print(str(error),file=sys.stderr);sys.exit(1)
