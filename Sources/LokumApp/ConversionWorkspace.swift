import SwiftUI
import AppKit
import LokumCore

@MainActor final class ConversionWorkspaceModel:ObservableObject {
    let inputs:[URL]
    let formats:[String]
    @Published var format:String {didSet{options=FormatPreferences.shared.options(for:format)}}
    @Published var options=OperationOptions() {didSet{invalidate()}}
    @Published private(set) var outputs:[URL]=[]
    @Published private(set) var totalBytes:Int64=0
    @Published private(set) var busy=false
    @Published private(set) var error:String?
    private var folder:URL?
    private var proxies:[MediaPreviewResult]=[]
    private var playback:[URL]=[]
    private var token=CancellationToken()
    private var generation=UUID()
    private let preview=FilePreview()
    init(inputs:[URL],format:String) {
        let available=FeatureCatalog.formats(for:inputs)
        self.inputs=inputs;formats=available
        self.format=available.contains(format) ? format:(available.first ?? "")
        options=FormatPreferences.shared.options(for:self.format)
    }
    func invalidate() {
        token.cancel();generation=UUID();busy=false;error=nil;preview.close()
        proxies.forEach{$0.removeTemporaryFiles()};proxies=[];playback=[];outputs=[];totalBytes=0
        if let folder {try? FileManager.default.removeItem(at:folder);self.folder=nil}
    }
    func prepare()async {
        guard !format.isEmpty,!busy else{return}
        invalidate();let revision=generation,cancel=CancellationToken();token=cancel;busy=true
        let directory=FileManager.default.temporaryDirectory.appendingPathComponent("Lokum-ConversionPreview-"+UUID().uuidString,isDirectory:true)
        var settings=options;settings.outputDirectory=directory;settings.revealInFinder=false
        let request=ConversionRequest(inputs:inputs,operation:.convert(format),options:settings)
        var prepared:[MediaPreviewResult]=[]
        do {
            try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
            let files=try await withTaskCancellationHandler(operation:{try await Task.detached{try withGrantedFolderAccess(request.inputs){try ConversionEngine.run(request,token:cancel)}}.value},onCancel:{cancel.cancel()})
            var playable:[URL]=[]
            for file in files {
                try cancel.check()
                if [.audio,.video].contains(FileCategory.detect(file)) {
                    let value=try await MediaPreviewEngine.prepare(file,token:cancel);prepared.append(value);playable.append(value.playbackURL)
                }else{playable.append(file)}
            }
            guard generation==revision,!cancel.isCancelled,!Task.isCancelled else {prepared.forEach{$0.removeTemporaryFiles()};try? FileManager.default.removeItem(at:directory);return}
            folder=directory;outputs=files;playback=playable;proxies=prepared
            totalBytes=try files.reduce(Int64(0)){$0+Int64(try $1.resourceValues(forKeys:[.fileSizeKey]).fileSize ?? 0)}
            busy=false
        }catch{
            prepared.forEach{$0.removeTemporaryFiles()};try? FileManager.default.removeItem(at:directory)
            if generation==revision,!cancel.isCancelled {self.error=error.localizedDescription;busy=false}
        }
    }
    func showResult() {if !playback.isEmpty {preview.show(playback,displayNames:outputs.map(\.lastPathComponent))}}
    func save(to destination:URL,state:AppState) {
        guard !outputs.isEmpty,!busy,!state.working else{return}
        let files=outputs,owned=folder;folder=nil
        state.publishPrepared(files,inputs:inputs,operation:.convert(format),to:destination,cleanup:owned)
        invalidate()
    }
}

