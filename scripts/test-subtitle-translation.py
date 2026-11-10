import importlib.util
from pathlib import Path
import unittest
import json
import os
import subprocess
import sys
import tempfile

path=Path(__file__).resolve().parents[1]/"Resources/SubtitleTranslate/worker.py"
spec=importlib.util.spec_from_file_location("subtitle_worker",path)
worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)

class SubtitleTranslationTests(unittest.TestCase):
    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_other_turkish_names_keep_their_original_spelling(self):
        request={"sourceLanguage":"tr","targetLanguage":"en","cues":[
            {"start":0,"end":3000,"text":"Ayşe Yılmaz'cığım, bize yarın gel."},
            {"start":4000,"end":7000,"text":"Deniz Akın'ın annesi geldi."}]}
        result=worker.translate_request(request,worker.local_translator(os.environ["LOKUM_TEST_TRANSLATION_MODEL"]))
        text=" ".join(c["text"].replace("\n"," ") for c in result["cues"])
        self.assertIn("Ayşe Yılmaz",text);self.assertIn("Deniz Akın",text)
        self.assertIn("tomorrow",text.lower());self.assertIn("mother",text.lower())

    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_turkish_surname_and_familiar_address_remain_the_same_person(self):
        request={"sourceLanguage":"tr","targetLanguage":"en","cues":[
            {"start":0,"end":2400,"text":"Abi Yaren Duman'ın doğum gününü kutlayıp"},
            {"start":2400,"end":4040,"text":"kokulu ve öpücükler yollar mısın?"},
            {"start":4040,"end":6480,"text":"Yaren Duman'cım doğum günün kutlu olsun kardeş."},
            {"start":7920,"end":10710,"text":"Rabbim hayırlı günler versin. Baba."},
            {"start":10710,"end":12000,"text":"Canım benim seni seviyoruz."}]}
        result=worker.translate_request(request,worker.local_translator(os.environ["LOKUM_TEST_TRANSLATION_MODEL"]))
        first=" ".join(c["text"].replace("\n"," ") for c in result["cues"] if c["start"]<4040)
        greeting=" ".join(c["text"].replace("\n"," ") for c in result["cues"] if 4040<=c["start"]<6480)
        blessing=" ".join(c["text"].replace("\n"," ") for c in result["cues"] if 7920<=c["start"]<10710)
        self.assertIn("Yaren Duman",first);self.assertIn("Yaren Duman",greeting)
        self.assertNotIn("smok",greeting.lower());self.assertIn("birthday",greeting.lower())
        self.assertRegex(blessing.lower(),r"\b(dad|father|papa)\b")
        self.assertRegex(blessing.lower(),r"\b(you|your)\b")

    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_spanish_source_produces_readable_english(self):
        result=worker.translate_request({"sourceLanguage":"es","targetLanguage":"en","cues":[{"start":1000,"end":4000,"text":"Por favor, cierra la ventana."}]},worker.local_translator(os.environ["LOKUM_TEST_TRANSLATION_MODEL"]))
        text=" ".join(c["text"] for c in result["cues"])
        self.assertIn("window",text.lower());self.assertNotIn("@@",text)

    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_french_translation_contains_normal_words_without_tokenizer_markers(self):
        result=worker.translate_request({"sourceLanguage":"fr","targetLanguage":"en","cues":[{"start":1000,"end":4000,"text":"Veuillez fermer la fenêtre."}]},worker.local_translator(os.environ["LOKUM_TEST_TRANSLATION_MODEL"]))
        text=" ".join(c["text"] for c in result["cues"])
        self.assertIn("window",text.lower());self.assertNotIn("▁",text)

    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_turkish_birthday_message_keeps_we_instead_of_i(self):
        request={"sourceLanguage":"tr","targetLanguage":"en","cues":[{"start":7920,"end":10710,"text":"Rabbim aylıyor günler versin. Baba canım"},{"start":10710,"end":12000,"text":"benim seni seviyoruz."}]}
        result=worker.translate_request(request,worker.local_translator(os.environ["LOKUM_TEST_TRANSLATION_MODEL"]))
        text=" ".join(c["text"].replace("\n"," ") for c in result["cues"]).lower()
        self.assertIn("we love you",text)
        self.assertNotIn("i love",text)

    @unittest.skipUnless(os.environ.get("LOKUM_TEST_TRANSLATION_MODEL"),"Install real translation models to run inference.")
    def test_real_turkish_translation_preserves_negation_and_does_not_repeat(self):
        request={"sourceLanguage":"en","targetLanguage":"tr","cues":[{"start":1000,"end":4000,"text":"I did not say that he stole the money."},{"start":6000,"end":9000,"text":"Please close the window."}]}
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/"request.json").write_text(json.dumps(request),encoding="utf-8")
            result=subprocess.run([sys.executable,str(path),"--model",os.environ["LOKUM_TEST_TRANSLATION_MODEL"],"--request",str(root/"request.json"),"--response",str(root/"response.json")],capture_output=True,text=True,encoding="utf-8",timeout=120)
            self.assertEqual(result.returncode,0,result.stderr)
            translated=json.loads((root/"response.json").read_text(encoding="utf-8"))
            text=" ".join(c["text"].replace("\n"," ") for c in translated["cues"])
            self.assertIn("söylemedim",text);self.assertIn("pencere",text)
            self.assertLess(len(text),160)
            self.assertEqual(translated["cues"][0]["start"],1000);self.assertEqual(translated["cues"][-1]["end"],9000)

    def test_non_turkish_or_english_output_is_rejected_before_translation(self):
        request={"sourceLanguage":"en","targetLanguage":"de","cues":[{"start":0,"end":2000,"text":"Hello."}]}
        with self.assertRaises(ValueError):worker.translate_request(request,lambda *_:["Hallo."])

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
