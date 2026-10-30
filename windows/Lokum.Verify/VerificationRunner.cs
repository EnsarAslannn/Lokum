internal static class VerificationRunner
{
    internal static async Task<int> RunAsync(Func<Task> work,TextWriter errors)
    {
        try{await work();return 0;}
        catch(Exception error)
        {
            await errors.WriteLineAsync("FAIL verification: "+error);
            return 1;
        }
    }
}
