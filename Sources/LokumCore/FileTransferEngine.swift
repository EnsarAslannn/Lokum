import Foundation

public struct FileMoveResult:Sendable {
    public let outputs:[URL]
    public let retainedOriginals:[URL]
}
public enum FileTransferEngine {
    public static func move(_ inputs:[URL],to destination:URL,token:CancellationToken = .init())throws->FileMoveResult {
        try token.check()
        guard !inputs.isEmpty,destination.isFileURL else {throw LokumError.invalidInput("Choose files and a local destination folder.")}
        let manager=FileManager.default
        var isDirectory:ObjCBool=false
        if manager.fileExists(atPath:destination.path,isDirectory:&isDirectory),!isDirectory.boolValue {throw LokumError.invalidInput("Choose a destination folder.")}
        var ancestor=destination.standardizedFileURL.resolvingSymlinksInPath()
        while !manager.fileExists(atPath:ancestor.path),ancestor.path != "/" {ancestor=ancestor.deletingLastPathComponent()}
        for input in inputs {
            try validate(input,depth:0,token:token)
            if try input.resourceValues(forKeys:[.isDirectoryKey]).isDirectory == true {
                var relationship=FileManager.URLRelationship.other
                try manager.getRelationship(&relationship,ofDirectoryAt:input.resolvingSymlinksInPath(),toItemAt:ancestor)
                guard relationship == .other else {throw LokumError.invalidInput("Choose a destination outside the folder being moved.")}
            }
        }
        let transaction=ExportTransaction();defer{transaction.cleanUp()}
        let area=try transaction.makeArea(destination:destination)
        var copies:[URL]=[]
        for input in inputs {
            try token.check()
            let copy=OutputFiles.unique(for:input,extension:input.pathExtension,directory:area.directory)
            try manager.copyItem(at:input,to:copy);copies.append(copy)
        }
        try token.check();try transaction.record(copies,in:area)
        let outputs=try transaction.publish(token:token)
        var retained:[URL]=[]
        for input in inputs where manager.fileExists(atPath:input.path) {
            do {try manager.removeItem(at:input)}catch{retained.append(input)}
        }
        return FileMoveResult(outputs:outputs,retainedOriginals:retained)
    }
    private static func validate(_ input:URL,depth:Int,token:CancellationToken)throws {
        try token.check()
        guard input.isFileURL,depth<=64 else {throw LokumError.invalidInput("Move regular files and folders without symbolic links.")}
        let values=try input.resourceValues(forKeys:[.isSymbolicLinkKey,.isRegularFileKey,.isDirectoryKey])
        guard values.isSymbolicLink != true,values.isRegularFile == true || values.isDirectory == true else {throw LokumError.invalidInput("Move regular files and folders without symbolic links.")}
        if values.isDirectory == true {
            for child in try FileManager.default.contentsOfDirectory(at:input,includingPropertiesForKeys:nil) {try validate(child,depth:depth+1,token:token)}
        }
    }
}
