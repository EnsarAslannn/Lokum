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
        let image=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:16,pixelsHigh:16,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:64,bitsPerPixel:32)!
        for y in 0..<16 {for x in 0..<16 {image.setColor(NSColor(srgbRed:x<8 ? 1:0,green:0,blue:x<8 ? 0:1,alpha:1).usingColorSpace(.deviceRGB)!,atX:x,y:y)}}
        try image.representation(using:.png,properties:[:])!.write(to:source)
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
        let report:[String:Any]=["passed":failures.isEmpty,"checks":checks,"failures":failures]
        try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:output.appendingPathComponent("verification.json"))
        if !failures.isEmpty {throw NSError(domain:"Lokum desktop feature parity",code:1,userInfo:[NSLocalizedDescriptionKey:failures.joined(separator:"\n")])}
        passed=true
    }
}
