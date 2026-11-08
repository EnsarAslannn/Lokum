import XCTest
@testable import LokumCore

final class TranscriptionTests:XCTestCase {
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
