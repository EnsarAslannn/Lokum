using System.Diagnostics;
using System.IO.Pipes;
namespace Lokum.Windows;

internal static class ChromeNativeHost
{
    internal static async Task<int> RunAsync(string origin)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));var output=Console.OpenStandardOutput();
        try
        {
            if(origin!=ChromeMediaProtocol.ExtensionOrigin)throw new InvalidDataException("This Chrome extension is not allowed.");
            var request=(await ChromeMediaProtocol.ReadAsync<ChromeMediaRequest>(Console.OpenStandardInput(),timeout.Token)).Validate();
            var reply=request.Operation=="ping"?new ChromeMediaReply(true,Version:UpdateInstaller.Current.ToString()):await DeliverAsync(request,timeout.Token);
            await ChromeMediaProtocol.WriteAsync(output,reply,timeout.Token);return reply.Ok?0:1;
        }
        catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException or OperationCanceledException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            try{await ChromeMediaProtocol.WriteAsync(output,new ChromeMediaReply(false,error.Message),CancellationToken.None);}catch(IOException){}
            return 1;
        }
    }
    private static async Task<ChromeMediaReply> DeliverAsync(ChromeMediaRequest request,CancellationToken token)
    {
        var started=false;
        for(var attempt=0;attempt<12;attempt++)
        {
            using var client=new NamedPipeClientStream(".",ChromeMediaProtocol.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            try
            {
                await client.ConnectAsync(250,token);await ChromeMediaProtocol.WriteAsync(client,request,token);return await ChromeMediaProtocol.ReadAsync<ChromeMediaReply>(client,token);
            }
            catch(TimeoutException)
            {
                if(!started)
                {
                    started=true;var executable=Environment.ProcessPath??throw new IOException("Lokum executable is unavailable.");
                    var start=new ProcessStartInfo(executable){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden};start.ArgumentList.Add("--background");using var app=Process.Start(start);
                }
                await Task.Delay(150,token);
            }
        }
        return new(false,L10n.Text("Restart Lokum from its system tray, then try the Chrome shortcut again."));
    }
}
