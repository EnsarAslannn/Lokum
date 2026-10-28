import Foundation

/// Lokum owns an independent update identity. Repository selection is explicit;
/// no legacy app's repository or saved credential account is remapped or reused.
enum UpdateBrandMigration {
    static func canonical(_ repository:GitHubRepository)->GitHubRepository { repository }
    static func credentialAccount(_ repository:GitHubRepository)->GitHubRepository { repository }
    static func acceptsArchive(owner:String,name:String,for repository:GitHubRepository)->Bool {
        let slug=owner.lowercased()+"/"+name.lowercased()
        return slug==repository.slug
    }
    /// A missing release is not permission to contact a different publisher.
    static func latestFeed(repository:GitHubRepository,token:String?,configuration:URLSessionConfiguration = .ephemeral) async throws -> (GitHubUpdateClient,Data) {
        let client=GitHubUpdateClient(repository:repository,token:token,configuration:configuration)
        return (client,try await client.fetchLatestAppcast())
    }
}
