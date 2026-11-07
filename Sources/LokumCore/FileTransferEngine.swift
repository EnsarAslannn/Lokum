import Foundation

public struct FileMoveResult:Sendable {
    public let outputs:[URL]
    public let retainedOriginals:[URL]
}
public enum FileTransferEngine {
    public static func move(_ inputs:[URL],to destination:URL,token:CancellationToken = .init())throws->FileMoveResult {
        return FileMoveResult(outputs:[],retainedOriginals:[])
    }
}
