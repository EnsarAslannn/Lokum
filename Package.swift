// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Lokum",
    platforms: [.macOS("13.3")],
    products: [
        .library(name: "LokumCore", targets: ["LokumCore"]),
        .executable(name: "Lokum", targets: ["LokumApp"]),
        .executable(name: "lokum-cli", targets: ["LokumCLI"])
    ],
    dependencies: [
        .package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.0"),
        .package(url: "https://github.com/microsoft/onnxruntime-swift-package-manager", exact: "1.20.0")
    ],
    targets: [
        .binaryTarget(name:"whisper",url:"https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.2/whisper-v1.9.2-xcframework.zip",checksum:"af74fed13ea7f2d5ca2a39d9f58ec177713fafd7cab63aef4e27b79f3ceca80b"),
        .target(name: "LokumCore", dependencies: ["whisper",.product(name:"onnxruntime",package:"onnxruntime-swift-package-manager")], resources: [.copy("Resources")]),
        .target(name: "LokumUpdates", dependencies: [.product(name: "Sparkle", package: "Sparkle")], path: "Sources", exclude: ["LokumApp", "LokumCore", "LokumCLI", "UpdateCredentialBroker"], sources: ["LokumUpdates", "TangerineUpdates/UpdateCredentialBrokerProtocol.swift"]),
        .executableTarget(name: "LokumApp", dependencies: ["LokumCore", "LokumUpdates"], linkerSettings: [.unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks"])]),
        .executableTarget(name: "LokumCLI", dependencies: ["LokumCore"]),
        .testTarget(name: "LokumCoreTests", dependencies: ["LokumCore"], resources: [.copy("Fixtures")]),
        .testTarget(name: "LokumUpdatesTests", dependencies: ["LokumUpdates"])
    ],
    swiftLanguageModes: [.v5]
)
