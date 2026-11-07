import Foundation

enum UpdateBrandMigration {
    static func canonical(_ repository:GitHubRepository)->GitHubRepository { repository }
    static func credentialAccount(_ repository:GitHubRepository)->GitHubRepository { repository }
    static func acceptsArchive(owner:String,name:String,for repository:GitHubRepository)->Bool {
        let slug=owner.lowercased()+"/"+name.lowercased()
        return slug==repository.slug
    }
    static func latestFeed(repository:GitHubRepository,token:String?,configuration:URLSessionConfiguration = .ephemeral) async throws -> (GitHubUpdateClient,Data) {
        let client=GitHubUpdateClient(repository:repository,token:token,configuration:configuration)
        return (client,try await client.fetchLatestAppcast())
    }
}
