import Foundation

@objc(TangerineUpdateCredentialBrokerProtocol)
public protocol UpdateCredentialBrokerProtocol {
    func handshake(withReply reply: @escaping (NSError?) -> Void)
    func readToken(repository: String, allowInteraction: Bool, withReply reply: @escaping (String?, NSError?) -> Void)
    func writeToken(_ token: String, repository: String, withReply reply: @escaping (NSError?) -> Void)
    func deleteToken(repository: String, withReply reply: @escaping (NSError?) -> Void)
}
