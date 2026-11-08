import Foundation

public struct SubtitleWorkflow:Sendable {
    public var duration:Double
    public private(set) var source:SubtitleTranscript?
    public private(set) var subtitles:SubtitleTranscript?
    public private(set) var sourceLanguage:String?
    public private(set) var targetLanguage:String?
    public private(set) var revision=0
    public init(duration:Double) {self.duration=duration}
    public var canTranslate:Bool {source != nil && sourceLanguage != nil && targetLanguage != nil}
    public var canSave:Bool {canTranslate && subtitles != nil}
    public static func validateLanguage(_ value:String)throws {
        guard value != "auto",TranscriptionEngine.languages.contains(where:{$0.0==value}) else {throw LokumError.invalidInput("Choose the video's source language and the subtitle language.")}
    }
    public mutating func setSource(_ text:String,language:String)throws {
        invalidateSource();try Self.validateLanguage(language);source=try SubtitleTranscript(srt:text,duration:duration);sourceLanguage=language
    }
    public mutating func chooseTarget(_ language:String)throws {try Self.validateLanguage(language);targetLanguage=language;subtitles=nil;revision += 1}
    public mutating func acceptTranslation(_ text:String,revision:Int,target:String)throws -> Bool {
        guard canTranslate,revision==self.revision,target==targetLanguage else{return false}
        subtitles=try SubtitleTranscript(srt:text,duration:duration);return true
    }
    public mutating func updateSubtitles(_ text:String)throws {
        guard canTranslate else {throw LokumError.invalidInput("Create and review the source transcript first.")}
        subtitles=nil;subtitles=try SubtitleTranscript(srt:text,duration:duration)
    }
    public mutating func useSourceLanguage()->Bool {guard canTranslate,sourceLanguage==targetLanguage else{return false};subtitles=source;return true}
    public mutating func invalidateSource() {source=nil;sourceLanguage=nil;subtitles=nil;revision += 1}
}
