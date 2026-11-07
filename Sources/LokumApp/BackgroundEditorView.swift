import SwiftUI
import AppKit
import LokumCore

struct BackgroundEditorView:View {
    @ObservedObject var state:AppState
    let inputs:[URL]
    @State private var index=0
    @State private var source:NSImage?
    @State private var result:NSImage?
    @State private var size=CGSize(width:1,height:1)
    @State private var selections:[String:[ForegroundRegion]]=[:]
    @State private var automatic=Set<String>()
    @State private var rectangle=false
    @State private var subtract=false
    @State private var fill="transparent"
    @State private var color=Color.white
    @State private var background:URL?
    @State private var busy=false
    @State private var error:String?
    @State private var token=CancellationToken()
    @State private var generation=UUID()
    @State private var ownerWindowID:ObjectIdentifier?
    var input:URL? {inputs.indices.contains(index) ? inputs[index]:nil}
    var regions:[ForegroundRegion] {input.flatMap{selections[$0.path]} ?? []}
    var settings:BackgroundEditingOptions {
        var value=BackgroundEditingOptions();value.selections=selections;value.automaticInputs=Array(automatic)
        value.fill=fill;value.color=ImageColorCodec.backgroundHex(NSColor(color)).prefix(7).description;value.image=background
        return value
    }
    var canSave:Bool {
        !busy && !state.working && !inputs.isEmpty && (fill != "image" || background != nil) && inputs.allSatisfy{automatic.contains($0.path) || (selections[$0.path] ?? []).contains{!$0.subtract}}
    }
    var previewKey:Data {
        var bytes=(try? JSONEncoder().encode(settings)) ?? Data();bytes.append(contentsOf:(input?.path ?? "").utf8);return bytes
    }
    var body:some View {
        VStack(spacing:0) {
            HStack(spacing:14) {
                WindowCloseButton().frame(width:26,height:26)
                VStack(alignment:.leading,spacing:4) {LText("Edit Background").font(.system(size:21,weight:.bold,design:.rounded));LText("Select what stays. Preview a fresh background.").font(.caption).foregroundStyle(.secondary)}
                Spacer()
                if inputs.count>1 {Picker("Image",selection:$index) {ForEach(inputs.indices,id:\.self) {Text(inputs[$0].lastPathComponent).tag($0)}}.frame(maxWidth:260)}
            }.padding(20).frame(height:82).uiAuditProbe("editor.header")
            Divider()
            HStack(spacing:0) {
                HStack(spacing:16) {
                    VStack(alignment:.leading,spacing:8) {
                        LText("Original").font(.caption.bold())
                        ForegroundSelectionCanvas(image:source,imageSize:size,regions:regions,rectangle:rectangle,subtract:subtract) {region in
                            guard let input else{return};selections[input.path,default:[]].append(region)
                        }.background(TransparencyGrid()).cornerRadius(14)
                    }
                    VStack(alignment:.leading,spacing:8) {
                        HStack {LText("Result").font(.caption.bold());Spacer();if busy {ProgressView().controlSize(.small)}}
                        ZStack {TransparencyGrid();if let result {Image(nsImage:result).resizable().scaledToFit()}else{LText("Select an area to keep").font(.caption).foregroundStyle(.secondary)}}.cornerRadius(14)
                    }
                }.padding(20).frame(maxWidth:.infinity,maxHeight:.infinity).uiAuditProbe("editor.preview")
                Divider()
                ScrollView {
                    VStack(alignment:.leading,spacing:16) {
                        LText("Foreground selection").font(.headline)
                        Picker("Selection",selection:$rectangle) {LText("Freehand selection").tag(false);LText("Rectangle selection").tag(true)}.pickerStyle(.segmented)
                        Toggle("Subtract from selection",isOn:$subtract)
                        HStack {
                            Button("Undo") {guard let input else{return};if !regions.isEmpty {selections[input.path]?.removeLast()}else{automatic.remove(input.path)}}.disabled(regions.isEmpty && !(input.map{automatic.contains($0.path)} ?? false))
                            Button("Clear") {guard let input else{return};selections[input.path]=[];automatic.remove(input.path)}
                        }
                        Button("Auto select") {if let input {automatic.insert(input.path)}}.disabled(source==nil || busy)
                        LText("Add areas to keep, or subtract holes. Each image keeps its own selection.").font(.caption).foregroundStyle(.secondary)
                        Divider()
                        Picker("Background",selection:$fill) {LText("Transparent").tag("transparent");LText("Solid color").tag("color");LText("Background image").tag("image")}
                        if fill=="color" {ColorPicker("Background color",selection:$color,supportsOpacity:false)}
                        if fill=="image" {
                            Button("Choose Background Image…") {let panel=NSOpenPanel();panel.allowsMultipleSelection=false;panel.canChooseDirectories=false;if panel.runModal() == .OK {background=panel.url}}
                            if let background {Text(background.lastPathComponent).font(.caption).lineLimit(2)}
                        }
                        if inputs.count>1 {LText("Background settings apply to every image.").font(.caption).foregroundStyle(.secondary)}
                        if let error {LText(error).font(.caption).foregroundStyle(.orange).textSelection(.enabled)}
                    }.padding(20)
                }.frame(width:285).uiAuditProbe("editor.options")
            }.uiAuditProbe("editor.body")
            if state.working {JobProgressView(state:state).padding(.horizontal,20)}
            Divider()
            HStack {
                Button("Reset") {selections=[:];automatic=[];fill="transparent";background=nil;result=nil;error=nil}.buttonStyle(LokumButtonStyle(.quiet)).uiAuditProbe("editor.reset")
                Spacer()
                LText("Original stays safe").font(.caption).foregroundStyle(.secondary)
                Button {
                    var options=OperationOptions();options.backgroundEditing=settings
                    state.execute(.tool(.removeBackground),options:options,inputs:inputs)
                } label:{LText("Save new PNG").uiAuditProbe("editor.primary.label")}.buttonStyle(LokumButtonStyle(.primary)).disabled(!canSave).uiAuditProbe("editor.primary")
            }.padding(20).uiAuditProbe("editor.footer")
        }.frame(minWidth:900,minHeight:420).background(LokumPalette.canvas).tint(.lokum).buttonStyle(LokumButtonStyle(.secondary,compact:true))
            .task(id:input?.path) {await load()}
            .task(id:previewKey) {await render()}
            .uiAuditStatus(source==nil ? "loading":"ready")
            .background(WindowIdentityReader {ownerWindowID=$0})
            .onReceive(NotificationCenter.default.publisher(for:NSWindow.willCloseNotification)) {notification in if let window=notification.object as? NSWindow,ownerWindowID==ObjectIdentifier(window) {token.cancel();generation=UUID()}}
    }
    private func load()async {
        guard let input else{return};source=nil;result=nil
        do {let image=try await Task.detached{try ImageEngine.load(input)}.value;try Task.checkCancellation();source=NSImage(cgImage:image,size:.zero);size=CGSize(width:image.width,height:image.height)}catch{if !Task.isCancelled {self.error=error.localizedDescription}}
    }
    private func render()async {
        token.cancel();let next=CancellationToken(),revision=UUID();token=next;generation=revision
        result=nil;error=nil;busy=false
        guard let input,automatic.contains(input.path) || regions.contains(where:{!$0.subtract}) else{return}
        if fill=="image",background==nil {return}
        let value=settings;busy=true
        do {
            try await Task.sleep(nanoseconds:100_000_000)
            let image=try await withTaskCancellationHandler(operation:{try await Task.detached{try BackgroundEditingEngine.render(input,settings:value,token:next)}.value},onCancel:{next.cancel()})
            guard generation==revision,!Task.isCancelled,!next.isCancelled else{return}
            result=NSImage(cgImage:image,size:.zero);busy=false
        }catch{if generation==revision,!next.isCancelled,!Task.isCancelled {self.error=error.localizedDescription;busy=false}}
    }
}

