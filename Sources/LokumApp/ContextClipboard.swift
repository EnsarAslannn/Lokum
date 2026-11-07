import AppKit

@MainActor struct ContextClipboard {
    let changeCount:Int
    private let items:[[NSPasteboard.PasteboardType:Data]]
    static func capture(_ board:NSPasteboard,maximumBytes:Int=32*1024*1024)->ContextClipboard? {
        let count=board.changeCount,source=board.pasteboardItems ?? []
        guard maximumBytes>=0,source.count<=16 else{return nil}
        var result:[[NSPasteboard.PasteboardType:Data]]=[],total=0
        for item in source {
            guard item.types.count<=64 else{return nil}
            var values:[NSPasteboard.PasteboardType:Data]=[:]
            for type in item.types {
                guard let data=item.data(forType:type),data.count<=maximumBytes-total else{return nil}
                total+=data.count;values[type]=data
            }
            result.append(values)
        }
        guard board.changeCount==count else{return nil}
        return ContextClipboard(changeCount:count,items:result)
    }
    @discardableResult func restore(_ board:NSPasteboard,ifChangeCount:Int)->Bool {
        guard board.changeCount==ifChangeCount else{return false}
        var restored:[NSPasteboardItem]=[]
        for values in items {
            let item=NSPasteboardItem()
            for (type,data) in values {guard item.setData(data,forType:type) else{return false}}
            restored.append(item)
        }
        guard board.changeCount==ifChangeCount else{return false}
        board.clearContents()
        return restored.isEmpty || board.writeObjects(restored)
    }
}
