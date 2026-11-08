import Foundation
import whisper

public enum TranscriptionEngine {
    public static let languages:[(String,String)]=[("auto","Detect automatically"),("tr","Turkish"),("en","English"),("de","German"),("fr","French"),("es","Spanish"),("it","Italian"),("ar","Arabic"),("ru","Russian"),("ja","Japanese"),("ko","Korean"),("zh","Chinese")]
    public static func transcribe(_ input:URL,model:URL=SpeechModelSetup.model,language:String="auto",token:CancellationToken = .init(),progress:ProgressHandler={_,_ in})throws -> SubtitleTranscript {
        try token.check();guard languages.contains(where:{$0.0==language}) else {throw LokumError.invalidInput("Choose a supported spoken language.")}
        let info=try MediaEngine.inspect(input,token:token);guard info.audio != nil else {throw LokumError.invalidInput("This media has no audio track to transcribe.")}
        let fm=FileManager.default,directory=fm.temporaryDirectory.appendingPathComponent("Lokum-Subtitles-\(UUID().uuidString)",isDirectory:true)
        try fm.createDirectory(at:directory,withIntermediateDirectories:false);defer{try? fm.removeItem(at:directory)}
        let pcm=directory.appendingPathComponent("speech.f32");progress(.indeterminate,"Preparing audio…")
        try ProcessRunner.run(Dependencies.executable("ffmpeg"),["-nostdin","-v","error","-i",input.path,"-map","0:a:0","-vn","-ar","16000","-ac","1","-f","f32le",pcm.path],token:token)
        guard (try pcm.resourceValues(forKeys:[.fileSizeKey]).fileSize ?? 0)<=256*1024*1024 else {throw LokumError.invalidInput("This recording is too long. Cut it into shorter clips before generating subtitles.")}
        let data=try Data(contentsOf:pcm,options:.mappedIfSafe)
        let samples:[Float]=data.withUnsafeBytes {bytes in stride(from:0,to:bytes.count-3,by:4).map{Float(bitPattern:UInt32(littleEndian:bytes.loadUnaligned(fromByteOffset:$0,as:UInt32.self)))}}
        guard !samples.isEmpty else {throw LokumError.invalidInput("No speech was detected. Try choosing the spoken language.")}
        try token.check();progress(.indeterminate,"Transcribing speech on your computer…")
        var contextOptions=whisper_context_default_params();contextOptions.use_gpu=false
        guard let context=model.path.withCString({whisper_init_from_file_with_params($0,contextOptions)}) else {throw LokumError.failed("The speech model could not be opened. Download it again.")}
        defer{whisper_free(context)}
        var options=whisper_full_default_params(WHISPER_SAMPLING_GREEDY);options.n_threads=Int32(min(8,max(1,ProcessInfo.processInfo.activeProcessorCount/2)));options.translate=false;options.print_progress=false;options.print_realtime=false;options.print_timestamps=false
        options.max_len=42;options.split_on_word=true;options.token_timestamps=true
        options.abort_callback={pointer in guard let pointer else{return false};return Unmanaged<CancellationToken>.fromOpaque(pointer).takeUnretainedValue().isCancelled}
        options.abort_callback_user_data=Unmanaged.passUnretained(token).toOpaque()
        let result=language.withCString {name in options.language=name;return samples.withUnsafeBufferPointer {buffer in whisper_full(context,options,buffer.baseAddress,Int32(buffer.count))}}
        try token.check();guard result==0 else {throw LokumError.failed("Speech recognition failed. Try a shorter recording or choose the spoken language.")}
        var cues:[SubtitleTranscript.Cue]=[]
        for index in 0..<whisper_full_n_segments(context) {
            let start=whisper_full_get_segment_t0(context,index)*10,end=min(whisper_full_get_segment_t1(context,index)*10,Int64(info.duration*1000))
            let text=String(cString:whisper_full_get_segment_text(context,index)).trimmingCharacters(in:.whitespacesAndNewlines)
            if !text.isEmpty,start<end {cues.append(.init(start:start,end:end,text:text))}
        }
        return try SubtitleTranscript(cues:cues,duration:info.duration)
    }
    public static func export(_ transcript:SubtitleTranscript,input:URL,format:String,destination:URL,token:CancellationToken = .init())throws -> [URL] {
        guard ["srt","txt","vtt","mp4"].contains(format) else {throw LokumError.invalidInput("Choose SRT, TXT, VTT or MP4.")}
        if format=="mp4",FileCategory.detect(input) != .video {throw LokumError.invalidInput("Choose a video to embed subtitles.")}
        let tx=ExportTransaction();defer{tx.cleanUp()};let area=try tx.makeArea(destination:destination)
        let output=area.directory.appendingPathComponent(input.deletingPathExtension().lastPathComponent+"-subtitles."+format)
        if format=="mp4" {
            try transcript.srt.write(to:area.directory.appendingPathComponent("captions.srt"),atomically:true,encoding:.utf8)
            try ProcessRunner.run(Dependencies.executable("ffmpeg"),["-nostdin","-v","error","-i",input.path,"-map","0:v:0","-map","0:a:0?","-vf","subtitles=filename=captions.srt","-c:v","libx264","-preset","medium","-crf","20","-c:a","aac","-movflags","+faststart",output.path],directory:area.directory,token:token)
        } else if format=="vtt" {
            let source=area.directory.appendingPathComponent("captions.srt");try transcript.srt.write(to:source,atomically:true,encoding:.utf8)
            try SubtitleCodec.convert(source,to:"vtt",token:token).write(to:output,atomically:true,encoding:.utf8)
        } else {try (format=="txt" ? transcript.text:transcript.srt).write(to:output,atomically:true,encoding:.utf8)}
        try token.check();try tx.record([output],in:area);return try tx.publish(token:token)
    }
}
