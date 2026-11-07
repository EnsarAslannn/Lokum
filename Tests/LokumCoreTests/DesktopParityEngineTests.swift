import XCTest
import AppKit
@testable import LokumCore

final class DesktopParityEngineTests:XCTestCase {
    private func image(width:Int=8,height:Int=4,split:Bool=false)throws->CGImage {
        var bytes:[UInt8]=[]
        for _ in 0..<height {for x in 0..<width {bytes += split && x>=width/2 ? [0,0,255,255]:[255,0,0,255]}}
        return try XCTUnwrap(CGImage(width:width,height:height,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:width*4,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.premultipliedLast.rawValue),provider:CGDataProvider(data:Data(bytes) as CFData)!,decode:nil,shouldInterpolate:false,intent:.defaultIntent))
    }
    private func pixels(_ image:CGImage)throws->[UInt8] {
        let context=try XCTUnwrap(CGContext(data:nil,width:image.width,height:image.height,bitsPerComponent:8,bytesPerRow:image.width*4,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue))
        context.draw(image,in:CGRect(x:0,y:0,width:image.width,height:image.height))
        return Array(Data(bytes:try XCTUnwrap(context.data),count:image.width*image.height*4))
    }
    private func options(_ changes:[String:Any])throws->OperationOptions {
        var values=try JSONSerialization.jsonObject(with:JSONEncoder().encode(OperationOptions())) as! [String:Any]
        for (key,value) in changes {values[key]=value}
        return try JSONDecoder().decode(OperationOptions.self,from:JSONSerialization.data(withJSONObject:values))
    }
    private func directory()throws->URL {
        let url=FileManager.default.temporaryDirectory.appendingPathComponent("Lokum parity "+UUID().uuidString,isDirectory:true)
        try FileManager.default.createDirectory(at:url,withIntermediateDirectories:true)
        return url
    }
    private func video(in folder:URL)throws->URL {
        let url=folder.appendingPathComponent("Source tone.mp4")
        try ProcessRunner.run(Dependencies.executable("ffmpeg"),["-v","error","-nostdin","-f","lavfi","-i","testsrc2=size=64x48:rate=10:duration=4","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=4","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",url.path])
        return url
    }
    private func amplitude(_ input:URL,at time:Double)throws->Double {
        let result=try ProcessRunner.run(Dependencies.executable("ffmpeg"),["-v","error","-nostdin","-i",input.path,"-ss",String(time),"-t","0.2","-vn","-ac","1","-f","s16le","-"])
        let bytes=Array(result.output)
        XCTAssertGreaterThan(bytes.count,100)
        let values=stride(from:0,to:bytes.count-1,by:2).map{Double(Int16(bitPattern:UInt16(bytes[$0]) | UInt16(bytes[$0+1])<<8))/32768}
        return sqrt(values.reduce(0){$0+$1*$1}/Double(max(1,values.count)))
    }
    func testSelectedMuteKeepsAudioBeforeAndAfterTheSelectedInterval()throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder),original=try Data(contentsOf:input)
        var settings=try options(["muteSelectedRange":true,"start":1.0,"end":2.0])
        settings.outputDirectory=folder.appendingPathComponent("Output");settings.revealInFinder=false
        let result=try ConversionEngine.run(.init(inputs:[input],operation:.tool(.muteVideo),options:settings))[0]
        let info=try MediaEngine.inspect(result)
        XCTAssertTrue(info.hasAudio,"Muting one interval must retain the rest of the audio track")
        if info.hasAudio {
            XCTAssertGreaterThan(try amplitude(result,at:0.3),0.03)
            XCTAssertLessThan(try amplitude(result,at:1.3),0.001)
            XCTAssertGreaterThan(try amplitude(result,at:2.5),0.03)
        }
        XCTAssertEqual(try Data(contentsOf:input),original)
    }
    func testWholeVideoMuteStillRemovesTheAudioTrack()throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder)
        var settings=OperationOptions();settings.outputDirectory=folder.appendingPathComponent("Output")
        let result=try ConversionEngine.run(.init(inputs:[input],operation:.tool(.muteVideo),options:settings))[0]
        XCTAssertFalse(try MediaEngine.inspect(result).hasAudio)
    }
    func testIndependentClipRangesAllowGapsAndReverseSourceOrder()throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder)
        var settings=try options(["clipRanges":[["start":2.5,"end":3.5],["start":0.5,"end":1.25]]])
        settings.outputDirectory=folder.appendingPathComponent("Output")
        let results=try ConversionEngine.run(.init(inputs:[input],operation:.tool(.trimVideo),options:settings))
        XCTAssertEqual(results.count,2,"Each independently selected clip must produce its own file")
        if results.count==2 {
            XCTAssertEqual(try MediaEngine.inspect(results[0]).duration,1,accuracy:0.15)
            XCTAssertEqual(try MediaEngine.inspect(results[1]).duration,0.75,accuracy:0.15)
        }
    }
    func testInvalidLaterClipPublishesNoPartialResults()throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder),destination=folder.appendingPathComponent("Output")
        var settings=try options(["clipRanges":[["start":0.5,"end":1.0],["start":3.0,"end":9.0]]])
        settings.outputDirectory=destination
        XCTAssertThrowsError(try ConversionEngine.run(.init(inputs:[input],operation:.tool(.trimVideo),options:settings)))
        XCTAssertTrue((try? FileManager.default.contentsOfDirectory(atPath:destination.path).isEmpty) ?? true)
    }
    func testIndependentClipsCanBePreviewedTogetherWithoutTheGaps()async throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder)
        let settings=try options(["clipRanges":[["start":2.5,"end":3.5],["start":0.5,"end":1.25]],"joinSplitClips":true])
        let preview=try await MediaPreviewEngine.processedPreview(.trimVideo,input:input,options:settings)
        defer{preview.removeTemporaryFiles()}
        XCTAssertEqual(try MediaEngine.inspect(preview.playbackURL).duration,1.75,accuracy:0.15)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath:folder.path),[input.lastPathComponent])
    }
    func testAutomaticCompressionCanBePreviewedWithoutPublishingFiles()async throws {
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let input=try video(in:folder),original=try Data(contentsOf:input)
        do {
            let preview=try await MediaPreviewEngine.processedPreview(.compress,input:input)
            defer{preview.removeTemporaryFiles()}
            XCTAssertTrue(try MediaEngine.inspect(preview.playbackURL).hasVideo)
            XCTAssertNotNil(preview.ownedTemporaryDirectory)
            XCTAssertEqual(try Data(contentsOf:input),original)
            XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath:folder.path),[input.lastPathComponent])
        }catch{XCTFail("Automatic compression needs a real, owned preview before saving: \(error.localizedDescription)")}
    }
    func testEditingAnImageAppliesItsCropBeforeOtherAdjustments()throws {
        let image=try image(split:true)
        XCTAssertEqual(Array(try pixels(image).prefix(4)),[255,0,0,255])
        var settings=OperationOptions();settings.crop=EditRect(x:4,y:0,width:4,height:4)
        let result=try ImageEngine.preview(image,tool:.editImage,options:settings)
        XCTAssertEqual(result.width,4)
        XCTAssertEqual(result.height,4)
        let pixel=try pixels(result)
        XCTAssertGreaterThan(pixel[2],230)
        XCTAssertLessThan(pixel[0],25)
    }
    func testBackgroundSelectionExportsTransparentOrReplacementPixels()throws {
        guard let tool=ToolKind(rawValue:"removeBackground") else {XCTFail("The image background editor is missing on macOS");return}
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let source=folder.appendingPathComponent("Source.png")
        try ImageEngine.write(image(),to:source)
        let original=try Data(contentsOf:source)
        let region:[String:Any]=["points":[["x":0.0,"y":0.0],["x":0.5,"y":0.0],["x":0.5,"y":1.0],["x":0.0,"y":1.0]],"subtract":false]
        for fill in ["transparent","color"] {
            var settings=try options(["backgroundEditing":["regions":[region],"automatic":false,"fill":fill,"color":"#00FF00","selections":[:]]])
            settings.outputDirectory=folder.appendingPathComponent(fill)
            let result=try ConversionEngine.run(.init(inputs:[source],operation:.tool(tool),options:settings))[0]
            let actual=try pixels(ImageEngine.load(result))
            XCTAssertGreaterThan(actual[(1*8+1)*4],230)
            if fill=="transparent" {XCTAssertLessThan(actual[(1*8+6)*4+3],2)}
            else {XCTAssertGreaterThan(actual[(1*8+6)*4+1],230)}
        }
        XCTAssertEqual(try Data(contentsOf:source),original)
    }
    func testAutomaticBackgroundRemovalUsesTheBundledModelWithoutUploading()throws {
        guard let tool=ToolKind(rawValue:"removeBackground") else {XCTFail("Automatic background removal is missing on macOS");return}
        let folder=try directory();defer{try? FileManager.default.removeItem(at:folder)}
        let source=URL(fileURLWithPath:#filePath).deletingLastPathComponent().appendingPathComponent("Fixtures/astronaut.png")
        let original=try Data(contentsOf:source)
        var settings=try options(["backgroundEditing":["regions":[],"automatic":true,"fill":"transparent","color":"#FFFFFF","selections":[:]]])
        settings.outputDirectory=folder
        let result=try ConversionEngine.run(.init(inputs:[source],operation:.tool(tool),options:settings))[0]
        let image=NSBitmapImageRep(cgImage:try ImageEngine.load(result))
        var transparent=0,visible=0
        for y in stride(from:0,to:image.pixelsHigh,by:4) {for x in stride(from:0,to:image.pixelsWide,by:4) {
            let alpha=image.colorAt(x:x,y:y)!.alphaComponent
            if alpha<0.1 {transparent+=1};if alpha>0.9 {visible+=1}
        }}
        XCTAssertGreaterThan(transparent,100)
        XCTAssertGreaterThan(visible,100)
        XCTAssertEqual(try Data(contentsOf:source),original)
    }
}
