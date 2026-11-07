// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Lokum",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "LokumCore", targets: ["LokumCore"]),
        .executable(name: "Lokum", targets: ["LokumApp"]),
        .executable(name: "lokum-cli", targets: ["LokumCLI"])
    ],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.0")
    ],
    targets: [
        .target(name: "LokumCore", resources: [.copy("Resources")]),
        .target(name: "LokumUpdates", dependencies: [.product(name: "Sparkle", package: "Sparkle")], path: "Sources", exclude: ["LokumApp", "LokumCore", "LokumCLI", "UpdateCredentialBroker"], sources: ["LokumUpdates", "TangerineUpdates/UpdateCredentialBrokerProtocol.swift"]),
        .executableTarget(name: "LokumApp", dependencies: ["LokumCore", "LokumUpdates"], linkerSettings: [.unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"])]),
        .executableTarget(name: "LokumCLI", dependencies: ["LokumCore"]),
        .testTarget(name: "LokumCoreTests", dependencies: ["LokumCore"], resources: [.copy("Fixtures")]),
        .testTarget(name: "LokumUpdatesTests", dependencies: ["LokumUpdates"])
    ],
    swiftLanguageModes: [.v5]
)
