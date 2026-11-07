import Foundation

public enum MediaContextCommand {
    public static func kind(_ title:String)->String? {
        let value=title.folding(options:[.caseInsensitive,.diacriticInsensitive],locale:Locale(identifier:"en_US_POSIX")).lowercased().replacingOccurrences(of:"ı",with:"i").trimmingCharacters(in:.whitespacesAndNewlines)
        if ["copy image address","copy image link","resim adresini kopyala","resim baglantisini kopyala"].contains(value) {return "image"}
        if ["copy video address","copy video url","copy video link","video adresini kopyala","video baglantisini kopyala"].contains(value) {return "video"}
        return nil
    }
    public static func address(_ value:String,kind:String)->URL? {
        guard ["image","video"].contains(kind),value.utf8.count<=16384,let url=URL(string:value.trimmingCharacters(in:.whitespacesAndNewlines)),["http","https","file"].contains(url.scheme?.lowercased() ?? ""),url.user==nil,url.password==nil else {return nil}
        if url.isFileURL {return url}
        guard let host=url.host,!host.isEmpty else{return nil}
        return url
    }
    public static func videoPage(_ value:String)->URL? {
        let text=value.trimmingCharacters(in:.whitespacesAndNewlines)
        guard let url=address(text.contains("://") ? text:"https://"+text,kind:"video"),!url.isFileURL,let host=url.host?.lowercased() else{return nil}
        let parts=url.path.split(separator:"/").map(String.init)
        if ["youtube.com","www.youtube.com","m.youtube.com","music.youtube.com"].contains(host) {
            if parts.first=="watch",let id=URLComponents(url:url,resolvingAgainstBaseURL:false)?.queryItems?.first(where:{$0.name=="v"})?.value,validID(id) {return url}
            if parts.count==2,["shorts","embed","live"].contains(parts[0]),validID(parts[1]) {return url}
        }
        if host=="youtu.be",parts.count==1,validID(parts[0]) {return url}
        if ["instagram.com","www.instagram.com"].contains(host),parts.count==2,["reel","reels"].contains(parts[0]),!parts[1].isEmpty {return url}
        return nil
    }
    private static func validID(_ value:String)->Bool {value.range(of:"^[A-Za-z0-9_-]{11}$",options:.regularExpression) != nil}
}
