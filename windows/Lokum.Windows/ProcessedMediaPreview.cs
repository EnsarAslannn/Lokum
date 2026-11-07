using System.Windows.Controls;
using Lokum.Core;
namespace Lokum.Windows;
public sealed partial class ToolEditor
{
    private Button? processedButton;private bool processingPreview,processedPlayback;
    private CancellationTokenSource? processedCancellation;private int previewRequest;
    private double processedDuration;
    private WrapPanel? mediaComparison;
    private readonly TextBlock resultSummary=new(){TextWrapping=System.Windows.TextWrapping.Wrap,Margin=new(4,0,4,8),Visibility=System.Windows.Visibility.Collapsed};
    private bool HasVideoResultPreview=>media?.Video is not null&&tool is "compress" or "changeVideoSpeed" or "cropVideo" or "redactVideo";
    private double PlaybackDuration=>processedPlayback?processedDuration:duration;
    internal string? ProcessedPreviewPath{get;private set;}
    internal bool ProcessedPlayback=>processedPlayback;
    private void PlayOriginalPreview()=>RestoreOriginalPreview(true);
    private void RestoreOriginalPreview(bool play)
    {
        if(media is null)return;
        previewRequest++;processedCancellation?.Cancel();processingPreview=false;
        if(processedButton is not null){processedButton.IsEnabled=true;processedButton.Content=L10n.Text(PreviewButtonText);}
        if(saveCopy is not null)saveCopy.IsEnabled=true;
        processedPlayback=false;drawing.IsEnabled=true;drawing.Visibility=System.Windows.Visibility.Visible;
        scrubber.Maximum=duration;player.SpeedRatio=1;player.Source=new Uri(originalPreviewPath??input);player.Play();if(!play)player.Pause();playing=play;Seek(0);
    }
    private string PreviewButtonText=>HasVideoResultPreview?"Show Result":tool is "trimVideo" or "splitVideo"?"Play Result":tool=="audioToVideo"?"Preview Visualization":"Preview Processed";
    private void WireMediaPreviewChanges()
    {
        if(media is null||processedButton is null)return;
        foreach(var box in fields.Values)box.TextChanged+=(_,_)=>{if(!updatingCuts)MediaSettingsChanged();};
        foreach(var combo in new[]{redactMode,bleepMode,visualization})combo.SelectionChanged+=(_,_)=>MediaSettingsChanged();
        joinClips.Checked+=(_,_)=>MediaSettingsChanged();joinClips.Unchecked+=(_,_)=>MediaSettingsChanged();
    }
    private void MediaSettingsChanged()
    {
        if(media is null)return;
        if(processedPlayback||processingPreview){var position=player.Position.TotalSeconds;RestoreOriginalPreview(false);Seek(Math.Min(position,duration));}
        ProcessedPreviewPath=null;
        resultSummary.Text="";resultSummary.Visibility=System.Windows.Visibility.Collapsed;
    }
    private async Task PreviewProcessed()
    {
        if(previewTools?.Ready!=true){time.Text=L10n.Text("Set up media tools to preview the processed result.");return;}
        if(!ReadSettings(false))return;
        var request=++previewRequest;processedCancellation?.Cancel();
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(previewLifetime.Token);processedCancellation=cancellation;
        processingPreview=true;
        if(processedButton is not null){processedButton.IsEnabled=false;processedButton.Content=L10n.Text("Preparing…");}
        if(saveCopy is not null)saveCopy.IsEnabled=false;
        player.Pause();playing=false;time.Text=L10n.Text("Preparing processed preview…");
        try
        {
            var engine=new MediaEngine(previewTools.Executable("ffmpeg"),previewTools.Executable("ffprobe"));
            var settings=tool is "trimVideo" or "splitVideo"?MediaOptions with{JoinSplitClips=true}:MediaOptions;
            var rendered=(await engine.ToolAsync(tool,[input],PreviewDirectory(),settings,null,cancellation.Token))[0];
            string playable;
            if(HasVideoResultPreview)
            {
                playable=rendered;
                if(!Path.GetExtension(rendered).Equals(".mp4",StringComparison.OrdinalIgnoreCase))
                {
                    playable=SafePaths.Unique(PreviewDirectory(),"result-preview.mp4");
                    await ProcessRunner.RunAsync(previewTools.Executable("ffmpeg"),["-v","error","-nostdin","-i",rendered,"-map","0:v:0","-map","0:a:0?","-c","copy","-movflags","+faststart",playable],cancellation.Token);
                }
            }
            else playable=(await engine.ConvertAsync([rendered],media?.Video is null&&tool!="audioToVideo"?"wav":"mp4",PreviewDirectory(),new(){PreserveMetadata=false},null,cancellation.Token))[0];
            var info=await engine.InspectAsync(playable,cancellation.Token);
            if(cancellation.IsCancellationRequested||request!=previewRequest)return;
            if(tool=="compress")SetResultSummary(string.Format(System.Globalization.CultureInfo.CurrentCulture,L10n.Text("Result: {0} KB · Original: {1} KB"),(new FileInfo(rendered).Length/1024d).ToString("0.0",System.Globalization.CultureInfo.CurrentCulture),(new FileInfo(input).Length/1024d).ToString("0.0",System.Globalization.CultureInfo.CurrentCulture)));
            ProcessedPreviewPath=playable;processedDuration=info.Duration;processedPlayback=true;player.SpeedRatio=1;
            player.Source=new Uri(playable);scrubber.Maximum=processedDuration;drawing.IsEnabled=false;drawing.Visibility=System.Windows.Visibility.Hidden;
            Seek(tool=="redactVideo"?Math.Min(settings.Regions.Select(r=>r.Start).DefaultIfEmpty(0).Min(),Math.Max(0,processedDuration-.05)):0);
            player.Play();playing=true;time.Text=L10n.Text("Processed preview · export uses the original file.");
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(IsVisible&&request==previewRequest)time.Text=L10n.Text("Preview unavailable: ")+error.Message;}
        finally
        {
            if(ReferenceEquals(processedCancellation,cancellation))processedCancellation=null;
            if(request==previewRequest)
            {
                processingPreview=false;if(saveCopy is not null)saveCopy.IsEnabled=true;
                if(processedButton is not null){processedButton.IsEnabled=true;processedButton.Content=L10n.Text(PreviewButtonText);}
            }
        }
    }
    private void SetResultSummary(string text){resultSummary.Text=text;resultSummary.Visibility=System.Windows.Visibility.Visible;}
}
