using System.Buffers.Binary;
using System.Text.Json;
namespace Lokum.Windows;

internal sealed record ChromeMediaRequest(int Version,string Operation,string? Kind=null,string? Url=null,string? PageUrl=null)
{
    internal ChromeMediaRequest Validate()
    {
        if(Version!=1||Operation is not ("ping" or "open"))throw new InvalidDataException("Unsupported Chrome media request.");
        if(Operation=="ping")return this;
        if(Kind is not ("image" or "video"))throw new InvalidDataException("Choose an image or video.");
        return this with{Url=Http(Url),PageUrl=Http(PageUrl)};
    }
    private static string Http(string? value)
    {
        if(value is null||value.Length>32768||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("Choose a public image or video address.");
        return uri.AbsoluteUri;
    }
    internal MediaToolSource Source()=>MediaToolSource.FromWeb(new(Url!,Kind!),Kind);
}
internal sealed record ChromeMediaReply(bool Ok,string? Error=null,string? Version=null);

internal static class ChromeMediaProtocol
{
    internal const string HostName="com.lokum.media";
    internal const string ExtensionId="djllfhjpfpmpcpfhnmnciacikoklceln";
    internal const string ExtensionOrigin="chrome-extension://"+ExtensionId+"/";
    internal const int MaximumBytes=131072;
    internal static string PipeName=>"Lokum.Chrome.v1."+System.Diagnostics.Process.GetCurrentProcess().SessionId;
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    internal static async Task<T> ReadAsync<T>(Stream stream,CancellationToken token)
    {
        var prefix=new byte[4];await stream.ReadExactlyAsync(prefix,token);var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if(length is <2 or >MaximumBytes)throw new InvalidDataException("Chrome media message is too large.");
        var body=new byte[length];await stream.ReadExactlyAsync(body,token);return JsonSerializer.Deserialize<T>(body,Json)??throw new InvalidDataException("Invalid Chrome media message.");
    }
    internal static async Task WriteAsync<T>(Stream stream,T value,CancellationToken token)
    {
        var body=JsonSerializer.SerializeToUtf8Bytes(value,Json);if(body.Length>MaximumBytes)throw new InvalidDataException("Chrome media message is too large.");
        var prefix=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(prefix,body.Length);await stream.WriteAsync(prefix,token);await stream.WriteAsync(body,token);await stream.FlushAsync(token);
    }
}
