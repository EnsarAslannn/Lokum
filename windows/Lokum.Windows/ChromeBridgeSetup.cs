using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
namespace Lokum.Windows;

internal static class ChromeBridgeSetup
{
    internal static string ExtensionDirectory=>Path.Combine(AppContext.BaseDirectory,"ChromeExtension");
    internal static string WriteManifest(string destination,string executable,string extensionDirectory)
    {
        if(!File.Exists(executable))throw new IOException("Lokum executable is unavailable.");
        using var extension=JsonDocument.Parse(File.ReadAllText(Path.Combine(extensionDirectory,"manifest.json")));
        var key=Convert.FromBase64String(extension.RootElement.GetProperty("key").GetString()!);var hash=SHA256.HashData(key);
        var id=string.Concat(hash.Take(16).SelectMany(value=>new[]{(char)('a'+(value>>4)),(char)('a'+(value&15))}));
        if(id!=ChromeMediaProtocol.ExtensionId)throw new InvalidDataException("Chrome extension identity does not match this Lokum version.");
        Directory.CreateDirectory(destination);var file=Path.Combine(destination,ChromeMediaProtocol.HostName+".json");
        var manifest=new{name=ChromeMediaProtocol.HostName,description="Lokum media tools",path=Path.GetFullPath(executable),type="stdio",allowed_origins=new[]{ChromeMediaProtocol.ExtensionOrigin}};
        File.WriteAllText(file,JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));return file;
    }
    internal static void Register()
    {
        var manifest=WriteManifest(ExtensionDirectory,Path.Combine(AppContext.BaseDirectory,"Lokum.exe"),ExtensionDirectory);
        foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})
        {
            using var root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view);
            using var key=root.CreateSubKey(@"SOFTWARE\Google\Chrome\NativeMessagingHosts\"+ChromeMediaProtocol.HostName);key.SetValue("",manifest,RegistryValueKind.String);
        }
    }
}
