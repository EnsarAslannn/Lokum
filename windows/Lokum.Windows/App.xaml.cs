using System.Windows;
namespace Lokum.Windows;
public partial class App:Application
{
    private SingleInstance? instance;
    internal static bool IsSmokeTest{get;private set;}
    private WindowManagement? windowManagement;
    protected override void OnStartup(StartupEventArgs e)
    {
        if(Environment.GetEnvironmentVariable("LOKUM_SMOKE_DIRECTORY") is {} diagnostics)
        {
            Directory.CreateDirectory(diagnostics);
            AppDomain.CurrentDomain.UnhandledException+=(_,error)=>File.WriteAllText(Path.Combine(diagnostics,$"unhandled-{Environment.ProcessId}.txt"),error.ExceptionObject.ToString());
            Exit+=(_,exit)=>File.WriteAllText(Path.Combine(diagnostics,$"process-exit-{Environment.ProcessId}.json"),System.Text.Json.JsonSerializer.Serialize(new{mode=e.Args.FirstOrDefault(),code=exit.ApplicationExitCode,environmentCode=Environment.ExitCode}));
        }
        DispatcherUnhandledException+=(_,error)=>{var folder=Environment.GetEnvironmentVariable("LOKUM_SMOKE_DIRECTORY");if(folder is not null){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"dispatcher-error.txt"),error.Exception.ToString());error.Handled=true;Shutdown(1);}};
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--web-media-import-checks")
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(async()=>{try{await WebMediaImportChecks.Run(e.Args[1]);Shutdown(0);}catch(Exception error){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());Shutdown(1);}});return;
        }
        if(e.Args.Length==2&&e.Args[0] is "--context-media-native" or "--browser-media-native")
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(()=>{try{ContextMediaChecks.Native(e.Args[1],e.Args[0]=="--browser-media-native");Shutdown(0);}catch(Exception error){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());Shutdown(1);}});return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--context-media-checks")
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(async()=>{try{await ContextMediaChecks.Run(e.Args[1]);Shutdown(0);}catch(Exception error){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());Shutdown(1);}});return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--conversion-workspace-ui")
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(async()=>{try{await ConversionWorkspaceUiChecks.Run(e.Args[1],e.Args[2]);Shutdown(0);}catch(Exception error){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());Shutdown(1);}});return;
        }
        if(e.Args.Length==4&&e.Args[0]=="--video-workspace-ui")
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(async()=>{try{await VideoWorkspaceUiChecks.Run(e.Args[1],e.Args[2],e.Args[3]);Shutdown(0);}catch(Exception error){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),error.ToString());Shutdown(1);}});
            return;
        }
        if(e.Args.Contains("--ui-audit"))
        {
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try
            {
                var index=Array.IndexOf(e.Args,"--ui-audit");
                if(index+1>=e.Args.Length||e.Args[index+1].StartsWith("--"))throw new ArgumentException("Use --ui-audit OUTPUT [--scenes scene,...] [--appearance light|dark|both] [--language en|tr|both]");
                string[]? scenes=null,appearances=null,languages=null;
                for(var next=index+2;next<e.Args.Length;next+=2)
                {
                    if(next+1>=e.Args.Length)throw new ArgumentException("Missing value for "+e.Args[next]);
                    var value=e.Args[next+1];
                    switch(e.Args[next])
                    {
                        case "--scenes":scenes=value.Split(',');break;
                        case "--appearance":appearances=value=="both"?null:[value];break;
                        case "--language":languages=value=="both"?null:[value];break;
                        default:throw new ArgumentException("Unknown audit argument: "+e.Args[next]);
                    }
                }
                Shutdown(UIAudit.Run(e.Args[index+1],scenes,appearances,languages));
            }catch(Exception error){Console.Error.WriteLine(error.Message);Shutdown(64);}
            return;
        }
        if(e.Args.Contains("--window-management-fixture"))
        {
            var key=Array.IndexOf(e.Args,"--minimum-window-width");var minimum=key>=0&&key+1<e.Args.Length&&int.TryParse(e.Args[key+1],out var parsed)?Math.Clamp(parsed,160,1200):160;
            new Window{Title=minimum>620?"Lokum Constrained Layout Fixture":"Lokum Window Layout Fixture",Width=Math.Max(620,minimum),MinWidth=minimum,Height=420,Left=80,Top=90,WindowStartupLocation=WindowStartupLocation.Manual,
                Content=new System.Windows.Controls.TextBlock{Text="Lokum window arrangement test fixture",Margin=new Thickness(30),FontSize=22}}.Show();return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--background-editor-fixture")
        {
            // Explicit interactive QA workspace: bypass singleton/global hooks and keep all
            // preferences, Library and exports in the caller's disposable directory.
            IsSmokeTest=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try
            {
                var directory=Path.GetFullPath(e.Args[2]);Directory.CreateDirectory(directory);
                var library=new Lokum.Core.LibraryStore(Path.Combine(directory,"state"));library.SetAppearance("light");
                var preferences=new Lokum.Core.PreferencesStore(library.DirectoryPath);
                preferences.Save(new(){Language="tr",Enabled=false,AutomaticUpdateChecks=false,KeepInTray=false,RevealFinished=false,Feedback=false,Animations=false,OutputMode="folder",OutputFolder=Path.Combine(directory,"exports")});
                var fixture=new MainWindow([Path.GetFullPath(e.Args[1])],library,checkForUpdates:false,offerToolSetup:false);
                fixture.Closed+=(_,_)=>Shutdown(0);fixture.Show();
            }
            catch(Exception error){Console.Error.WriteLine(error);Shutdown(1);}
            return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--apply-update"){try{Shutdown(UpdateInstaller.Apply(e.Args[1]));}catch(Exception error){UseSavedLanguage();MessageBox.Show(L10n.Text(error.Message),L10n.Text("Lokum update could not finish"));Shutdown(1);}return;}
        if(e.Args.Contains("--verify-update-state")){try{ShutdownMode=ShutdownMode.OnExplicitShutdown;SmokeTests.VerifyUpdateState();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);Shutdown(1);}return;}
        if(e.Args.Contains("--smoke-test")){IsSmokeTest=true;try{NativeExceptionTrace.Install();SmokeTests.Run();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);var directory=Environment.GetEnvironmentVariable("LOKUM_SMOKE_DIRECTORY");if(directory is not null){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"error.txt"),error.ToString());}Shutdown(1);}return;}
        if(e.Args.Length==1&&e.Args[0]=="--install-update"){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = InstallUpdateAsync();return;}
        instance=new SingleInstance();Exit+=(_,_)=>instance.Dispose();var paths=e.Args.Where(p=>File.Exists(p)||Directory.Exists(p)).ToArray();
        if(!instance.Primary){ShutdownMode=ShutdownMode.OnExplicitShutdown;_ = ForwardAndExit(paths);return;}
        var window=new MainWindow(paths,offerToolSetup:!e.Args.Contains("--background"));instance.Start(incoming=>Dispatcher.BeginInvoke(()=>window.AcceptLaunch(incoming)));window.Show();
        windowManagement=new WindowManagement(notice:message=>MessageBox.Show(L10n.Text(message),L10n.Text("Window arrangement"),MessageBoxButton.OK,MessageBoxImage.Information),isEnabled:()=>window.WindowManagementEnabled);
    }
    internal static void UseSavedLanguage(Func<string>? readLanguage=null)
    {
        L10n.Language="en";
        try{L10n.Language=readLanguage is null?new Lokum.Core.PreferencesStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lokum")).Value.Language:readLanguage();}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException){/* Preserve the original error dialog in English. */}
    }
    private async Task ForwardAndExit(string[] paths)
    {
        try{await instance!.Notify(paths);Shutdown(0);}catch(Exception error)when(error is IOException or OperationCanceledException or UnauthorizedAccessException){UseSavedLanguage();MessageBox.Show(L10n.Text("Lokum is already running but is still starting. Open it from the system tray or try again in a moment."),"Lokum");Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e){windowManagement?.Dispose();Feedback.Stop();PdfRasterDocument.Shutdown();base.OnExit(e);}
    private async Task InstallUpdateAsync()
    {
        try
        {
            var update=await Lokum.Core.PublicUpdates.CheckAsync(UpdateInstaller.Current);
            if(update is not null)await UpdateInstaller.StageAsync(update,new Progress<Lokum.Core.OperationProgress>(),CancellationToken.None);
            Shutdown(0);
        }
        catch(Exception error)
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lokum");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"update-error.txt"),error.ToString());Shutdown(1);
        }
    }
}