struct TransparencyGrid:View {
    var body:some View {Canvas {context,size in
        context.fill(Path(CGRect(origin:.zero,size:size)),with:.color(Color(nsColor:.controlBackgroundColor)))
        for y in stride(from:0.0,to:size.height,by:16) {for x in stride(from:0.0,to:size.width,by:16) where (Int(x/16)+Int(y/16))%2==0 {context.fill(Path(CGRect(x:x,y:y,width:16,height:16)),with:.color(.gray.opacity(0.2)))}}
    }.accessibilityHidden(true)}
}
struct ForegroundSelectionCanvas:View {
    let image:NSImage?
    let imageSize:CGSize
    let regions:[ForegroundRegion]
    let rectangle:Bool
    let subtract:Bool
    var onSelect:(ForegroundRegion)->Void
    @State private var points:[ImagePoint]=[]
    var body:some View {GeometryReader {geometry in
        let scale=min(geometry.size.width/max(1,imageSize.width),geometry.size.height/max(1,imageSize.height))
        let fitted=CGSize(width:imageSize.width*scale,height:imageSize.height*scale)
        let frame=CGRect(x:(geometry.size.width-fitted.width)/2,y:(geometry.size.height-fitted.height)/2,width:fitted.width,height:fitted.height)
        ZStack {
            if let image {Image(nsImage:image).resizable().scaledToFit().frame(width:geometry.size.width,height:geometry.size.height)}
            Canvas {context,_ in
                for region in regions+[ForegroundRegion(points:points,subtract:subtract)] where !region.points.isEmpty {
                    var path=Path();for (index,point) in region.points.enumerated() {let p=CGPoint(x:frame.minX+point.x*frame.width,y:frame.minY+point.y*frame.height);if index==0 {path.move(to:p)}else{path.addLine(to:p)}}
                    path.closeSubpath();context.fill(path,with:.color((region.subtract ? Color.red:Color.green).opacity(0.15)));context.stroke(path,with:.color(region.subtract ? .red:.green),lineWidth:2)
                }
            }.allowsHitTesting(false)
            Color.clear.contentShape(Rectangle()).gesture(DragGesture(minimumDistance:0).onChanged {event in
                guard image != nil,frame.width>0,frame.height>0,frame.contains(event.startLocation) else{return}
                func point(_ p:CGPoint)->ImagePoint {ImagePoint(x:min(1,max(0,(p.x-frame.minX)/frame.width)),y:min(1,max(0,(p.y-frame.minY)/frame.height)))}
                let start=point(event.startLocation),end=point(event.location)
                if rectangle {points=[start,ImagePoint(x:end.x,y:start.y),end,ImagePoint(x:start.x,y:end.y)]}
                else if points.isEmpty {points=[start,end]}else if points.count<4096 {points.append(end)}
            }.onEnded {_ in if points.count>=3 {onSelect(ForegroundRegion(points:points,subtract:subtract))};points=[]})
        }
    }.accessibilityLabel("Foreground selection preview")}
}
