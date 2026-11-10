import Foundation

public struct SubtitleTranscript:Sendable {
    public struct Cue:Sendable,Equatable {
        public var start:Int64
        public var end:Int64
        public var text:String
        public init(start:Int64,end:Int64,text:String) {self.start=start;self.end=end;self.text=text}
    }
    public let cues:[Cue]
    public init(cues:[Cue],duration:Double)throws {
        guard duration.isFinite,duration>0 else {throw LokumError.invalidInput("Choose media with a valid duration.")}
        guard !cues.isEmpty else {throw LokumError.invalidInput("No speech was detected. Try choosing the spoken language.")}
        var previous:Int64 = -1
        for cue in cues {
            guard cue.start>=0,cue.end>cue.start,Double(cue.end)<=duration*1000+0.5,cue.start>=previous,!cue.text.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty else {throw LokumError.invalidInput("Each subtitle needs text and a valid start and end within the media duration.")}
            previous=cue.start
        }
        self.cues=cues
    }
    public init(srt:String,duration:Double)throws {try self.init(cues:SubtitleCodec.parse(srt).map{Cue(start:$0.start,end:$0.end,text:$0.text)},duration:duration)}
    private func stamp(_ n:Int64)->String {String(format:"%02lld:%02lld:%02lld,%03lld",n/3600000,n/60000%60,n/1000%60,n%1000)}
    public var srt:String {cues.enumerated().map{"\($0.offset+1)\n\(stamp($0.element.start)) --> \(stamp($0.element.end))\n\($0.element.text)"}.joined(separator:"\n\n")+"\n"}
    public var text:String {cues.map(\.text).joined(separator:"\n\n")+"\n"}
    public func at(_ seconds:Double)->String {cues.first{Double($0.start)<=seconds*1000 && Double($0.end)>seconds*1000}?.text ?? ""}
}
