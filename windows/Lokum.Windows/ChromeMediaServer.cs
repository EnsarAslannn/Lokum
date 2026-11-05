using System.IO.Pipes;
namespace Lokum.Windows;

internal sealed class ChromeMediaServer:IDisposable
{
    private readonly CancellationTokenSource lifetime=new();private NamedPipeServerStream? active;
    internal ChromeMediaServer(string pipe,Func<ChromeMediaRequest,Task<ChromeMediaReply>> receive)
    {
        _ = Task.Run(async()=>
        {
            while(!lifetime.IsCancellationRequested)
            {
                try
                {
                    using var server=new NamedPipeServerStream(pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);active=server;
                    await server.WaitForConnectionAsync(lifetime.Token);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(8));
                    ChromeMediaReply reply;
                    try{var request=(await ChromeMediaProtocol.ReadAsync<ChromeMediaRequest>(server,deadline.Token)).Validate();reply=await receive(request).WaitAsync(deadline.Token);}
                    catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException){reply=new(false,error.Message);}
                    await ChromeMediaProtocol.WriteAsync(server,reply,deadline.Token);
                }
                catch(OperationCanceledException)when(lifetime.IsCancellationRequested){break;}
                catch(ObjectDisposedException)when(lifetime.IsCancellationRequested){break;}
                catch(Exception error)when(error is IOException or OperationCanceledException or UnauthorizedAccessException){if(!lifetime.IsCancellationRequested)await Task.Delay(100);}
                finally{active=null;}
            }
        });
    }
    public void Dispose(){lifetime.Cancel();active?.Dispose();}
}
