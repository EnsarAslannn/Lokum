import AppKit
import SwiftUI
import AVKit
import LokumCore

@MainActor final class DesktopFeatureSmoke:NSObject,NSApplicationDelegate {
    let output:URL
    private var passed=false
    init(output:URL) {self.output=output}
    func applicationDidFinishLaunching(_ notification:Notification) {
        Task { @MainActor in
            do {try await run()} catch {try? FileManager.default.createDirectory(at:output,withIntermediateDirectories:true);try? Data(error.localizedDescription.utf8).write(to:output.appendingPathComponent("error.txt"))}
            NSApp.terminate(nil)
        }
    }
    func applicationWillTerminate(_ notification:Notification) {if !passed {exit(1)}}
    private func run()async throws {
        try FileManager.default.createDirectory(at:output,withIntermediateDirectories:true)
        let source=output.appendingPathComponent("Source.png")
        var pixels:[UInt8]=[]
        for _ in 0..<16 {for x in 0..<16 {pixels += x<8 ? [255,0,0,255]:[0,0,255,255]}}
        let image=CGImage(width:16,height:16,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:64,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.premultipliedLast.rawValue),provider:CGDataProvider(data:Data(pixels) as CFData)!,decode:nil,shouldInterpolate:false,intent:.defaultIntent)!
        try ImageEngine.write(image,to:source)
        let suite="Lokum.DesktopFeatureSmoke."+UUID().uuidString
        let defaults=UserDefaults(suiteName:suite)!
        let original=UserDefaults.standard.volatileDomain(forName:UserDefaults.argumentDomain)
        var arguments=original;arguments["wheelScale"]=1.0;arguments["sound"]=false;arguments["reduceAnimations"]=true;arguments["reveal"]=false
        UserDefaults.standard.setVolatileDomain(arguments,forName:UserDefaults.argumentDomain)
        defer{UserDefaults.standard.removePersistentDomain(forName:suite);UserDefaults.standard.setVolatileDomain(original,forName:UserDefaults.argumentDomain)}
        var checks:[String]=[],failures:[String]=[]
        func check(_ success:Bool,_ label:String) {if success {checks.append(label)}else{failures.append(label)}}
        let state=AppState(defaults:defaults);state.inputs=[source]
        var formats:[String]=[];state.onShowConversion={formats.append($0)}
        state.select(.convert("png"))
        check(formats==["png"] && !state.working && state.history.isEmpty && state.outputs.isEmpty,"Choosing a conversion opens a preview workspace without immediately exporting or changing Library")
        state.token.cancel()
        let mute=AppState(defaults:defaults);mute.inputs=[source.deletingPathExtension().appendingPathExtension("mp4")]
        var opened:[ToolKind]=[];mute.onShowTool={opened.append($0)}
        mute.select(.tool(.muteVideo))
        check(opened==[.muteVideo] && !mute.working,"Muting a video opens its settings and preview before saving")
        mute.token.cancel()
        let imageTools=FeatureCatalog.tools(for:[source])
        check(imageTools.contains(where:{$0.rawValue=="removeBackground"}) && !imageTools.contains(.frameImage),"Image selection exposes one combined background editor")
        check(imageTools.contains(.editImage) && !imageTools.contains(.cropImage),"Image cropping is part of Edit Image rather than a second wheel entry")
        let videoTools=FeatureCatalog.tools(for:mute.inputs)
        check(videoTools.contains(.trimVideo) && !videoTools.contains(.splitVideo) && !videoTools.contains(.videoSnapshots),"Video cutting includes independent clips without duplicate split or snapshot wheel entries")
        let wheelState=AppState(defaults:defaults);wheelState.inputs=[source]
        let controller=WheelController(state:wheelState)
        controller.show(inputs:[source],mode:.tools,at:NSPoint(x:400,y:400),keyboard:true)
        if let first=controller.wheel?.items.first,let operation=first.operation,case .convert=operation {
            check(true,"The tools wheel offers conversion before the editing tools")
        }else{check(false,"The tools wheel offers conversion before the editing tools")}
        controller.hide()
        let originalBytes=try Data(contentsOf:source)
        let conversion=ConversionWorkspaceModel(inputs:[source],format:"jpg")
        await conversion.prepare()
        check(conversion.error==nil && conversion.outputs.count==1 && conversion.totalBytes>0,"Show Result generates a real target file and reports its size; "+(conversion.error ?? "ready"))
        check(state.history.isEmpty && state.outputs.isEmpty,"Preparing a conversion leaves Library and saved outputs unchanged")
        if let file=conversion.outputs.first {
            let stale=file
            conversion.options.quality=0.45
            check(conversion.outputs.isEmpty && !FileManager.default.fileExists(atPath:stale.path),"Changing conversion settings invalidates and removes the previous preview")
            await conversion.prepare()
            if let prepared=conversion.outputs.first {
                let expected=try Data(contentsOf:prepared)
                conversion.save(to:output.appendingPathComponent("Exports"),state:state)
                let deadline=Date().addingTimeInterval(15)
                while state.working && Date()<deadline {try await Task.sleep(nanoseconds:20_000_000)}
                check(!state.working && state.error==nil && state.outputs.count==1 && state.history.count==1,"Saving the approved conversion publishes one Library entry")
                if let saved=state.outputs.first {check(try Data(contentsOf:saved)==expected,"The saved file is byte-for-byte identical to the approved preview")}
                check(!FileManager.default.fileExists(atPath:prepared.path),"Saving releases the owned preview files after publishing")
            }
        }
        conversion.invalidate()
        check(try Data(contentsOf:source)==originalBytes,"Conversion preview and save preserve the original input")
        let board=NSPasteboard(name:NSPasteboard.Name("Lokum.ContextClipboard."+UUID().uuidString))
        defer{board.releaseGlobally()}
        let first=NSPasteboardItem(),second=NSPasteboardItem(),custom=NSPasteboard.PasteboardType("local.lokum.fixture")
        first.setString("Original clipboard",forType:.string);first.setData(Data([0,2,255]),forType:custom)
        second.setString(source.absoluteString,forType:.fileURL)
        board.writeObjects([first,second])
        let count=board.changeCount
        if let snapshot=ContextClipboard.capture(board) {
            check(board.changeCount==count,"Reading a context clipboard snapshot leaves the user's clipboard unchanged")
            let menu=NSMenu(),copy=ContextCopyFixture(board:board)
            let item=NSMenuItem(title:"Copy Image Address",action:#selector(ContextCopyFixture.copyAddress),keyEquivalent:"");item.target=copy;menu.addItem(item)
            menu.performActionForItem(at:0)
            check(board.string(forType:.string)=="https://cdn.example/image.png?signed=fixture","A real native menu copy action produces the selected media address")
            snapshot.restore(board,ifChangeCount:board.changeCount)
            check(board.pasteboardItems?.count==2 && board.pasteboardItems?[0].string(forType:.string)=="Original clipboard" && board.pasteboardItems?[0].data(forType:custom)==Data([0,2,255]) && board.pasteboardItems?[1].string(forType:.fileURL)==source.absoluteString,"Context copy restores every original item and format")
            let staleCount=board.changeCount;board.clearContents();board.setString("New user content",forType:.string)
            snapshot.restore(board,ifChangeCount:staleCount)
            check(board.string(forType:.string)=="New user content","A concurrent clipboard change is never overwritten by context restoration")
            check(ContextClipboard.capture(board,maximumBytes:8)==nil,"An oversized clipboard is rejected before a context copy can replace it")
        }else{check(false,"Context copy can preserve the original multi-format clipboard")}
        let video=output.appendingPathComponent("Native video.mp4")
        try await Task.detached {try ProcessRunner.run(Dependencies.executable("ffmpeg"),["-y","-f","lavfi","-i","testsrc2=size=160x120:rate=10","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","4","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",video.path])}.value
        for tool:ToolKind in [.compress,.changeVideoSpeed,.muteVideo,.trimVideo,.cropVideo,.redactVideo] {
            let editorState=AppState(defaults:defaults);editorState.inputs=[video]
            let historyCount=editorState.history.count
            let recorder=UIAuditLayout()
            let window=ToolPanel(contentRect:NSRect(x:0,y:0,width:1080,height:760),styleMask:[.borderless,.closable],backing:.buffered,defer:false)
            window.isReleasedWhenClosed=false
            let content=NSHostingView(rootView:ToolEditor(state:editorState,tool:tool).uiAuditRoot(recorder));window.contentView=content;window.center();window.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)
            defer{window.close()}
            try await wait {
                content.layoutSubtreeIfNeeded()
                if recorder.status.hasPrefix("failed:") {return true}
                return recorder.status=="ready" && self.views(content).contains {view in
                    view is AVPlayerView && (view as? AVPlayerView)?.player?.currentItem != nil && view.bounds.width>1 && view.bounds.height>1
                }
            }
            guard recorder.status=="ready" else {throw NSError(domain:"Native media editor",code:1,userInfo:[NSLocalizedDescriptionKey:tool.title+": "+recorder.status])}
            if tool == .cropVideo || tool == .redactVideo {
                guard let player=views(content).compactMap({$0 as? AVPlayerView}).first else {throw NSError(domain:"Native media editor",code:2)}
                let frame=player.convert(player.bounds,to:nil)
                let start=NSPoint(x:frame.minX+frame.width*0.3,y:frame.minY+frame.height*0.3),end=NSPoint(x:frame.minX+frame.width*0.7,y:frame.minY+frame.height*0.7)
                for (type,point) in [(NSEvent.EventType.leftMouseDown,start),(.leftMouseDragged,end),(.leftMouseUp,end)] {
                    if let event=NSEvent.mouseEvent(with:type,location:point,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:window.windowNumber,context:nil,eventNumber:1,clickCount:1,pressure:1) {window.sendEvent(event)}
                }
                try await Task.sleep(nanoseconds:100_000_000)
            }
            let old=views(content).compactMap({$0 as? AVPlayerView}).first?.player?.currentItem
            try UIAuditInteraction.click("media.result.generate",recorder:recorder,content:content,window:window)
            try await wait {
                guard let item=self.views(content).compactMap({$0 as? AVPlayerView}).first?.player?.currentItem,let asset=item.asset as? AVURLAsset else{return false}
                return item !== old && asset.url != video
            }
            if let asset=views(content).compactMap({$0 as? AVPlayerView}).first?.player?.currentItem?.asset as? AVURLAsset {
                check(FileManager.default.fileExists(atPath:asset.url.path) && editorState.history.count==historyCount && editorState.outputs.isEmpty,tool.title+" plays an actual processed result before publishing any files")
                let size=try MediaEngine.inspect(asset.url)
                if tool == .cropVideo {check((size.video?.width ?? 160)<160 && (size.video?.height ?? 120)<120,"The native crop selection is reflected in the processed video's actual dimensions")}
                if tool == .muteVideo {check(size.audio==nil,"The native whole-video mute preview contains no audio track")}
                window.close();try await Task.sleep(nanoseconds:100_000_000)
                check(!FileManager.default.fileExists(atPath:asset.url.path),tool.title+" releases its owned processed preview when the window closes")
            }
        }
        let subtitles=SubtitleEditorModel(input:video)
        subtitles.duration=try MediaEngine.inspect(video).duration
        subtitles.text="1\n00:00:00,000 --> 00:00:02,000\nDeneme altyazısı.\n"
        check(subtitles.document?.at(1)=="Deneme altyazısı.","Native subtitle edits reach the synchronized preview document")
        subtitles.text="1\n00:00:02,000 --> 00:00:01,000\nInvalid\n"
        check(subtitles.document==nil && subtitles.error != nil,"Invalid native subtitle timing blocks result preview and export with an explanation")
        subtitles.text="1\n00:00:00,000 --> 00:00:02,000\nDeneme altyazısı.\n"
        subtitles.format="mp4";subtitles.showResult()
        try await wait{!subtitles.busy}
        check(subtitles.error==nil && subtitles.resultPlaying,"The native subtitle editor prepares an actual burned-subtitle video; "+(subtitles.error ?? "ready"))
        if let asset=subtitles.player.currentItem?.asset as? AVURLAsset,asset.url != video {
            let file=asset.url;let info=try MediaEngine.inspect(file)
            check(info.video != nil && info.audio != nil,"The native subtitle result retains both video and audio")
            subtitles.close()
            check(!FileManager.default.fileExists(atPath:file.path),"Closing the native subtitle editor removes its owned video preview")
        }else {subtitles.close();check(false,"The subtitle result was not loaded into the native player")}
        let report:[String:Any]=["passed":failures.isEmpty,"checks":checks,"failures":failures]
        try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:output.appendingPathComponent("verification.json"))
        if !failures.isEmpty {throw NSError(domain:"Lokum desktop feature parity",code:1,userInfo:[NSLocalizedDescriptionKey:failures.joined(separator:"\n")])}
        passed=true
    }
    private func wait(_ ready:()->Bool)async throws {
        let deadline=Date().addingTimeInterval(25)
        while !ready(),Date()<deadline {try await Task.sleep(nanoseconds:25_000_000)}
        if !ready() {throw NSError(domain:"Native editor readiness",code:1,userInfo:[NSLocalizedDescriptionKey:"The native editor did not reach the expected ready state."])}
    }
    private func views(_ view:NSView)->[NSView] {[view]+view.subviews.flatMap{views($0)}}
    private func elements(_ view:NSView)->[NSAccessibilityProtocol] {
        var queue:[Any]=[view],result:[NSAccessibilityProtocol]=[],visited=Set<ObjectIdentifier>()
        while !queue.isEmpty,result.count<2000 {
            guard let element=queue.removeFirst() as? NSAccessibilityProtocol,visited.insert(ObjectIdentifier(element as AnyObject)).inserted else{continue}
            result.append(element);queue.append(contentsOf:element.accessibilityChildren() ?? [])
        }
        return result
    }
}

@MainActor private final class ContextCopyFixture:NSObject {
    let board:NSPasteboard
    init(board:NSPasteboard) {self.board=board}
    @objc func copyAddress() {board.clearContents();board.setString("https://cdn.example/image.png?signed=fixture",forType:.string)}
}