struct ConversionWorkspace:View {
    @ObservedObject var state:AppState
    @StateObject private var model:ConversionWorkspaceModel
    @State private var ownerWindowID:ObjectIdentifier?
    init(state:AppState,format:String) {self.state=state;_model=StateObject(wrappedValue:ConversionWorkspaceModel(inputs:state.inputs,format:format))}
    var body:some View {
        VStack(spacing:0) {
            HStack(spacing:14) {
                WindowCloseButton().frame(width:26,height:26)
                VStack(alignment:.leading,spacing:4) {LText("Convert").font(.system(size:21,weight:.bold,design:.rounded));LText("Preview the result before saving a fresh copy.").font(.caption).foregroundStyle(.secondary)}
                Spacer()
            }.padding(20).frame(height:82).uiAuditProbe("editor.header")
            Divider()
            HStack(spacing:0) {
                VStack(alignment:.leading,spacing:16) {
                    LText("Selected files").font(.headline)
                    ScrollView {VStack(alignment:.leading,spacing:12) {ForEach(model.inputs,id:\.self) {url in Label(url.lastPathComponent,systemImage:"doc").lineLimit(2)}}}
                    if model.busy {ProgressView("Preparing your result…")}
                    if !model.outputs.isEmpty {
                        LText("Result").font(.headline)
                        Text("\(model.format.uppercased()) · \(model.outputs.count) · \(String(format:"%.1f",Double(model.totalBytes)/1000)) KB").font(.system(.body,design:.monospaced)).textSelection(.enabled)
                        Button("Open Preview") {model.showResult()}
                    }else{LText("Show Result creates a temporary preview. Nothing is saved to Library until you choose Save.").font(.caption).foregroundStyle(.secondary)}
                    if let error=model.error {LText(error).font(.caption).foregroundStyle(.orange).textSelection(.enabled)}
                }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading).uiAuditProbe("editor.preview")
                Divider()
                ScrollView {
                    VStack(alignment:.leading,spacing:18) {
                        Picker("Output format",selection:$model.format) {ForEach(model.formats,id:\.self) {Text($0.uppercased()).tag($0)}}
                        LText("Quality").font(.headline);Slider(value:$model.options.quality,in:0.1...1)
                        Toggle("Preserve metadata",isOn:$model.options.preserveMetadata)
                        if model.format=="epub" {Picker("Layout",selection:$model.options.epubLayout) {LText("Flexible, reflowable text").tag("reflowable");LText("Keep the original pages").tag("fixed")}}
                        if model.inputs.first.map({FileCategory.detect($0) == .pdf}) == true,["docx","pages","pptx","xlsx"].contains(model.format) {Picker("Layout",selection:$model.options.pdfOfficeLayout) {LText("Editable content").tag("editable");LText("Preserve page layout").tag("preserved")}}
                        if ["png","jpg","pdf"].contains(model.format) {Stepper("DPI: \(model.options.dpi)",value:$model.options.dpi,in:72...600,step:24)}
                        if model.format=="gif" {Stepper("Frame rate: \(model.options.gifFrameRate)",value:$model.options.gifFrameRate,in:1...60);Stepper("Width: \(model.options.gifMaxWidth)",value:$model.options.gifMaxWidth,in:160...1920,step:80)}
                    }.padding(24).disabled(state.working)
                }.frame(width:300).uiAuditProbe("editor.options")
            }.uiAuditProbe("editor.body")
            if state.working {JobProgressView(state:state).padding(.horizontal,20)}
            Divider()
            HStack {
                Button("Reset") {model.options=FormatPreferences.shared.options(for:model.format)}.buttonStyle(LokumButtonStyle(.quiet)).uiAuditProbe("editor.reset")
                Spacer()
                Button("Show Result") {Task {if model.outputs.isEmpty {await model.prepare()};model.showResult()}}.disabled(model.busy || state.working || model.formats.isEmpty)
                Button {
                    let panel=NSOpenPanel();panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true;panel.directoryURL=model.inputs.first?.deletingLastPathComponent();panel.prompt=L10n.text("Save Here")
                    if panel.runModal() == .OK,let directory=panel.url {model.save(to:directory,state:state)}
                } label:{LText("Save a fresh copy").uiAuditProbe("editor.primary.label")}.buttonStyle(LokumButtonStyle(.primary)).disabled(model.outputs.isEmpty || model.busy || state.working).uiAuditProbe("editor.primary")
            }.padding(20).uiAuditProbe("editor.footer")
        }.frame(minWidth:900,minHeight:420).background(LokumPalette.canvas).tint(.lokum).buttonStyle(LokumButtonStyle(.secondary,compact:true)).uiAuditStatus("ready")
            .background(WindowIdentityReader {ownerWindowID=$0})
            .onReceive(NotificationCenter.default.publisher(for:NSWindow.willCloseNotification)) {notification in if let window=notification.object as? NSWindow,ownerWindowID==ObjectIdentifier(window) {model.invalidate()}}
    }
}
