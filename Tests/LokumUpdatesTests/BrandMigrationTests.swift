import XCTest
@testable import LokumUpdates

final class BrandMigrationTests:XCTestCase {
    private final class UnusedCredentials:UpdateCredentialStore,@unchecked Sendable {
        var reads=0
        func token(for repository:GitHubRepository) throws->String { reads += 1;throw UpdateCredentialError.missing }
        func save(_ token:String,for repository:GitHubRepository) throws { XCTFail("A rename must not save or replace credentials") }
        func remove(for repository:GitHubRepository) throws { XCTFail("A rename must not delete credentials") }
    }
    func testLegacyAppSettingsAreNotImportedOrChanged() async throws {
        let suite="LokumBrandMigrationTests.\(UUID())"
        let defaults=UserDefaults(suiteName:suite)!
        defer { defaults.removePersistentDomain(forName:suite) }
        defaults.set("orkunisitmak/tangerine",forKey:"TangerineUpdateGitHubRepository")
        defaults.set(true,forKey:"TangerineUpdateGitHubSavedToken")
        defaults.set(true,forKey:"TangerineUpdateGitHubRequiresToken")
        defaults.set(false,forKey:"TangerineUpdateGitHubAutomaticChecks")
        defaults.set("preserved history",forKey:"history")
        let store=UnusedCredentials()
        let updater=await MainActor.run { AppUpdater(credentials:store,defaults:defaults) }
        await MainActor.run { XCTAssertNil(updater.repository);XCTAssertFalse(updater.hasSavedToken);XCTAssertNil(updater.officialRepository) }
        XCTAssertEqual(defaults.string(forKey:"TangerineUpdateGitHubRepository"),"orkunisitmak/tangerine")
        XCTAssertFalse(defaults.bool(forKey:"TangerineUpdateGitHubAutomaticChecks"));XCTAssertEqual(defaults.string(forKey:"history"),"preserved history");XCTAssertEqual(store.reads,0)
    }
    func testSelectedRepositoryAndCredentialAccountAreNeverRemapped() throws {
        for slug in ["example/lokum","example/private","orkunisitmak/nashi"] {
            let selected=try GitHubRepository(slug)
            XCTAssertEqual(UpdateBrandMigration.credentialAccount(selected),selected)
            XCTAssertEqual(UpdateBrandMigration.canonical(selected),selected)
        }
    }
    func testOriginalAppArchiveURLsAreNotAcceptedAsLokumUpdates() throws {
        let url=URL(string:"https://github.com/orkunisitmak/tangerine/releases/download/v0.6.0-24/Lokum-0.6.0-24.zip")!
        let current=GitHubUpdateClient(repository:try GitHubRepository("example/lokum"),token:nil)
        XCTAssertThrowsError(try current.archiveReference(for:url))
        XCTAssertEqual(try current.archiveReference(for:URL(string:"https://github.com/example/lokum/releases/download/v1/Lokum.zip")!),.release(tag:"v1",filename:"Lokum.zip"))
        for slug in ["other/lokum","orkunisitmak/unrelated"] {
            let unrelated=GitHubUpdateClient(repository:try GitHubRepository(slug),token:nil)
            XCTAssertThrowsError(try unrelated.archiveReference(for:url))
        }
        XCTAssertThrowsError(try current.archiveReference(for:URL(string:"https://github.com/other/tangerine/releases/download/v1/a.zip")!))
    }
}
