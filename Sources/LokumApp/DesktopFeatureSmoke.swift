import AppKit
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
        let original=try Data(contentsOf:source)
        let conversion=ConversionWorkspaceModel(inputs:[source],format:"jpg")
        await conversion.prepare()
        check(conversion.error==nil && conversion.outputs.count==1 && conversion.totalBytes>0,"Show Result generates a real target file and reports its size")
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
        check(try Data(contentsOf:source)==original,"Conversion preview and save preserve the original input")
        let report:[String:Any]=["passed":failures.isEmpty,"checks":checks,"failures":failures]
        try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:output.appendingPathComponent("verification.json"))
        if !failures.isEmpty {throw NSError(domain:"Lokum desktop feature parity",code:1,userInfo:[NSLocalizedDescriptionKey:failures.joined(separator:"\n")])}
        passed=true
    }
}
