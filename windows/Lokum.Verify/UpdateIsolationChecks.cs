using Lokum.Core;
using System.IO.Compression;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

internal static class UpdateIsolationChecks
{
    internal static async Task Run(Action<bool,string> check,string root)
    {
        var upstream=new WindowsUpdate("99.0.0.0","https://github.com/example/other-app/releases/download/test/OtherApp.zip",1,Convert.ToBase64String(new byte[64]));
        var rejected=false;
        try { PublicUpdates.Validate(upstream); }
        catch(Exception error)when(error is InvalidDataException or InvalidOperationException){rejected=true;}
        check(rejected,"A default Lokum build still trusts the unrelated release channel");
        try{await PublicUpdates.CheckAsync(new Version(1,0));throw new Exception("An unconfigured Lokum build attempted a network update check");}
        catch(InvalidOperationException error){check(error.Message.Contains("not connected",StringComparison.Ordinal),"Missing release channel did not explain how updates become available");}
        check(PublicUpdates.LoadChannel(Path.Combine(root,"missing-channel.json")) is null,"A missing release channel invented publisher trust");

        var privateKey=new Ed25519PrivateKeyParameters(Enumerable.Repeat((byte)7,32).ToArray());
        var publicKey=Convert.ToBase64String(privateKey.GeneratePublicKey().GetEncoded());
        var channel=new PublicUpdateChannel("example/lokum",publicKey);
        var path=Path.Combine(root,"updates-test-channel.json");File.WriteAllText(path,JsonSerializer.Serialize(channel));
        check(PublicUpdates.LoadChannel(path)==channel,"Configured repository or publisher key changed while loading");
        check(channel.Feed=="https://github.com/example/lokum/releases/latest/download/windows-update.json","Configured release feed does not belong to the selected publisher");
        foreach(var bad in new[]{channel with{Repository="../lokum"},channel with{Repository="example/.."},channel with{Repository="https://github.com/example/lokum"},channel with{PublicKey="not a key"},channel with{PublicKey=Convert.ToBase64String(new byte[31])},channel with{PublicKey=Convert.ToBase64String(new byte[32])}})
        {
            File.WriteAllText(path,JsonSerializer.Serialize(bad));var invalid=false;try{PublicUpdates.LoadChannel(path);}catch(InvalidDataException){invalid=true;}
            check(invalid,"Invalid publisher configuration was accepted");
        }
        File.WriteAllText(path,"{broken");var malformed=false;try{PublicUpdates.LoadChannel(path);}catch(InvalidDataException){malformed=true;}
        check(malformed,"Malformed update configuration did not fail closed");

        var archive=Path.Combine(root,"signed-lokum-test.zip");
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){using var executable=zip.CreateEntry("Lokum.exe").Open();executable.Write("static-test-fixture"u8);}
        var bytes=File.ReadAllBytes(archive);var signer=new Ed25519Signer();signer.Init(true,privateKey);signer.BlockUpdate(bytes,0,bytes.Length);
        var update=new WindowsUpdate("99.0.0.0","https://github.com/example/lokum/releases/download/test/Lokum.zip",bytes.Length,Convert.ToBase64String(signer.GenerateSignature()));
        await PublicUpdates.VerifyAsync(archive,update,channel);check(true,"A publisher-signed Lokum archive did not verify");
        var extraction=Path.Combine(root,"signed-lokum-extracted");try{PublicUpdates.ExtractVerified(archive,extraction);check(File.ReadAllText(Path.Combine(extraction,"Lokum.exe"))=="static-test-fixture","Verified update extraction changed its payload");}finally{if(Directory.Exists(extraction))Directory.Delete(extraction,true);}
        foreach(var url in new[]{upstream.Url,"https://github.com/another/repo/releases/download/test/Lokum.zip",update.Url+"?token=x",update.Url+"#fragment",update.Url.Replace("https://","http://",StringComparison.Ordinal),update.Url.Replace("github.com/","github.com:444/",StringComparison.Ordinal)})
        {var invalid=false;try{PublicUpdates.Validate(update with{Url=url},channel);}catch(InvalidDataException){invalid=true;}check(invalid,"A release from outside the configured Lokum channel was accepted");}
        var wrongKey=channel with{PublicKey=Convert.ToBase64String(new Ed25519PrivateKeyParameters(Enumerable.Repeat((byte)8,32).ToArray()).GeneratePublicKey().GetEncoded())};
        try{await PublicUpdates.VerifyAsync(archive,update,wrongKey);throw new Exception("An archive signed by another publisher was accepted");}catch(InvalidDataException){check(true,"Wrong publisher key was not rejected");}
        bytes[^1]^=1;File.WriteAllBytes(archive,bytes);
        try{await PublicUpdates.VerifyAsync(archive,update,channel);throw new Exception("Modified signed archive was accepted");}catch(InvalidDataException){check(true,"Archive modification was not rejected");}
    }
}
