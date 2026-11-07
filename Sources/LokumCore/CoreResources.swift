import Foundation

enum CoreResources {
    static let bundle:Bundle? = {
        let main=Bundle.main
        if main.bundleURL.pathExtension.lowercased()=="app" {
            guard let resources=main.resourceURL else{return nil}
            return Bundle(url:resources.appendingPathComponent("Lokum_LokumCore.bundle",isDirectory:true))
        }
        return Bundle.module
    }()
}
