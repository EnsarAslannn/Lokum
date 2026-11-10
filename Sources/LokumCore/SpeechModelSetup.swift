import Foundation
import CryptoKit

public enum SpeechModelSetup {
    public static let directory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask).first!.appendingPathComponent("Lokum/Speech/whisper-base",isDirectory:true)
    public static var model:URL {directory.appendingPathComponent("ggml-base.bin")}
    public static var ready:Bool {(try? model.resourceValues(forKeys:[.fileSizeKey]).fileSize)==147951465}
    public static func install(token:CancellationToken,progress:@escaping @Sendable (Double?,String)->Void)async throws {
        try token.check();try Task.checkCancellation();if ready{return}
        let fm=FileManager.default;try fm.createDirectory(at:directory,withIntermediateDirectories:true)
        let pending=directory.appendingPathComponent(".download-\(UUID().uuidString)");defer{try? fm.removeItem(at:pending)}
        try await CheckedToolDownload.fetch(URL(string:"https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.bin")!,to:pending,maximum:160*1024*1024) {progress($0,"Downloading the speech model (148 MB)…")}
        try token.check();try Task.checkCancellation();let handle=try FileHandle(forReadingFrom:pending);defer{try? handle.close()};var sha=SHA256()
        while let bytes=try handle.read(upToCount:1024*1024),!bytes.isEmpty {try token.check();sha.update(data:bytes)}
        guard sha.finalize().map({String(format:"%02x",$0)}).joined()=="60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe" else {throw LokumError.failed("Upstream download checksum mismatch. Nothing was installed.")}
        if fm.fileExists(atPath:model.path) {_ = try fm.replaceItemAt(model,withItemAt:pending)} else {try fm.moveItem(at:pending,to:model)}
    }
}
