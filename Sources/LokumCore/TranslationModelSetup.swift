import Foundation
import CryptoKit

public enum TranslationModelSetup {
    public static let directory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask).first!.appendingPathComponent("Lokum/Translation/m2m100-v1",isDirectory:true)
    public static var ready:Bool {complete(directory)}
    private static func complete(_ root:URL)->Bool {["model/model.bin","model/shared_vocabulary.txt","sentencepiece.model"].allSatisfy{FileManager.default.fileExists(atPath:root.appendingPathComponent($0).path)}}
    public static func install(token:CancellationToken,progress:@escaping @Sendable (Double?,String)->Void)async throws {
        try token.check();try Task.checkCancellation();if ready{return}
        let fm=FileManager.default,parent=directory.deletingLastPathComponent();try fm.createDirectory(at:parent,withIntermediateDirectories:true)
        let stage=parent.appendingPathComponent(".translation-\(UUID().uuidString)",isDirectory:true);try fm.createDirectory(at:stage,withIntermediateDirectories:false);defer{try? fm.removeItem(at:stage)}
        let archive=stage.appendingPathComponent("model.zip")
        try await CheckedToolDownload.fetch(URL(string:"https://data.argosopentech.com/argospm/v2/translate-fairseq_m2m_100_418M.argosmodel")!,to:archive,maximum:500*1024*1024) {progress($0,"Downloading the translation model (459 MB)…")}
        let handle=try FileHandle(forReadingFrom:archive);defer{try? handle.close()};var sha=SHA256()
        while let bytes=try handle.read(upToCount:1024*1024),!bytes.isEmpty {try token.check();sha.update(data:bytes)}
        guard sha.finalize().map({String(format:"%02x",$0)}).joined()=="8a63b8e9228f985d081dd166e336acde5f9083f1f4d7b39e14f7f2d57e5405b4" else {throw LokumError.failed("Upstream download checksum mismatch. Nothing was installed.")}
        let unpack=stage.appendingPathComponent("unpack",isDirectory:true);try fm.createDirectory(at:unpack,withIntermediateDirectories:false)
        try ArchiveCodec.extract(archive,into:unpack,token:token)
        let content=unpack.appendingPathComponent("translate-fairseq_m2m_100_418M",isDirectory:true)
        try token.check();try Task.checkCancellation();try ToolInstallation.publish(content,to:directory,complete:complete)
    }
}
