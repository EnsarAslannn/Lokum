import XCTest
@testable import LokumCore

final class TranscriptionTests:XCTestCase {
    func testVerifiedSharedTranslationModelInstallationProducesUsableEnglishSubtitles()async throws {
        guard ProcessInfo.processInfo.environment["LOKUM_TEST_INSTALL_TRANSLATION"]=="1" else {throw XCTSkip("Fresh model installation is exercised by engine CI.")}
        try await TranslationModelSetup.install(source:"fr",target:"en",token:CancellationToken()) {_,_ in}
        let source=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:03,000\nVeuillez fermer la fenêtre.\n",duration:4)
        let translated=try SubtitleTranslationEngine.translate(source,duration:4,sourceLanguage:"fr",targetLanguage:"en")
        XCTAssertTrue(translated.text.lowercased().contains("window"));XCTAssertFalse(translated.text.contains("▁"))
    }
    func testRealTranslationUsesChosenLanguagesAndEditedTranscript()throws {
        guard let model=ProcessInfo.processInfo.environment["LOKUM_TEST_TRANSLATION_MODEL"] else {throw XCTSkip("Translation fixtures are installed by engine CI.")}
        let source=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nHello, how are you?\n\n2\n00:00:06,000 --> 00:00:09,000\nPlease close the door.\n",duration:10)
        let turkish=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nLütfen kapıyı kapatın.\n\n2\n00:00:06,000 --> 00:00:09,000\nLütfen pencereyi kapatın.\n",duration:10)
        let german=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nBitte schließen Sie die Tür.\n\n2\n00:00:06,000 --> 00:00:09,000\nBitte schließen Sie das Fenster.\n",duration:10)
        let french=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nVeuillez fermer la porte.\n\n2\n00:00:06,000 --> 00:00:09,000\nVeuillez fermer la fenêtre.\n",duration:10)
        for (from,target,original,word) in [("en","tr",source,"kapı"),("tr","en",turkish,"door"),("de","en",german,"door"),("fr","tr",french,"kapı")] {
            let translated=try SubtitleTranslationEngine.translate(original,duration:10,sourceLanguage:from,targetLanguage:target,model:URL(fileURLWithPath:model))
            XCTAssertTrue(translated.text.contains(word),translated.text)
            XCTAssertEqual(translated.cues.first?.start,1000);XCTAssertEqual(translated.cues.last?.end,9000)
            XCTAssertFalse(translated.cues.contains{$0.start<6000 && $0.end>4000})
        }
        let edited=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nPlease close the window.\n",duration:10)
        XCTAssertTrue(try SubtitleTranslationEngine.translate(edited,duration:10,sourceLanguage:"en",targetLanguage:"tr",model:URL(fileURLWithPath:model)).text.contains("pencere"))
        let birthday=try SubtitleTranscript(srt:"1\n00:00:01,000 --> 00:00:04,000\nBaba canım benim seni seviyoruz.\n",duration:10)
        XCTAssertTrue(try SubtitleTranslationEngine.translate(birthday,duration:10,sourceLanguage:"tr",targetLanguage:"en",model:URL(fileURLWithPath:model)).text.lowercased().contains("we love you"))
        XCTAssertTrue(source.text.contains("door"))
        XCTAssertEqual(try SubtitleTranslationEngine.translate(source,duration:10,sourceLanguage:"en",targetLanguage:"en").srt,source.srt)
    }
    func testSubtitleWorkflowSeparatesSourceAndTargetAndInvalidatesStaleTranslations()throws {
        let english="1\n00:00:01,000 --> 00:00:03,000\nPlease close the door.\n"
        let turkish="1\n00:00:01,000 --> 00:00:03,000\nLütfen kapıyı kapatın.\n"
        var flow=SubtitleWorkflow(duration:4)
        XCTAssertFalse(flow.canSave);XCTAssertFalse(flow.canTranslate)
        let required=try TranslationModelSetup.requiredModels(source:"fr",target:"tr")
        XCTAssertEqual(required.count,1);XCTAssertEqual(try TranslationModelSetup.requiredModels(source:"tr",target:"en"),required)
        XCTAssertEqual(try TranslationModelSetup.requiredModels(source:"tr",target:"tr"),[])
        XCTAssertThrowsError(try flow.setSource(english,language:"auto"))
        try flow.setSource(english,language:"en");try flow.chooseTarget("tr");let revision=flow.revision
        XCTAssertTrue(flow.canTranslate);XCTAssertFalse(flow.canSave)
        XCTAssertTrue(try flow.acceptTranslation(turkish,revision:revision,target:"tr"))
        XCTAssertEqual(flow.source?.at(2),"Please close the door.");XCTAssertEqual(flow.subtitles?.at(2),"Lütfen kapıyı kapatın.")
        try flow.setSource(english.replacingOccurrences(of:"door",with:"window"),language:"en")
        XCTAssertFalse(flow.canSave);XCTAssertFalse(try flow.acceptTranslation(turkish,revision:revision,target:"tr"))
        XCTAssertThrowsError(try flow.chooseTarget("de"))
        try flow.chooseTarget("en");XCTAssertTrue(flow.useSourceLanguage());XCTAssertTrue(flow.subtitles?.text.contains("window") == true)
        flow.invalidateSource();XCTAssertFalse(flow.canSave)
    }
    func testRealSpeechInferenceAndSubtitledVideoExport()throws {
        guard let root=ProcessInfo.processInfo.environment["LOKUM_TEST_SPEECH_FIXTURES"] else {throw XCTSkip("Speech fixtures are installed by the engine CI job.")}
        let fixtures=URL(fileURLWithPath:root),directory=FileManager.default.temporaryDirectory.appendingPathComponent("Lokum-SpeechTests-\(UUID().uuidString)",isDirectory:true)
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:false);defer{try? FileManager.default.removeItem(at:directory)}
        let source=directory.appendingPathComponent("Konuşma 'Türkçe'.mp4")
        let ffmpeg=try Dependencies.executable("ffmpeg")
        try ProcessRunner.run(ffmpeg,["-v","error","-f","lavfi","-i","color=black:size=640x360:rate=15","-i",fixtures.appendingPathComponent("jfk.wav").path,"-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",source.path])
        let original=try Data(contentsOf:source)
        let document=try TranscriptionEngine.transcribe(source,model:fixtures.appendingPathComponent("ggml-base.bin"),language:"en")
        XCTAssertTrue(document.text.lowercased().contains("country"))
        let target=directory.appendingPathComponent("Saved 'çalışma'",isDirectory:true)
        let srt=try XCTUnwrap(TranscriptionEngine.export(document,input:source,format:"srt",destination:target).first)
        XCTAssertEqual(try String(contentsOf:srt,encoding:.utf8),document.srt)
        let video=try XCTUnwrap(TranscriptionEngine.export(document,input:source,format:"mp4",destination:target).first)
        let info=try MediaEngine.inspect(video)
        XCTAssertEqual(info.video?.width,640);XCTAssertNotNil(info.audio)
        let decoded=directory.appendingPathComponent("subtitled-audio.s16")
        try ProcessRunner.run(ffmpeg,["-y","-v","error","-i",video.path,"-map","0:a:0","-vn","-ar","16000","-ac","1","-f","s16le",decoded.path])
        let samples=try Data(contentsOf:decoded)
        XCTAssertTrue(stride(from:0,to:samples.count-1,by:2).contains{abs(Int(Int16(bitPattern:UInt16(samples[$0]) | UInt16(samples[$0+1])<<8)))>1000},"Subtitled output retains audible speech")
        let multi=directory.appendingPathComponent("multiple-audio.mp4")
        try ProcessRunner.run(ffmpeg,["-y","-v","error","-i",source.path,"-f","lavfi","-i","anullsrc=r=48000:cl=stereo","-map","0:v:0","-map","1:a:0","-map","0:a:0","-c:v","copy","-c:a","aac","-disposition:a:0","0","-disposition:a:1","default","-shortest",multi.path])
        let multiOutput=try XCTUnwrap(TranscriptionEngine.export(document,input:multi,format:"mp4",destination:target).first)
        XCTAssertEqual(try MediaEngine.inspect(multiOutput).streams.filter{$0.kind=="audio"}.count,2,"An audible default audio track after a silent first track must be preserved")
        let frame=directory.appendingPathComponent("frame.gray")
        try ProcessRunner.run(ffmpeg,["-v","error","-ss","1","-i",video.path,"-frames:v","1","-pix_fmt","gray","-f","rawvideo",frame.path])
        XCTAssertGreaterThan(try Data(contentsOf:frame).filter{$0>100}.count,50)
        XCTAssertEqual(try Data(contentsOf:source),original)
        let cancel=CancellationToken();cancel.cancel()
        XCTAssertThrowsError(try TranscriptionEngine.transcribe(source,token:cancel))
    }
    func testMediaWheelOffersSubtitlesForOneSpokenMediaFile() {
        XCTAssertTrue(FeatureCatalog.tools(for:[URL(fileURLWithPath:"/speech.mp4")]).contains(.generateSubtitles))
        XCTAssertTrue(FeatureCatalog.tools(for:[URL(fileURLWithPath:"/speech.wav")]).contains(.generateSubtitles))
        XCTAssertFalse(FeatureCatalog.tools(for:[URL(fileURLWithPath:"/image.png")]).contains(.generateSubtitles))
        XCTAssertFalse(FeatureCatalog.tools(for:[URL(fileURLWithPath:"/a.mp4"),URL(fileURLWithPath:"/b.mp4")]).contains(.generateSubtitles))
    }
    func testEditedTextAndTimingReachPlaybackAndExport()throws {
        let document=try SubtitleTranscript(srt:"1\n00:00:00,250 --> 00:00:01,500\nDüzeltilmiş metin.\n",duration:4)
        XCTAssertEqual(document.text,"Düzeltilmiş metin.\n")
        XCTAssertEqual(document.at(0.5),"Düzeltilmiş metin.")
        XCTAssertEqual(document.at(1.5),"")
        XCTAssertEqual(document.srt,"1\n00:00:00,250 --> 00:00:01,500\nDüzeltilmiş metin.\n")
    }
    func testRejectsInvalidTimingsAndEmptyRecognition() {
        for source in ["","1\n00:00:03,000 --> 00:00:02,000\nWrong\n","1\n00:00:03,000 --> 00:00:05,000\nOutside\n"] {
            XCTAssertThrowsError(try SubtitleTranscript(srt:source,duration:4))
        }
        XCTAssertThrowsError(try SubtitleTranscript(cues:[],duration:4))
    }
}
