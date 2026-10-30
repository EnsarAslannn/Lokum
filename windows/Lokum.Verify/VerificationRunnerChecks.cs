internal static class VerificationRunnerChecks
{
    internal static async Task<int> Run()
    {
        var failures=new List<string>();var checks=0;
        async Task Case(string name,Func<Task> work,int expected,string? message=null)
        {
            using var errors=new StringWriter();
            try
            {
                var code=await VerificationRunner.RunAsync(work,errors);
                checks++;if(code!=expected)failures.Add(name+": incorrect exit code");
                checks++;if(message is null?errors.ToString().Length!=0:!errors.ToString().Contains(message,StringComparison.Ordinal))failures.Add(name+": missing or unexpected diagnostic");
            }
            catch(Exception error){failures.Add(name+": exception escaped the runner ("+error.GetType().Name+")");}
        }
        await Case("success",()=>Task.CompletedTask,0);
        await Case("synchronous failure",()=>throw new InvalidOperationException("Expected fixture failure"),1,"Expected fixture failure");
        await Case("asynchronous failure",async()=>{await Task.Yield();throw new IOException("Türkçe doğrulama hatası");},1,"Türkçe doğrulama hatası");
        await Case("cancelled check",()=>Task.FromCanceled(new CancellationToken(true)),1,"TaskCanceledException");
        foreach(var failure in failures)Console.Error.WriteLine("FAIL runner: "+failure);
        if(failures.Count>0)return 1;
        Console.WriteLine("PASS verification runner: "+checks);return 0;
    }
}
