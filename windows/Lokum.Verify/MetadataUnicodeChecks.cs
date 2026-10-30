using ImageMagick;
using Lokum.Core;
using System.Security.Cryptography;

internal static class MetadataUnicodeChecks
{
    internal static async Task Run(Action<bool,string> check,string tools,string root)
    {
        Directory.CreateDirectory(root);
        var service=new MetadataService(tools);
        check(service.Executable is not null,"Provide an installed ExifTool directory.");
        var fixture=Path.GetFullPath("Tests/LokumCoreTests/Fixtures/astronaut.png");
        var plain=Path.Combine(root,"screen.png");File.Copy(fixture,plain,true);
        check((await service.ReadAsync(plain)).GetValueOrDefault("File:FileType")=="PNG","ASCII PNG metadata could not be read");
        var folder=Path.Combine(root,"Türkçe klasör – çığ ışık");Directory.CreateDirectory(folder);
        var input=Path.Combine(folder,"Ekran görüntüsü 2026-10-30 203000.png");File.Copy(fixture,input,true);
        var hash=SHA256.HashData(File.ReadAllBytes(input));
        var metadata=await service.ReadAsync(input);
        check(metadata.GetValueOrDefault("File:FileType")=="PNG","Turkish screenshot name could not be read");
        check(Path.GetFullPath(metadata["SourceFile"])==Path.GetFullPath(input),"Screenshot path changed while reading metadata");
        const string title="Türkçe başlık çığ ıİşğü destek@example.com $1 \"alıntı\" \\ klasör\n-FileName=do-not-rename.png\r\n#[CSTR] literal\tsekme";
        var edited=(await service.WriteAsync([input],folder,false,new Dictionary<string,string>{{"XMP:Title",title}})).Single();
        var values=await service.ReadAsync(edited);
        check(values.GetValueOrDefault("XMP:Title")==title,"Unicode, escapes or line breaks changed; a value may have become an argument");
        check(!File.Exists(Path.Combine(folder,"do-not-rename.png")),"Metadata text was interpreted as a file-renaming option");
        using(var original=PhotoCodec.Read(input))using(var result=PhotoCodec.Read(edited))
        using(var before=original.GetPixels())using(var after=result.GetPixels())
            check(before.ToByteArray(PixelMapping.RGBA)!.SequenceEqual(after.ToByteArray(PixelMapping.RGBA)!),"Editing metadata changed PNG pixels");
        var copy=Path.Combine(folder,"Kopyalanmış çıktı.png");File.Copy(plain,copy,true);
        await service.CopyImageTagsAsync(edited,copy,CancellationToken.None);
        check((await service.ReadAsync(copy)).GetValueOrDefault("XMP:Title")==title,"Metadata copy failed on Turkish source/output paths");
        var stripped=(await service.WriteAsync([edited],folder,true)).Single();
        check(!(await service.ReadAsync(stripped)).ContainsKey("XMP:Title"),"Remove metadata retained the edited title");
        check(SHA256.HashData(File.ReadAllBytes(input)).SequenceEqual(hash),"Metadata operations modified the original screenshot");
        check(!Directory.EnumerateDirectories(folder,".lokum-*").Any(),"Metadata export left a staging directory");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{await service.ReadAsync(input,cancelled.Token);throw new Exception("Cancelled metadata read continued");}catch(OperationCanceledException){check(true,"Metadata cancellation");}
        var atPath=Path.Combine(folder,"Ekran @ görüntüsü $1.png");File.Copy(input,atPath,true);
        check((await service.ReadAsync(atPath)).GetValueOrDefault("File:FileType")=="PNG","An @ or dollar sign in the filename was changed");
        var largeTitle=new string('ü',40000)+"\nMetin @ $1";
        var large=(await service.WriteAsync([input],folder,false,new Dictionary<string,string>{{"XMP:Title",largeTitle}})).Single();
        check((await service.ReadAsync(large)).GetValueOrDefault("XMP:Title")==largeTitle,"A large UTF-8 metadata value was truncated or deadlocked");
        check(!Directory.EnumerateDirectories(folder,".lokum-*").Any(),"Private metadata value files remained after export");
    }
}
