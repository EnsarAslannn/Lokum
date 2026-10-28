import XCTest
@testable import LokumUpdates

final class PublicUpdateChannelTests:XCTestCase {
    private let info:[String:Any]=["LokumPublicUpdateRepository":"example/lokum","SUFeedURL":"https://github.com/example/lokum/releases/latest/download/appcast.xml"]
    private final class NoCredentials:UpdateCredentialStore,@unchecked Sendable {
        func token(for repository:GitHubRepository) throws->String { XCTFail("Anonymous migration read a private token");throw UpdateCredentialError.missing }
        func save(_ token:String,for repository:GitHubRepository) throws { XCTFail("Anonymous migration wrote a credential") }
        func remove(for repository:GitHubRepository) throws { XCTFail("Anonymous migration deleted a credential") }
    }
    func testOfficialChannelRequiresExactBundledFeed() {
        XCTAssertNotNil(PublicUpdateChannel(info:info))
        for address in ["http://github.com/example/lokum/releases/latest/download/appcast.xml","https://github.com/attacker/lokum-app/releases/latest/download/appcast.xml","https://github.com/example/lokum/releases/latest/download/appcast.xml?token=x"] {
            var altered=info;altered["SUFeedURL"]=address;XCTAssertNil(PublicUpdateChannel(info:altered))
        }
    }
    func testNewUsersAndThisAppsSavedChannelDoNotTouchCredentials() async {
        for previous in [nil,"example/lokum"] as [String?] {
            let suite="LokumPublicChannelTests.\(UUID())",defaults=UserDefaults(suiteName:suite)!
            defer { defaults.removePersistentDomain(forName:suite) }
            if let previous { defaults.set(previous,forKey:"LokumUpdateGitHubRepository");defaults.set(true,forKey:"LokumUpdateGitHubRequiresToken");defaults.set(true,forKey:"LokumUpdateGitHubSavedToken") }
            defaults.set("retained",forKey:"history");defaults.set(false,forKey:"LokumUpdateGitHubAutomaticChecks")
            await MainActor.run {
                let updater=AppUpdater(credentials:NoCredentials(),defaults:defaults,info:info)
                XCTAssertNil(updater.repository);XCTAssertFalse(updater.requiresToken);XCTAssertFalse(updater.hasSavedToken)
                XCTAssertEqual(updater.officialRepository?.slug,"example/lokum")
            }
            XCTAssertEqual(defaults.string(forKey:"history"),"retained")
            if previous != nil { XCTAssertEqual(defaults.object(forKey:"SUEnableAutomaticChecks") as? Bool,false) }
        }
    }
    func testUnrelatedCustomChannelIsPreserved() async {
        let suite="LokumPublicChannelTests.\(UUID())",defaults=UserDefaults(suiteName:suite)!
        defer { defaults.removePersistentDomain(forName:suite) }
        defaults.set("example/custom",forKey:"LokumUpdateGitHubRepository");defaults.set(true,forKey:"LokumUpdateGitHubRequiresToken")
        await MainActor.run {
            let updater=AppUpdater(credentials:NoCredentials(),defaults:defaults,info:info)
            XCTAssertEqual(updater.repository?.slug,"example/custom");XCTAssertTrue(updater.requiresToken)
        }
    }
}
