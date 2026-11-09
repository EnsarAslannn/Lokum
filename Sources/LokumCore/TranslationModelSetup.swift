import Foundation
import CryptoKit

public enum TranslationModelSetup {
    private struct Asset:Decodable {let url:String;let size:Int64;let sha256:String;let path:String?;let entries:[String:String]?}
    private struct Model:Decodable {let id:String;let required:[String];let assets:[Asset]}
    public static let directory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask).first!.appendingPathComponent("Lokum/Translation/language-pairs-v2",isDirectory:true)
    private static func models()throws -> [Model] {
        guard let url=CoreResources.bundle?.url(forResource:"SubtitleTranslationModels",withExtension:"json",subdirectory:"Resources") else {throw LokumError.missingDependency("Bundled translation model catalog")}
        return try JSONDecoder().decode([Model].self,from:Data(contentsOf:url))
    }
    public static func requiredPairs(source:String,target:String)throws -> [String] {
        try SubtitleWorkflow.validateLanguage(source);try SubtitleWorkflow.validateTargetLanguage(target)
        if source==target{return []}
        return (source=="en" ? []:[source+"-en"])+(target=="tr" ? ["en-tr"]:[])
    }
    private static func complete(_ root:URL,model:Model)->Bool {
        model.required.allSatisfy{let value=try? root.appendingPathComponent($0).resourceValues(forKeys:[.isRegularFileKey,.fileSizeKey]);return value?.isRegularFile==true && (value?.fileSize ?? 0)>0}
    }
    public static func install(source:String,target:String,token:CancellationToken,progress:@escaping @Sendable (Double?,String)->Void)async throws {
        try token.check();try Task.checkCancellation();let pairs=try requiredPairs(source:source,target:target);if pairs.isEmpty{return}
        let catalog=try models(),fm=FileManager.default;try fm.createDirectory(at:directory,withIntermediateDirectories:true)
        for id in pairs {
            guard let model=catalog.first(where:{$0.id==id}) else {throw LokumError.missingDependency("Bundled translation model catalog")}
            let destination=directory.appendingPathComponent(id,isDirectory:true);if complete(destination,model:model){continue}
            let stage=directory.appendingPathComponent(".translation-\(UUID().uuidString)",isDirectory:true);try fm.createDirectory(at:stage,withIntermediateDirectories:false);defer{try? fm.removeItem(at:stage)}
            let content=stage.appendingPathComponent("content",isDirectory:true);try fm.createDirectory(at:content,withIntermediateDirectories:false)
            for asset in model.assets {
                try token.check();try Task.checkCancellation();let download=stage.appendingPathComponent("download")
                try await CheckedToolDownload.fetch(URL(string:asset.url)!,to:download,maximum:asset.size) {progress($0,"Downloading the language-specific translation model…")}
                let handle=try FileHandle(forReadingFrom:download);defer{try? handle.close()};var sha=SHA256();var count:Int64=0
                while let bytes=try handle.read(upToCount:1024*1024),!bytes.isEmpty {try token.check();sha.update(data:bytes);count += Int64(bytes.count)}
                try handle.close()
                guard count==asset.size,sha.finalize().map({String(format:"%02x",$0)}).joined()==asset.sha256 else {throw LokumError.failed("Upstream download checksum mismatch. Nothing was installed.")}
                if let entries=asset.entries {
                    let unpack=stage.appendingPathComponent("unpack-\(UUID().uuidString)",isDirectory:true);try fm.createDirectory(at:unpack,withIntermediateDirectories:false)
                    try ArchiveCodec.extract(download,into:unpack,token:token)
                    for (entry,relative) in entries {
                        let output=content.appendingPathComponent(relative);try fm.createDirectory(at:output.deletingLastPathComponent(),withIntermediateDirectories:true)
                        try fm.copyItem(at:unpack.appendingPathComponent(entry),to:output)
                    }
                    try fm.removeItem(at:download)
                }else if let relative=asset.path {
                    let output=content.appendingPathComponent(relative);try fm.createDirectory(at:output.deletingLastPathComponent(),withIntermediateDirectories:true);try fm.moveItem(at:download,to:output)
                }
            }
            try token.check();try Task.checkCancellation();try ToolInstallation.publish(content,to:destination,complete:{complete($0,model:model)})
        }
    }
}
