import Foundation

/// The official anonymous channel is fixed in the signed bundle. A remote feed
/// cannot select a repository or ask this app to load a saved GitHub credential.
public struct PublicUpdateChannel:Equatable {
    public let repository:GitHubRepository
    public init?(info:[String:Any]) {
        guard let slug=info["LokumPublicUpdateRepository"] as? String,
              let repository=try? GitHubRepository(slug),
              info["SUFeedURL"] as? String == repository.websiteURL.appendingPathComponent("releases/latest/download/appcast.xml").absoluteString else { return nil }
        self.repository=repository
    }

    /// Switch only this app's saved copy of the same channel to anonymous mode.
    /// Credentials and preferences belonging to other applications stay untouched.
    public func migrate(_ defaults:UserDefaults) {
        let key="LokumUpdateGitHubRepository"
        guard let saved=defaults.string(forKey:key),
              saved.lowercased()==repository.slug else { return }
        for name in [key,"LokumUpdateGitHubRequiresToken","LokumUpdateGitHubSavedToken"] { defaults.removeObject(forKey:name) }
        if let automatic=defaults.object(forKey:"LokumUpdateGitHubAutomaticChecks") as? Bool {
            defaults.set(automatic,forKey:"SUEnableAutomaticChecks")
        }
        defaults.set(repository.slug,forKey:"LokumPublicChannelMigration")
    }
}
