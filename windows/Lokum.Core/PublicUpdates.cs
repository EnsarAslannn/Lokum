using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
namespace Lokum.Core;

public sealed record WindowsUpdate(string Version,string Url,long Length,string EdSignature);
public sealed record PublicUpdateChannel(string Repository,string PublicKey)
{
    public string Feed=>"https://github.com/"+Repository+"/releases/latest/download/windows-update.json";
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Repository)||!Regex.IsMatch(Repository,@"\A[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}\z")||Repository.Split('/')[1] is "." or "..")throw new InvalidDataException("The Lokum update repository must be a GitHub owner/repository.");
        try{if(string.IsNullOrWhiteSpace(PublicKey)||Convert.FromBase64String(PublicKey).Length!=32)throw new InvalidDataException("The Lokum update signing key must be a 32-byte Ed25519 public key.");_ = new Ed25519PublicKeyParameters(Convert.FromBase64String(PublicKey),0);}
        catch(FormatException error){throw new InvalidDataException("The Lokum update signing key is invalid.",error);}
        catch(ArgumentException error){throw new InvalidDataException("The Lokum update signing key is invalid.",error);}
    }
}
public static class PublicUpdates
{
    public static string ConfigurationPath=>Path.Combine(AppContext.BaseDirectory,"lokum-updates.json");
    public static PublicUpdateChannel? LoadChannel(string? path=null)
    {
        path??=ConfigurationPath;if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>65536)throw new InvalidDataException("The Lokum update configuration is too large.");
        PublicUpdateChannel channel;
        try{channel=JsonSerializer.Deserialize<PublicUpdateChannel>(File.ReadAllBytes(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("The Lokum update configuration is empty.");}
        catch(JsonException error){throw new InvalidDataException("The Lokum update configuration is invalid.",error);}
        channel.Validate();return channel;
    }
    private static PublicUpdateChannel RequireChannel()=>LoadChannel()??throw new InvalidOperationException("Lokum updates are not connected yet. A Lokum release channel and signing key are required.");
    public static Task<WindowsUpdate?> CheckAsync(Version current,CancellationToken token=default)=>CheckAsync(current,RequireChannel(),token);
    public static async Task<WindowsUpdate?> CheckAsync(Version current,PublicUpdateChannel channel,CancellationToken token=default)
    {
        channel.Validate();
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};client.DefaultRequestHeaders.UserAgent.ParseAdd("Lokum/"+current.ToString());
        using var response=await client.GetAsync(channel.Feed,HttpCompletionOption.ResponseHeadersRead,token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await stream.ReadAsync(buffer,token))>0){if(memory.Length+count>65536)throw new InvalidDataException("Update manifest is too large.");memory.Write(buffer,0,count);}
        var update=JsonSerializer.Deserialize<WindowsUpdate>(memory.ToArray(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Invalid update manifest.");
        Validate(update,channel);return Normalize(Version.Parse(update.Version))>Normalize(current)?update:null;
    }
    public static Version Normalize(Version value)=>new(value.Major,value.Minor,Math.Max(0,value.Build),Math.Max(0,value.Revision));
    public static void Validate(WindowsUpdate update)=>Validate(update,RequireChannel());
    public static void Validate(WindowsUpdate update,PublicUpdateChannel channel)
    {
        channel.Validate();
        if(!Version.TryParse(update.Version,out _)||!Uri.TryCreate(update.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.IsDefaultPort||!uri.AbsolutePath.StartsWith("/"+channel.Repository+"/releases/download/",StringComparison.Ordinal)||!string.IsNullOrEmpty(uri.UserInfo)||uri.Query.Length>0||uri.Fragment.Length>0||update.Length<1||update.Length>1024L*1024*1024)throw new InvalidDataException("Invalid public update location or size.");
        try{if(Convert.FromBase64String(update.EdSignature).Length!=64)throw new InvalidDataException("Invalid update signature length.");}catch(FormatException e){throw new InvalidDataException("Invalid update signature.",e);}
    }
    public static Task VerifyAsync(string archive,WindowsUpdate update,CancellationToken token=default)=>VerifyAsync(archive,update,RequireChannel(),token);
    public static async Task VerifyAsync(string archive,WindowsUpdate update,PublicUpdateChannel channel,CancellationToken token=default)
    {
        Validate(update,channel);if(new FileInfo(archive).Length!=update.Length)throw new InvalidDataException("Update size mismatch.");
        var verifier=new Ed25519Signer();verifier.Init(false,new Ed25519PublicKeyParameters(Convert.FromBase64String(channel.PublicKey),0));
        await using var source=File.OpenRead(archive);var buffer=new byte[81920];int count;
        while((count=await source.ReadAsync(buffer,token))>0)verifier.BlockUpdate(buffer,0,count);
        if(!verifier.VerifySignature(Convert.FromBase64String(update.EdSignature)))throw new InvalidDataException("The update signature is invalid. Your existing app was kept.");
    }
    public static void ExtractVerified(string archive,string destination,CancellationToken token=default)
    {
        using var zip=ZipFile.OpenRead(archive);long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(zip.Entries.Count>10000)throw new InvalidDataException("Too many update files.");
        foreach(var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();total=checked(total+entry.Length);if(total>3L*1024*1024*1024)throw new InvalidDataException("Expanded update is too large.");
            var relative=entry.FullName.TrimEnd('/','\\');if(relative.Length==0)continue;var path=SafePaths.ArchiveEntry(destination,relative);
            if(!names.Add(path)||((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Unsafe update entry.");
            if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path);
        }
        if(!File.Exists(Path.Combine(destination,"Lokum.exe")))throw new InvalidDataException("The update does not contain Lokum.");
    }
}
