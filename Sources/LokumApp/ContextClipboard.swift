import AppKit

@MainActor struct ContextClipboard {
    static func capture(_ board:NSPasteboard,maximumBytes:Int=32*1024*1024)->ContextClipboard? {nil}
    func restore(_ board:NSPasteboard,ifChangeCount:Int) {}
}
