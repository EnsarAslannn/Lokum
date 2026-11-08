using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Lokum.Core;
using Microsoft.Win32;

namespace Lokum.Windows;

internal sealed class SubtitleEditorWindow:Window
{
    private readonly string input;
    private readonly MediaInfo info;
    private readonly Toolchain tools;
    private readonly SpeechModelSetup speech;
    private readonly MediaElement player=new(){LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close,Stretch=Stretch.Uniform};
    private readonly TextBlock caption=new(){Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(210,0,0,0)),FontSize=22,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Padding=new(12),Margin=new(24),VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(4,12,4,8)};
    private readonly TextBox text=new(){AcceptsReturn=true,AcceptsTab=true,VerticalContentAlignment=VerticalAlignment.Top,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,FontFamily=new("Consolas"),MinHeight=200};
    private readonly ComboBox language=new(){ItemsSource=TranscriptionEngine.Languages.Values.ToArray(),SelectedIndex=0};
    private readonly ComboBox format=new(){ItemsSource=new[]{"SRT","TXT","VTT","MP4"},SelectedIndex=0};
    private readonly ProgressBar progress=new(){Height=4,IsIndeterminate=true,Visibility=Visibility.Collapsed};
    private readonly Slider seek=new(){Minimum=0};
    private readonly Button generate,preview,save,import;
    private readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? active;
    private string? previewDirectory;
    private string? proxyDirectory,originalProxy;
    private bool preparingProxy,attemptedProxy;
    private SubtitleTranscript? transcript;
    private bool busy,closed,resultPlaying,playing;
    public SubtitleTranscript Transcript=>SubtitleTranscript.FromSrt(text.Text,info.Duration);
    public string Format=>(format.SelectedItem as string??"SRT").ToLowerInvariant();
    private bool NeedsOriginalProxy=>info.Video is {} video?video.Codec!="h264"||video.Bits>8||FeatureCatalog.Extension(input) is not ("mp4" or "mov" or "m4v"):FeatureCatalog.Extension(input) is not ("wav" or "mp3" or "m4a" or "aac" or "wma");
    public SubtitleEditorWindow(Window owner,string input,MediaInfo info,Toolchain tools,SpeechModelSetup speech)
    {
        Theme.Attach(this);Owner=owner;this.input=input;this.info=info;this.tools=tools;this.speech=speech;
        Title=L10n.Text("Generate Subtitles")+" · Lokum";Width=1120;Height=790;MinWidth=800;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid{Margin=new(26)};Content=root;root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel{Margin=new(0,0,0,20)};header.Children.Add(L10n.Label(new TextBlock{Text="Generate Subtitles",FontSize=30,FontWeight=FontWeights.Bold}));header.Children.Add(new TextBlock{Text=Path.GetFileName(input),Foreground=Theme.Brush("Muted"),Margin=new(0,5,0,0)});root.Children.Add(header);
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetRow(body,1);root.Children.Add(body);
        var left=new Grid{Margin=new(0,0,20,0)};left.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=GridLength.Auto});body.Children.Add(left);
        var screen=new Grid{Background=Brushes.Black,MinHeight=220};screen.Children.Add(player);screen.Children.Add(caption);left.Children.Add(screen);if(!NeedsOriginalProxy){player.Source=new Uri(input);player.Pause();}
        seek.Maximum=info.Duration;Grid.SetRow(seek,1);left.Children.Add(seek);seek.ValueChanged+=(_,_)=>{if(seek.IsMouseCaptureWithin||seek.IsKeyboardFocusWithin)player.Position=TimeSpan.FromSeconds(seek.Value);};
        var playback=new WrapPanel();Grid.SetRow(playback,2);left.Children.Add(playback);playback.Children.Add(MainWindow.Button("Play / pause",()=>{if(playing)player.Pause();else player.Play();playing=!playing;}));playback.Children.Add(MainWindow.Button("Original",Original));
        var right=new Grid();Grid.SetColumn(right,1);body.Children.Add(right);foreach(var _ in Enumerable.Range(0,3))right.RowDefinitions.Add(new(){Height=GridLength.Auto});right.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});right.RowDefinitions.Add(new(){Height=GridLength.Auto});
        right.Children.Add(L10n.Label(new TextBlock{Text="Spoken language",FontWeight=FontWeights.SemiBold}));Grid.SetRow(language,1);right.Children.Add(language);
        var actions=new WrapPanel{Margin=new(0,8,0,8)};Grid.SetRow(actions,2);right.Children.Add(actions);generate=MainWindow.Button("Generate subtitles",async()=>await Generate());import=MainWindow.Button("Open SRT…",Import);actions.Children.Add(generate);actions.Children.Add(import);
        Grid.SetRow(text,3);right.Children.Add(text);text.TextChanged+=(_,_)=>Edited();
        var hint=L10n.Label(new TextBlock{Text="Edit the text and timestamps below. Review automatic subtitles before sharing.",TextWrapping=TextWrapping.Wrap,Foreground=Theme.Brush("Muted"),Margin=new(0,10,0,0)});Grid.SetRow(hint,4);right.Children.Add(hint);
        var footer=new StackPanel{Margin=new(0,16,0,0)};Grid.SetRow(footer,2);root.Children.Add(footer);footer.Children.Add(progress);footer.Children.Add(status);
        var bar=new DockPanel{LastChildFill=false};footer.Children.Add(bar);save=MainWindow.Button("Save new copy  ↗",()=>{try{_=Transcript;DialogResult=true;}catch(Exception error){Status(error.Message);}});save.Background=Theme.Primary;save.Foreground=Theme.PrimaryInk;DockPanel.SetDock(save,Dock.Right);bar.Children.Add(save);
        preview=MainWindow.Button("Show Result",async()=>await ShowResult());DockPanel.SetDock(preview,Dock.Right);bar.Children.Add(preview);format.Width=90;DockPanel.SetDock(format,Dock.Right);bar.Children.Add(format);bar.Children.Add(MainWindow.Button("Cancel",()=>{if(busy)active?.Cancel();else Close();}));
        if(info.Video is null)format.ItemsSource=new[]{"SRT","TXT","VTT"};format.SelectionChanged+=(_,_)=>Edited();
        format.ToolTip="MP4 embeds subtitles permanently in a new video copy.";
        Status(speech.Ready?"Choose the spoken language, then generate subtitles.":"Free and local. First use downloads a speech model (148 MB). Your media stays on your computer.");UpdateButtons();
        System.Windows.Automation.AutomationProperties.SetAutomationId(text,"subtitles.editor");System.Windows.Automation.AutomationProperties.SetAutomationId(generate,"subtitles.generate");System.Windows.Automation.AutomationProperties.SetAutomationId(preview,"subtitles.preview");System.Windows.Automation.AutomationProperties.SetAutomationId(save,"subtitles.save");
        clock.Tick+=(_,_)=>{if(!seek.IsMouseCaptureWithin&&!seek.IsKeyboardFocusWithin)seek.Value=Math.Clamp(player.Position.TotalSeconds,0,info.Duration);caption.Text=resultPlaying?"":transcript?.At(player.Position.TotalSeconds)??"";caption.Visibility=caption.Text.Length==0?Visibility.Collapsed:Visibility.Visible;};clock.Start();
        player.MediaEnded+=(_,_)=>{playing=false;player.Pause();};player.MediaFailed+=async(_,e)=>{if(!closed&&!resultPlaying&&originalProxy is null&&!preparingProxy)await PrepareOriginalProxy();else if(!closed)Status(e.ErrorException.Message);};
        Closed+=(_,_)=>{closed=true;clock.Stop();lifetime.Cancel();active?.Cancel();player.Close();if(!busy)CleanPreview();if(!preparingProxy)CleanProxy();};
        Loaded+=async(_,_)=>
        {
            if(NeedsOriginalProxy)await PrepareOriginalProxy();
        };
    }
    private void Status(string value)=>status.Text=L10n.Text(value);
    private void UpdateButtons(){generate.IsEnabled=import.IsEnabled=!busy;preview.IsEnabled=save.IsEnabled=!busy&&transcript is not null;text.IsReadOnly=busy;language.IsEnabled=format.IsEnabled=!busy;progress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;}
    private void Edited()
    {
        if(closed)return;if(resultPlaying)Original();
        try{transcript=SubtitleTranscript.FromSrt(text.Text,info.Duration);Status("Subtitles are ready. Review the text and timing.");}catch(Exception error)when(error is ArgumentException or InvalidDataException or FormatException or OverflowException){transcript=null;if(text.Text.Length>0)Status(error.Message);}
        UpdateButtons();
    }
    private void Import(){var dialog=new OpenFileDialog{Filter="SubRip (*.srt)|*.srt"};if(dialog.ShowDialog(this)==true){try{if(new FileInfo(dialog.FileName).Length>10*1024*1024)throw new InvalidDataException("The subtitle file is too large.");text.Text=File.ReadAllText(dialog.FileName);}catch(Exception error){Status(error.Message);}}}
    private void Original(){resultPlaying=false;player.Pause();playing=false;player.Source=originalProxy is not null?new Uri(originalProxy):NeedsOriginalProxy?null:new Uri(input);player.Position=TimeSpan.Zero;}
    private async Task PrepareOriginalProxy()
    {
        if(closed||attemptedProxy)return;attemptedProxy=true;
        preparingProxy=true;proxyDirectory=Path.Combine(Path.GetTempPath(),"Lokum-SubtitleSource-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(proxyDirectory);Status("Preparing a local preview…");
        try
        {
            var path=Path.Combine(proxyDirectory,info.Video is null?"source.wav":"source.mp4");var arguments=new List<string>{"-nostdin","-v","error","-i",input};
            if(info.Video is null)arguments.AddRange(["-vn","-c:a","pcm_s16le","-ar","48000","-ac","2"]);
            else arguments.AddRange(["-vf","scale=w='max(2,trunc(min(1280,iw)/2)*2)':h=-2","-c:v","libx264","-preset","veryfast","-crf","23","-pix_fmt","yuv420p","-c:a","aac","-movflags","+faststart"]);
            arguments.Add(path);await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),arguments,lifetime.Token);
            if(!closed){originalProxy=path;if(!resultPlaying){Original();player.Play();player.Pause();}Status("Preview ready · export uses your original file.");}
        }
        catch(OperationCanceledException){}catch(Exception error){if(!closed)Status(error.Message);}
        finally{preparingProxy=false;if(closed)CleanProxy();}
    }
    private void CleanProxy(){if(proxyDirectory is {} path){try{if(Directory.Exists(path))Directory.Delete(path,true);}catch(IOException){}proxyDirectory=null;}}
    private async Task Generate()
    {
        if(busy)return;busy=true;UpdateButtons();active=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=active.Token;var chosen=TranscriptionEngine.Languages.Keys.ElementAt(Math.Max(0,language.SelectedIndex));
        var report=new Progress<OperationProgress>(p=>{if(closed)return;Status(p.Label);progress.IsIndeterminate=p.Fraction is null;if(p.Fraction is {} n){progress.Maximum=1;progress.Value=n;}});
        try{await speech.InstallAsync(report,token);var result=await new TranscriptionEngine(speech.Executable,speech.Model,tools.Executable("ffmpeg"),tools.Executable("ffprobe")).TranscribeAsync(input,chosen,report,token);if(!closed)text.Text=result.Srt;}
        catch(OperationCanceledException){if(!closed)Status("Cancelled. Originals kept.");}catch(Exception error){if(!closed)Status(error.Message);}
        finally{active.Dispose();active=null;busy=false;if(closed)CleanPreview();else UpdateButtons();}
    }
    private async Task ShowResult()
    {
        if(busy||transcript is null)return;
        if(Format!="mp4"){Original();player.Play();playing=true;return;}
        busy=true;UpdateButtons();active=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);Original();CleanPreview();previewDirectory=Path.Combine(Path.GetTempPath(),"Lokum-SubtitlePreview-"+Guid.NewGuid().ToString("N"));Status("Preparing result preview…");
        try{var files=await TranscriptionEngine.ExportAsync(Transcript,input,"mp4",previewDirectory,tools.Executable("ffmpeg"),active.Token);if(!closed){resultPlaying=true;player.Source=new Uri(files[0]);player.Play();playing=true;Status("Subtitles are ready. Review the text and timing.");}}
        catch(OperationCanceledException){if(!closed)Status("Cancelled. Originals kept.");}catch(Exception error){if(!closed)Status(error.Message);}
        finally{active.Dispose();active=null;busy=false;if(closed)CleanPreview();else UpdateButtons();}
    }
    private void CleanPreview(){if(previewDirectory is {} path){try{if(Directory.Exists(path))Directory.Delete(path,true);}catch(IOException){}previewDirectory=null;}}
}
