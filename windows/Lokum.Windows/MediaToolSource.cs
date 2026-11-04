using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using Lokum.Core;
namespace Lokum.Windows;

internal sealed record MediaToolSource(string[] Paths,WebMediaPayload? Web,string Kind)
{
    internal string[] CatalogPaths=>Paths.Length>0?Paths:[Kind switch{"image"=>"Image.png","audio"=>"Audio.mp3",_=>"Video.mp4"}];
    internal IReadOnlyList<ToolChoice> Choices=>FeatureCatalog.WheelTools(CatalogPaths).Select(id=>new ToolChoice(Web is null?id:$"web:{Kind}:{id}",FeatureCatalog.ToolNames[id],MainWindow.ToolSymbol(id))).ToArray();
    internal static MediaToolSource? Local(IEnumerable<string> paths)
    {
        var files=paths.Where(File.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(files.Length==0)return null;
        var kind=files.All(FeatureCatalog.IsImage)?"image":files.All(FeatureCatalog.IsVideo)?"video":files.All(FeatureCatalog.IsAudio)?"audio":null;
        return kind is null?null:new(files,null,kind);
    }
    internal static MediaToolSource FromWeb(WebMediaPayload payload,string? kind=null)
    {
        kind??=payload.Kind is "image" or "instagramPhotos"?"image":FeatureCatalog.IsAudio(new Uri(payload.Url).AbsolutePath)?"audio":"video";
        return new([],payload,kind);
    }
    internal static MediaToolSource? Address(string address,string kind)
    {
        if(address.Length>32768||kind is not ("image" or "video" or "audio")||!Uri.TryCreate(address,UriKind.Absolute,out var uri))return null;
        if(uri.IsFile)return Local([uri.LocalPath]);
        if(uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))return null;
        // Keep an image's actual address (including YouTube thumbnails). Page-link
        // canonicalization belongs to video downloaders, not image editing.
        return FromWeb(new(uri.AbsoluteUri,kind),kind);
    }
    internal static MediaToolSource? FromDrop(IDataObject data,string? hint=null)
    {
        if(data.GetData(DataFormats.FileDrop) is string[] paths&&Local(paths) is {} local)return local;
        var html=DragPayload.Text(data,DataFormats.Html,2_000_000);
        if(html is not null)
        {
            var match=Regex.Match(html,"<(img|video|audio)\\b[^>]*?\\bsrc\\s*=\\s*[\"']([^\"']+)[\"']",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
            if(match.Success&&Address(WebUtility.HtmlDecode(match.Groups[2].Value),match.Groups[1].Value.ToLowerInvariant() switch{"img"=>"image",var type=>type}) is {} embedded)return embedded;
        }
        var web=DragPayload.Parse(data,hint);return web is null?null:FromWeb(web);
    }
    internal static bool TryTool(string action,out string kind,out string tool)
    {
        var parts=action.Split(':',3);kind=parts.Length==3?parts[1]:"";tool=parts.Length==3?parts[2]:"";
        return parts.Length==3&&parts[0]=="web"&&kind is "image" or "video" or "audio"&&FeatureCatalog.WheelTools([kind switch{"image"=>"Image.png","audio"=>"Audio.mp3",_=>"Video.mp4"}]).Contains(tool);
    }
}
