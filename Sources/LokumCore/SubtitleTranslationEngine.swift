import Foundation

public enum SubtitleTranslationEngine {
    private struct Cue:Codable {let start:Int64;let end:Int64;let text:String}
    private struct Request:Codable {let sourceLanguage:String;let targetLanguage:String;let cues:[Cue]}
    public static func executable()throws -> URL {
        if let path=ProcessInfo.processInfo.environment["LOKUM_TRANSLATE"],FileManager.default.isExecutableFile(atPath:path) {return URL(fileURLWithPath:path)}
        if let path=Bundle.main.resourceURL?.appendingPathComponent("Tools/Translate/translate"),FileManager.default.isExecutableFile(atPath:path.path) {return path}
        #if DEBUG
        let root=URL(fileURLWithPath:#filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        let path=root.appendingPathComponent("Tools/Translate/translate");if FileManager.default.isExecutableFile(atPath:path.path) {return path}
        #endif
        throw LokumError.missingDependency("The translation engine is missing. Extract the complete Lokum package again.")
    }
    public static func translate(_ source:SubtitleTranscript,duration:Double,sourceLanguage:String,targetLanguage:String,model:URL=TranslationModelSetup.directory,token:CancellationToken = .init(),progress:ProgressHandler={_,_ in})throws -> SubtitleTranscript {
        try token.check();try SubtitleWorkflow.validateLanguage(sourceLanguage);try SubtitleWorkflow.validateTargetLanguage(targetLanguage)
        if sourceLanguage==targetLanguage{return source}
        let fm=FileManager.default,folder=fm.temporaryDirectory.appendingPathComponent("Lokum-Translation-\(UUID().uuidString)",isDirectory:true);try fm.createDirectory(at:folder,withIntermediateDirectories:false);defer{try? fm.removeItem(at:folder)}
        let input=folder.appendingPathComponent("request.json"),output=folder.appendingPathComponent("response.json")
        try JSONEncoder().encode(Request(sourceLanguage:sourceLanguage,targetLanguage:targetLanguage,cues:source.cues.map{Cue(start:$0.start,end:$0.end,text:$0.text)})).write(to:input)
        progress(OperationProgress.indeterminate,"Translating the reviewed transcript on your computer…")
        try ProcessRunner.run(executable(),["--model",model.path,"--request",input.path,"--response",output.path],token:token)
        try token.check();let response=try JSONDecoder().decode(Request.self,from:Data(contentsOf:output))
        guard response.sourceLanguage==sourceLanguage,response.targetLanguage==targetLanguage else {throw LokumError.failed("The translation language does not match your selection.")}
        return try SubtitleTranscript(cues:response.cues.map{.init(start:$0.start,end:$0.end,text:$0.text)},duration:duration)
    }
}
