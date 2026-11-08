import importlib.util
from pathlib import Path
import unittest

path=Path(__file__).resolve().parents[1]/"Resources/SubtitleTranslate/worker.py"
spec=importlib.util.spec_from_file_location("subtitle_worker",path)
worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)

class SubtitleTranslationTests(unittest.TestCase):
    def test_joined_overlapping_cues_do_not_truncate_the_original_interval(self):
        cues=[{"start":0,"end":5000,"text":"Please close"},{"start":1000,"end":2000,"text":"the door."}]
        self.assertEqual(worker.group_cues(cues),[{"start":0,"end":5000,"text":"Please close the door."}])
    def test_sentence_context_is_joined_before_translation_and_silence_is_kept(self):
        cues=[{"start":1000,"end":2200,"text":"Please close"},{"start":2200,"end":4000,"text":"the door."},{"start":6000,"end":8000,"text":"Thank you."}]
        self.assertEqual(worker.group_cues(cues),[{"start":1000,"end":4000,"text":"Please close the door."},{"start":6000,"end":8000,"text":"Thank you."}])

    def test_translation_uses_reviewed_source_and_preserves_timed_groups(self):
        request={"sourceLanguage":"en","targetLanguage":"tr","cues":[{"start":1000,"end":3000,"text":"Please close the door."},{"start":5000,"end":7000,"text":"Thank you."}]}
        def translate(texts,source,target):
            if source!="en" or target!="tr":raise AssertionError("Wrong explicit language pair")
            return [{"Please close the door.":"Lütfen kapıyı kapatın.","Thank you.":"Teşekkür ederim."}[text] for text in texts]
        actual=worker.translate_request(request,translate)
        self.assertEqual(actual["cues"],[{"start":1000,"end":3000,"text":"Lütfen kapıyı kapatın."},{"start":5000,"end":7000,"text":"Teşekkür ederim."}])

    def test_long_translation_is_readable_and_covers_original_time_without_overlap(self):
        text="Bu metin, uzun çevirilerin ekrana sığan altyazılara ayrılmasını ve hiçbir kelimenin kaybolmamasını kontrol eder."
        values=worker.split_caption({"start":1000,"end":9000},text)
        self.assertEqual(" ".join(v["text"].replace("\n"," ") for v in values),text)
        self.assertEqual(values[0]["start"],1000);self.assertEqual(values[-1]["end"],9000)
        self.assertTrue(all(a["end"]==b["start"] for a,b in zip(values,values[1:])))
        self.assertTrue(all(v["start"]<v["end"] and len(v["text"].splitlines())<=2 for v in values))
        self.assertTrue(all(len(line)<=42 for v in values for line in v["text"].splitlines()))

    def test_missing_language_and_empty_or_truncated_translation_are_rejected(self):
        request={"sourceLanguage":"auto","targetLanguage":"tr","cues":[{"start":0,"end":2000,"text":"Hello."}]}
        with self.assertRaises(ValueError):worker.translate_request(request,lambda *_:["Merhaba."])
        request["sourceLanguage"]="en"
        for result in [[],[""],["Merhaba.","Extra"]]:
            with self.assertRaises(ValueError):worker.translate_request(request,lambda *_:result)

if __name__=="__main__":unittest.main()
