using System.Runtime.InteropServices;
namespace Lokum.Windows;

internal static class NativeExceptionTrace
{
    private static nint library;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void InstallTrace();
    internal static void Install()
    {
        if(!App.IsSmokeTest||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||Environment.GetEnvironmentVariable("LOKUM_NATIVE_TRACE_LIBRARY") is not {} path)return;
        library=NativeLibrary.Load(Path.GetFullPath(path));
        Marshal.GetDelegateForFunctionPointer<InstallTrace>(NativeLibrary.GetExport(library,"InstallTrace"))();
    }
}
