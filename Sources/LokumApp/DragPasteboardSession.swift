import Foundation

struct DragPasteboardSession {
    private var baseline:Int?
    private var previous:Int?
    private var changedAt:TimeInterval = -.infinity
    private var wasDown=false
    mutating func containsFreshDrag(isDown:Bool,changeCount:Int,now:TimeInterval)->Bool {
        if baseline==nil { baseline=changeCount }
        if let previous,previous != changeCount { changedAt=now }
        defer { previous=changeCount;wasDown=isDown }
        if !isDown {
            if wasDown || now-changedAt>0.2 { baseline=changeCount }
            return false
        }
        return changeCount != baseline
    }
}
