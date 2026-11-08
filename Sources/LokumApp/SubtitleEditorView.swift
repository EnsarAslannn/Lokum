import SwiftUI
import AppKit
import AVKit
import LokumCore

@MainActor final class SubtitleEditorModel:ObservableObject {
    let input:URL
    let player:AVPlayer
    @Published var text="" {didSet{validate()}}
    @Published var language="auto"
    @Published var format="srt"
    @Published var busy=false
    @Published var label="Free and local. First use downloads a speech model (148 MB). Your media stays on your computer."
    @Published var fraction:Double?
    @Published var document:SubtitleTranscript?
    @Published var duration=0.0 {didSet{validate()}}
    @Published var error:String?
    @Published var resultPlaying=false
    private var token=CancellationToken()
    private var task:Task<Void,Never>?
    private var previewDirectory:URL?
    private let playbackToken=CancellationToken()
    private var originalPreview:MediaPreviewResult?
    private var closed=false
    init(input:URL) {self.input=input;player=AVPlayer(url:input)}
    private func validate() {
        guard !text.isEmpty else {document=nil;error=nil;return}
        do {document=try SubtitleTranscript(srt:text,duration:duration);error=nil}catch{document=nil;self.error=error.localizedDescription}
    }
    func inspect() {
        let input=input
        Task {
            do {
                duration=try await Task.detached {try MediaEngine.inspect(input).duration}.value
                let result=try await MediaPreviewEngine.prepare(input,token:playbackToken)
                if closed {result.removeTemporaryFiles()}else{originalPreview=result;if !resultPlaying {player.replaceCurrentItem(with:AVPlayerItem(url:result.playbackURL))}}
            }catch{if !closed {self.error=error.localizedDescription}}
        }
    }
    func generate() {
        guard !busy else{return};busy=true;error=nil;token=CancellationToken();let cancel=token,input=input,chosen=language
        task=Task {
            do {
                let info=try await Task.detached {try MediaEngine.inspect(input,token:cancel)}.value
                guard info.audio != nil else {throw LokumError.invalidInput("This media has no audio track to transcribe.")}
                duration=info.duration
                try await SpeechModelSetup.install(token:cancel) {amount,message in Task{@MainActor in if !self.closed {self.fraction=amount;self.label=message}}}
                let transcript=try await Task.detached(priority:.userInitiated) {try TranscriptionEngine.transcribe(input,language:chosen,token:cancel) {_,message in Task{@MainActor in if !self.closed {self.fraction=nil;self.label=message}}}}.value
                if !closed {text=transcript.srt;label="Subtitles are ready. Review the text and timing."}
            }catch{if !closed {self.error=cancel.isCancelled ? L10n.text("Cancelled. Originals kept."):error.localizedDescription}}
            busy=false
        }
    }
    func original() {player.pause();resultPlaying=false;player.replaceCurrentItem(with:AVPlayerItem(url:originalPreview?.playbackURL ?? input))}
    func showResult() {
        guard !busy,let document else{return}
        if format != "mp4" {original();player.play();return}
        prepare(document,save:nil)
    }
    func save(_ destination:URL,state:AppState) {guard !busy,let document else{return};prepare(document,save:(destination,state))}
    private func prepare(_ document:SubtitleTranscript,save:(URL,AppState)?) {
        busy=true;error=nil;original();token=CancellationToken();let cancel=token,input=input,format=save == nil ? "mp4":self.format
        let directory=FileManager.default.temporaryDirectory.appendingPathComponent("Lokum-SubtitlePreview-\(UUID().uuidString)",isDirectory:true)
        label="Preparing result preview…";fraction=nil
        task=Task {
            do {
                let files=try await Task.detached(priority:.userInitiated) {try TranscriptionEngine.export(document,input:input,format:format,destination:directory,token:cancel)}.value
                if closed {try? FileManager.default.removeItem(at:directory)}
                else if let (destination,state)=save {state.publishPrepared(files,inputs:[input],operation:.tool(.generateSubtitles),to:destination,cleanup:directory);label="Subtitles are ready. Review the text and timing."}
                else {cleanPreview();previewDirectory=directory;resultPlaying=true;player.replaceCurrentItem(with:AVPlayerItem(url:files[0]));player.play();label="Subtitles are ready. Review the text and timing."}
            }catch{try? FileManager.default.removeItem(at:directory);if !closed {self.error=cancel.isCancelled ? L10n.text("Cancelled. Originals kept."):error.localizedDescription}}
            busy=false
        }
    }
    func stop() {token.cancel();task?.cancel()}
    func close() {closed=true;stop();playbackToken.cancel();player.pause();player.replaceCurrentItem(with:nil);cleanPreview();originalPreview?.removeTemporaryFiles();originalPreview=nil}
    private func cleanPreview() {if let previewDirectory {try? FileManager.default.removeItem(at:previewDirectory);self.previewDirectory=nil}}
}

