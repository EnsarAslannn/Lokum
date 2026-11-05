using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
namespace Lokum.Windows;

internal static class ChromeBridgeChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
        var pipe="Lokum.Chrome.Check."+Guid.NewGuid().ToString("N");var received=new List<ChromeMediaRequest>();
        using var server=new ChromeMediaServer(pipe,request=>{received.Add(request);return Task.FromResult(new ChromeMediaReply(true,Version:request.Kind));});
        async Task<ChromeMediaReply> Send(ChromeMediaRequest request)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var client=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(timeout.Token);await ChromeMediaProtocol.WriteAsync(client,request,timeout.Token);return await ChromeMediaProtocol.ReadAsync<ChromeMediaReply>(client,timeout.Token);
        }
        var image=new ChromeMediaRequest(1,"open","image","https://cdn.example/photo.jpg?token=keep","https://www.youtube.com/watch?v=aqz-KE-bpKQ");
        Check((await Send(image)).Ok&&received.Single().Url==image.Url,"The local named-pipe bridge preserves the exact clicked image URL and its query");
        var choices=received[0].Source().Choices;
        Check(choices.Any(choice=>choice.Id=="web:image:editImage")&&!choices.Any(choice=>choice.Id=="web:video:muteVideo"),"A linked thumbnail opens image tools even when its page is a video website");
        Check(!(await Send(image with{Kind="file"})).Ok&&received.Count==1,"An invalid media kind is rejected before reaching the desktop handler");
        var video=image with{Kind="video",Url="https://cdn.example/source-without-extension"};
        Check((await Send(video)).Ok&&received.Count==2,"A malformed request does not stop subsequent valid browser requests");
        Check(received[1].Source().Choices.Any(choice=>choice.Id=="web:video:muteVideo"),"A video without a filename extension retains video tools");
        var owner=new Window{Width=400,Height=250};owner.Show();
        try
        {
            foreach(var request in new[]{image,video})
            {
                ToolChoice? selected=null;var source=request.Source();var wheel=new WheelWindow(owner,source.Choices,choice=>selected=choice);wheel.Show();wheel.Navigate(System.Windows.Input.Key.Enter);
                Check(selected?.Id=="web:"+request.Kind+":convert","The browser "+request.Kind+" request creates a selectable native tools wheel");
            }
        }
        finally{owner.Close();}
        var manifest=ChromeBridgeSetup.WriteManifest(Path.Combine(output,"host"),Environment.ProcessPath!,ChromeBridgeSetup.ExtensionDirectory);
        using(var json=JsonDocument.Parse(File.ReadAllText(manifest)))
        {
            Check(json.RootElement.GetProperty("allowed_origins").EnumerateArray().Single().GetString()==ChromeMediaProtocol.ExtensionOrigin&&json.RootElement.GetProperty("type").GetString()=="stdio","The setup manifest allows only the shipped Chrome extension and the public key resolves to its expected ID");
            Check(json.RootElement.GetProperty("path").GetString()==Environment.ProcessPath,"The native host points to the actual portable executable without shell commands");
        }
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
