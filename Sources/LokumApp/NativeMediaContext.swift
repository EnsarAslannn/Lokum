import AppKit
import ApplicationServices
import LokumCore

@MainActor final class NativeMediaContextController:NSObject {
    private struct Click {let pid:pid_t;let point:NSPoint;let finder:Bool;let button:Int}
    private var monitor:Any?
    private var click:Click?
    private var task:Task<Void,Never>?
    private var revision=UUID()
    private var panel:NSPanel?
    private var selectedURL:URL?
    private var mode:WebDownloadMode = .video
    var onFiles:(([URL],NSPoint)->Void)?
    var onDownload:((URL?,WebDownloadMode)->Void)?
    private static let browsers:Set<String>=["com.google.Chrome","com.google.Chrome.beta","com.microsoft.edgemac","com.apple.Safari","org.mozilla.firefox","com.brave.Browser","com.operasoftware.Opera","com.vivaldi.Vivaldi"]

    override init() {
        super.init()
        monitor=NSEvent.addGlobalMonitorForEvents(matching:[.rightMouseDown,.rightMouseUp,.leftMouseDown,.leftMouseUp,.rightMouseDragged,.leftMouseDragged,.otherMouseDown,.keyDown]) { [weak self] event in
            Task { @MainActor in self?.receive(event) }
        }
    }
    func stop() {if let monitor {NSEvent.removeMonitor(monitor)};monitor=nil;task?.cancel();click=nil;hide()}
    private func hide() {panel?.orderOut(nil);panel=nil;selectedURL=nil}
    private func receive(_ event:NSEvent) {
        if event.type == .keyDown {task?.cancel();revision=UUID();click=nil;hide();return}
        let point=NSEvent.mouseLocation
        if event.type == .leftMouseDragged || event.type == .rightMouseDragged {
            if let click,hypot(point.x-click.point.x,point.y-click.point.y)>6 {self.click=nil;task?.cancel()}
            return
        }
        if event.type == .leftMouseDown || event.type == .rightMouseDown || event.type == .otherMouseDown {
            task?.cancel();revision=UUID();click=nil;hide()
            let flags=event.modifierFlags.intersection([.control,.command,.option,.shift])
            guard UserDefaults.standard.bool(forKey:"enabled"),flags == .control,event.type != .otherMouseDown,
                  let app=NSWorkspace.shared.frontmostApplication,let id=app.bundleIdentifier,
                  id=="com.apple.finder" || Self.browsers.contains(id) else{return}
            click=Click(pid:app.processIdentifier,point:point,finder:id=="com.apple.finder",button:event.buttonNumber)
        }else if event.type == .leftMouseUp || event.type == .rightMouseUp {
            guard let captured=click,captured.button==event.buttonNumber,hypot(point.x-captured.point.x,point.y-captured.point.y)<=6 else{return}
            click=nil;let generation=revision
            task=Task { [weak self] in
                try? await Task.sleep(nanoseconds:60_000_000)
                guard let self,self.valid(captured,generation:generation) else{return}
                await self.resolve(captured,generation:generation)
            }
        }
    }
    private func valid(_ click:Click,generation:UUID)->Bool {
        !Task.isCancelled && generation==revision && UserDefaults.standard.bool(forKey:"enabled") && NSWorkspace.shared.frontmostApplication?.processIdentifier==click.pid
    }
    private static func attribute(_ element:AXUIElement,_ name:String)->CFTypeRef? {
        AXUIElementSetMessagingTimeout(element,0.04)
        var result:CFTypeRef?
        return AXUIElementCopyAttributeValue(element,name as CFString,&result) == .success ? result:nil
    }
    private static func element(_ value:CFTypeRef?)->AXUIElement? {
        guard let value,CFGetTypeID(value)==AXUIElementGetTypeID() else{return nil}
        return (value as! AXUIElement)
    }
    private static func children(_ element:AXUIElement)->[AXUIElement] {attribute(element,kAXChildrenAttribute) as? [AXUIElement] ?? []}
    private static func contextMenus(_ app:AXUIElement,at point:NSPoint)->[AXUIElement] {
        var owner:pid_t=0;guard AXUIElementGetPid(app,&owner) == .success else{return []}
        var seeds=[app]
        if let focus=element(attribute(app,kAXFocusedUIElementAttribute)) {seeds.append(focus)}
        let system=AXUIElementCreateSystemWide(),p=WindowManagementController.accessibilityPoint(point)
        AXUIElementSetMessagingTimeout(system,0.04);var hit:AXUIElement?
        if AXUIElementCopyElementAtPosition(system,Float(p.x),Float(p.y),&hit) == .success,let hit {
            var current:AXUIElement?=hit
            for _ in 0..<8 {guard let node=current else{break};seeds.append(node);current=element(attribute(node,kAXParentAttribute))}
        }
        var queue=seeds.map{($0,0)},visited=Set<CFHashCode>(),menus:[AXUIElement]=[],nodes=0
        let deadline=Date().addingTimeInterval(0.5)
        while !queue.isEmpty,nodes<600,Date()<deadline {
            let (node,depth)=queue.removeFirst();nodes+=1
            guard depth<=10,visited.insert(CFHash(node)).inserted else{continue}
            var pid:pid_t=0;guard AXUIElementGetPid(node,&pid) == .success,pid==owner else{continue}
            let role=attribute(node,kAXRoleAttribute) as? String ?? ""
            if role==kAXMenuBarRole || [kAXTextFieldRole,kAXTextAreaRole,"AXSecureTextField","AXWebArea"].contains(role) {continue}
            if role==kAXMenuRole {menus.append(node);continue}
            if let shown=element(attribute(node,"AXShownMenuUIElement")) {queue.append((shown,depth+1))}
            queue.append(contentsOf:children(node).map{($0,depth+1)})
        }
        return menus
    }
    private static func menuItems(_ menu:AXUIElement)->[AXUIElement] {
        children(menu).filter{attribute($0,kAXRoleAttribute) as? String == kAXMenuItemRole && attribute($0,kAXEnabledAttribute) as? Bool != false}
    }
    private static func page(_ app:AXUIElement)->URL? {
        guard let window=element(attribute(app,kAXFocusedWindowAttribute)) else{return nil}
        if let doc=attribute(window,kAXDocumentAttribute) as? String,let url=MediaContextCommand.videoPage(doc) {return url}
        var queue=[(window,0)],visited=Set<CFHashCode>(),nodes=0
        let deadline=Date().addingTimeInterval(0.25)
        while !queue.isEmpty,nodes<250,Date()<deadline {
            let (node,depth)=queue.removeFirst();nodes+=1
            guard depth<=12,visited.insert(CFHash(node)).inserted else{continue}
            let role=attribute(node,kAXRoleAttribute) as? String ?? ""
            if [kAXTextFieldRole,kAXTextAreaRole,"AXSecureTextField",kAXMenuBarRole].contains(role) {continue}
            if role=="AXWebArea",let text=attribute(node,"AXURL") as? String,let url=MediaContextCommand.videoPage(text) {return url}
            queue.append(contentsOf:children(node).map{($0,depth+1)})
        }
        return nil
    }
    private func resolve(_ click:Click,generation:UUID)async {
        guard AXIsProcessTrusted() else {if !click.finder {show(url:nil,kind:"video",at:click.point,permission:true)};return}
        let app=AXUIElementCreateApplication(click.pid)
        let menus=Self.contextMenus(app,at:click.point)
        guard valid(click,generation:generation) else{return}
        var command:AXUIElement?,kind="video"
        for menu in menus {
            for item in Self.menuItems(menu) {
                let title=Self.attribute(item,kAXTitleAttribute) as? String ?? ""
                if click.finder {
                    let name=title.folding(options:[.caseInsensitive,.diacriticInsensitive],locale:Locale(identifier:"en_US_POSIX")).lowercased()
                    if name=="copy" || name=="kopyala" || name.hasPrefix("copy ") || name.hasSuffix(" kopyala") {command=item;break}
                }else if let media=MediaContextCommand.kind(title) {command=item;kind=media;break}
            }
            if command != nil {break}
        }
        var url:URL?,files:[URL]=[]
        let board=NSPasteboard.general
        if let command,let snapshot=ContextClipboard.capture(board),snapshot.changeCount==board.changeCount,valid(click,generation:generation) {
            _=AXUIElementPerformAction(command,kAXPressAction as CFString)
            let deadline=Date().addingTimeInterval(0.65)
            while board.changeCount==snapshot.changeCount,Date()<deadline,!Task.isCancelled {try? await Task.sleep(nanoseconds:20_000_000)}
            if board.changeCount != snapshot.changeCount {
                let copiedCount=board.changeCount
                if valid(click,generation:generation) {
                    if click.finder {files=(board.readObjects(forClasses:[NSURL.self],options:[.urlReadingFileURLsOnly:true]) as? [URL] ?? []).filter{FileManager.default.fileExists(atPath:$0.path)}}
                    else if let value=board.string(forType:.string) {url=MediaContextCommand.address(value,kind:kind)}
                }
                if valid(click,generation:generation) {snapshot.restore(board,ifChangeCount:copiedCount)}
            }
        }
        guard valid(click,generation:generation) else{return}
        if !click.finder,kind=="video",url==nil {url=Self.page(app)}
        for menu in menus {_=AXUIElementPerformAction(menu,kAXCancelAction as CFString)}
        guard valid(click,generation:generation) else{return}
        if click.finder {if !files.isEmpty,files.count<=256 {onFiles?(files,click.point)}}
        else if let url,url.isFileURL,FileManager.default.fileExists(atPath:url.path) {onFiles?([url],click.point)}
        else {show(url:url,kind:kind,at:click.point,permission:false)}
    }
    private func show(url:URL?,kind:String,at point:NSPoint,permission:Bool) {
        hide();selectedURL=url;mode=kind=="image" ? .original:.video
        let panel=NSPanel(contentRect:NSRect(x:0,y:0,width:290,height:permission ? 122:64),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false)
        panel.isReleasedWhenClosed=false;panel.level = .statusBar;panel.hidesOnDeactivate=false;panel.hasShadow=true;panel.backgroundColor=NSColor(LokumPalette.canvas)
        panel.collectionBehavior=[.canJoinAllSpaces,.fullScreenAuxiliary]
        let stack=NSStackView();stack.orientation = .vertical;stack.alignment = .leading;stack.spacing=10;stack.translatesAutoresizingMaskIntoConstraints=false
        let button=NSButton(title:L10n.text("Download Media"),target:self,action:#selector(download));button.bezelStyle = .rounded;stack.addArrangedSubview(button)
        if permission {let hint=NSTextField(wrappingLabelWithString:L10n.text("Allow Accessibility to read the selected media address."));hint.font = .systemFont(ofSize:11);stack.addArrangedSubview(hint);let access=NSButton(title:L10n.text("Open permission settings…"),target:self,action:#selector(permissionSettings));access.bezelStyle = .rounded;stack.addArrangedSubview(access)}
        let root=NSView();panel.contentView=root;root.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo:root.leadingAnchor,constant:14),stack.trailingAnchor.constraint(equalTo:root.trailingAnchor,constant:-14),stack.centerYAnchor.constraint(equalTo:root.centerYAnchor)])
        let visible=NSScreen.screens.first(where:{$0.frame.contains(point)})?.visibleFrame ?? NSScreen.main?.visibleFrame ?? NSRect(x:0,y:0,width:1440,height:900)
        panel.setFrameOrigin(NSPoint(x:min(max(visible.minX,point.x+8),visible.maxX-panel.frame.width),y:min(max(visible.minY,point.y-panel.frame.height-8),visible.maxY-panel.frame.height)))
        panel.orderFrontRegardless();self.panel=panel
    }
    @objc private func download() {let url=selectedURL,mode=mode;hide();onDownload?(url,mode)}
    @objc private func permissionSettings() {hide();WindowManagementController.requestAccess()}
}