struct SubtitleEditorView:View {
    @ObservedObject var state:AppState
    @StateObject private var model:SubtitleEditorModel
    @State private var position=0.0
    @State private var ownerWindowID:ObjectIdentifier?
    private let clock=Timer.publish(every:0.1,on:.main,in:.common).autoconnect()
    init(state:AppState,input:URL) {self.state=state;_model=StateObject(wrappedValue:SubtitleEditorModel(input:input))}
    var body:some View {
        VStack(spacing:0) {
            HStack {WindowCloseButton().frame(width:26,height:26);VStack(alignment:.leading,spacing:4){LText("Generate Subtitles").font(.system(size:23,weight:.bold,design:.rounded));Text(model.input.lastPathComponent).font(.caption).foregroundStyle(.secondary)};Spacer()}.padding(22)
            Divider()
            HStack(alignment:.top,spacing:24) {
                VStack {
                    VideoPlayer(player:model.player).overlay(alignment:.bottom) {
                        if !model.resultPlaying,let caption=model.document?.at(position),!caption.isEmpty {Text(caption).font(.title3).multilineTextAlignment(.center).foregroundColor(.white).padding(10).background(.black.opacity(0.8)).padding(.horizontal,20).padding(.bottom,45).allowsHitTesting(false)}
                    }.frame(minHeight:260).cornerRadius(12)
                    Button("Original") {model.original()}
                }
                VStack(alignment:.leading,spacing:12) {
                    LText("Spoken language").font(.headline)
                    Picker("Spoken language",selection:$model.language) {ForEach(TranscriptionEngine.languages,id:\.0) {item in Text(L10n.text(item.1)).tag(item.0)}}.labelsHidden().disabled(model.busy)
                    HStack {Button("Generate subtitles",action:model.generate).disabled(model.busy);Button("Open SRT…") {let panel=NSOpenPanel();panel.allowedFileTypes=["srt"];panel.allowsMultipleSelection=false;if panel.runModal() == .OK,let url=panel.url {do{model.text=try String(contentsOf:url,encoding:.utf8)}catch{model.error=error.localizedDescription}}}.disabled(model.busy)}
                    TextEditor(text:$model.text).font(.system(.body,design:.monospaced)).frame(minHeight:250).disabled(model.busy).accessibilityIdentifier("subtitles.editor")
                    LText("Edit the text and timestamps below. Review automatic subtitles before sharing.").font(.caption).foregroundStyle(.secondary)
                }.frame(minWidth:300,maxWidth:420)
            }.padding(22)
            Spacer(minLength:0)
            VStack(alignment:.leading,spacing:10) {
                if model.busy {ProgressView(value:model.fraction)}
                Text(L10n.text(model.error ?? model.label)).font(.caption).foregroundStyle(model.error == nil ? Color.secondary:Color.red).textSelection(.enabled)
                HStack {
                    Button("Cancel") {if model.busy {model.stop()}else{model.close();NSApp.keyWindow?.close()}}
                    Spacer()
                    Picker("Output format",selection:$model.format) {Text("SRT").tag("srt");Text("TXT").tag("txt");Text("VTT").tag("vtt");if FileCategory.detect(model.input) == .video {Text("MP4").tag("mp4")}}.frame(width:180).disabled(model.busy).help(L10n.text("MP4 embeds subtitles permanently in a new video copy."))
                    Button("Show Result",action:model.showResult).disabled(model.busy || model.document == nil)
                    Button("Save new copy  ↗") {
                        let panel=NSOpenPanel();panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true;panel.directoryURL=model.input.deletingLastPathComponent();if panel.runModal() == .OK,let url=panel.url {model.save(url,state:state)}
                    }.buttonStyle(LokumButtonStyle(.primary)).disabled(model.busy || model.document == nil || state.working)
                }
            }.padding(22)
        }.background(LokumPalette.canvas).tint(.lokum).buttonStyle(LokumButtonStyle(.secondary,compact:true))
            .onAppear{model.inspect()}.onReceive(clock){_ in position=model.player.currentTime().seconds}
            .onChange(of:model.text){_ in if model.resultPlaying {model.original()}}
            .onChange(of:model.format){_ in if model.resultPlaying {model.original()}}
            .background(WindowIdentityReader{ownerWindowID=$0})
            .onReceive(NotificationCenter.default.publisher(for:NSWindow.willCloseNotification)){notice in if let window=notice.object as? NSWindow,ownerWindowID==ObjectIdentifier(window){model.close()}}
    }
}
