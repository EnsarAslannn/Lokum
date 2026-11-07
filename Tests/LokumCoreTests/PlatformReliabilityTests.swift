import XCTest
@testable import LokumCore

final class PlatformReliabilityTests:XCTestCase {
    private func directory()throws->URL {
        let url=FileManager.default.temporaryDirectory.appendingPathComponent("Lokum file parity "+UUID().uuidString,isDirectory:true)
        try FileManager.default.createDirectory(at:url,withIntermediateDirectories:true);return url
    }
    func testMovePublishesAllCopiesBeforeRemovingOriginalsAndKeepsExistingFiles()throws {
        let root=try directory();defer{try? FileManager.default.removeItem(at:root)}
        let source=root.appendingPathComponent("Source"),target=root.appendingPathComponent("Target")
        try FileManager.default.createDirectory(at:source,withIntermediateDirectories:true);try FileManager.default.createDirectory(at:target,withIntermediateDirectories:true)
        let a=source.appendingPathComponent("A.txt"),b=source.appendingPathComponent("Folder")
        try Data("First".utf8).write(to:a);try FileManager.default.createDirectory(at:b,withIntermediateDirectories:true)
        try Data("Nested".utf8).write(to:b.appendingPathComponent("B.txt"));try Data("Existing".utf8).write(to:target.appendingPathComponent("A.txt"))
        let result=try FileTransferEngine.move([a,b],to:target)
        XCTAssertEqual(result.outputs.count,2)
        XCTAssertTrue(result.retainedOriginals.isEmpty)
        if result.outputs.count==2 {
            XCTAssertEqual(try String(contentsOf:result.outputs[0],encoding:.utf8),"First")
            XCTAssertEqual(try String(contentsOf:result.outputs[1].appendingPathComponent("B.txt"),encoding:.utf8),"Nested")
        }
        XCTAssertFalse(FileManager.default.fileExists(atPath:a.path));XCTAssertFalse(FileManager.default.fileExists(atPath:b.path))
        XCTAssertEqual(try String(contentsOf:target.appendingPathComponent("A.txt"),encoding:.utf8),"Existing")
    }
    func testMissingLaterMoveInputLeavesAllOriginalsAndNoPublishedFiles()throws {
        let root=try directory();defer{try? FileManager.default.removeItem(at:root)}
        let source=root.appendingPathComponent("Source.txt"),target=root.appendingPathComponent("Target")
        try Data("Keep".utf8).write(to:source)
        XCTAssertThrowsError(try FileTransferEngine.move([source,root.appendingPathComponent("Missing.txt")],to:target))
        XCTAssertEqual(try String(contentsOf:source,encoding:.utf8),"Keep")
        XCTAssertTrue((try? FileManager.default.contentsOfDirectory(atPath:target.path).isEmpty) ?? true)
    }
    func testMoveRejectsSymlinksAndMovingAFolderIntoItself()throws {
        let root=try directory();defer{try? FileManager.default.removeItem(at:root)}
        let source=root.appendingPathComponent("Source"),target=root.appendingPathComponent("Target")
        try FileManager.default.createDirectory(at:source,withIntermediateDirectories:true)
        let real=source.appendingPathComponent("Original.txt"),link=root.appendingPathComponent("Link.txt")
        try Data("Keep".utf8).write(to:real);try FileManager.default.createSymbolicLink(at:link,withDestinationURL:real)
        XCTAssertThrowsError(try FileTransferEngine.move([link],to:target))
        XCTAssertThrowsError(try FileTransferEngine.move([source],to:source.appendingPathComponent("Child")))
        XCTAssertEqual(try String(contentsOf:real,encoding:.utf8),"Keep")
    }
    func testCancelledMoveDoesNotPublishOrRemoveAnything()throws {
        let root=try directory();defer{try? FileManager.default.removeItem(at:root)}
        let source=root.appendingPathComponent("Source.txt"),target=root.appendingPathComponent("Target")
        try Data("Keep".utf8).write(to:source);let token=CancellationToken();token.cancel()
        XCTAssertThrowsError(try FileTransferEngine.move([source],to:target,token:token)) {error in
            guard case LokumError.cancelled=error else {return XCTFail("A cancelled move must report cancellation")}
        }
        XCTAssertEqual(try String(contentsOf:source,encoding:.utf8),"Keep")
        XCTAssertFalse(FileManager.default.fileExists(atPath:target.path))
    }
    func testMediaContextCommandsIdentifyOnlyActualMediaAddressActions() {
        for name in ["Copy Image Address","Copy Image Link","Resim adresini kopyala","Resim Bağlantısını Kopyala"] {XCTAssertEqual(MediaContextCommand.kind(name),"image",name)}
        for name in ["Copy Video Address","Copy Video URL","Video bağlantısını kopyala"] {XCTAssertEqual(MediaContextCommand.kind(name),"video",name)}
        for name in ["Copy link address","Copy image","Copy","Paste"] {XCTAssertNil(MediaContextCommand.kind(name),name)}
        XCTAssertEqual(MediaContextCommand.address("https://cdn.example/photo?signature=fixture",kind:"image")?.absoluteString,"https://cdn.example/photo?signature=fixture")
        XCTAssertNil(MediaContextCommand.address("javascript:alert(1)",kind:"image"));XCTAssertNil(MediaContextCommand.address("blob:https://example.com/id",kind:"video"))
    }
    func testOnlyKnownVideoPagesCanReplaceAnUnexposedBrowserVideoAddress() {
        XCTAssertEqual(MediaContextCommand.videoPage("https://www.youtube.com/shorts/X31goIHSY3c")?.absoluteString,"https://www.youtube.com/shorts/X31goIHSY3c")
        XCTAssertEqual(MediaContextCommand.videoPage("www.youtube.com/watch?v=aqz-KE-bpKQ")?.absoluteString,"https://www.youtube.com/watch?v=aqz-KE-bpKQ")
        for value in ["https://example.com/page","https://www.youtube.com/","https://youtube.com.attacker.example/watch?v=aqz-KE-bpKQ","https://www.instagram.com/p/PhotoOnly/"] {XCTAssertNil(MediaContextCommand.videoPage(value),value)}
    }
    func testArchivePreviewListsRealNamesWithoutExtractingFiles()throws {
        let root=try directory();defer{try? FileManager.default.removeItem(at:root)}
        let source=root.appendingPathComponent("Source"),archive=root.appendingPathComponent("Bundle.tar")
        try FileManager.default.createDirectory(at:source.appendingPathComponent("Folder"),withIntermediateDirectories:true)
        try Data("One".utf8).write(to:source.appendingPathComponent("A.txt"));try Data("Two".utf8).write(to:source.appendingPathComponent("Folder/B.txt"))
        try ProcessRunner.run(URL(fileURLWithPath:"/usr/bin/tar"),["-cf",archive.path,"-C",source.path,"A.txt","Folder/B.txt"])
        XCTAssertEqual(try DocumentEngine.archiveContents(archive),["A.txt","Folder/B.txt"])
        XCTAssertEqual(Set(try FileManager.default.contentsOfDirectory(atPath:root.path)),Set(["Source","Bundle.tar"]))
    }
}
