using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Lokum.Core;
namespace Lokum.Windows;

public sealed partial class ToolEditor
{
    private readonly ListBox cutList=new(){MaxHeight=170,MinHeight=55};
    private MediaRange[] cutRanges=[];private int cutIndex;private bool updatingCuts;
    private void BuildVideoCuts()
    {
        if(tool!="trimVideo")return;
        cutRanges=[new(0,duration)];Field("Number of clips",1);
        AutomationProperties.SetAutomationId(cutList,"video.cuts.list");
        AutomationProperties.SetName(cutList,L10n.Text("Clips"));
        var apply=MainWindow.Button("Apply clip count",()=>
        {
            try
            {
                CommitCutFields();var count=Value("Number of clips",1);
                if(count!=Math.Truncate(count)||count<1||count>1000||duration/count<RangeTimeline.MinimumSpan)throw new ArgumentException("Choose a whole clip count with at least 0.05 seconds per clip.");
                cutRanges=Enumerable.Range(0,(int)count).Select(n=>new MediaRange(duration*n/count,duration*(n+1)/count)).ToArray();cutIndex=0;RefreshCutList();LoadCutFields();MediaSettingsChanged();
            }
            catch(ArgumentException error){ShowMediaValidation(error);}
        });
        AutomationProperties.SetAutomationId(apply,"video.cuts.apply");options.Children.Add(apply);
        options.Children.Add(L10n.Label(new TextBlock{Text="Choose a clip and adjust its own Start and End. Other clips stay unchanged; gaps, overlaps and any source-time order are allowed. Applying a new count resets all clip ranges.",TextWrapping=TextWrapping.Wrap,Margin=new(4,8,4,8)}));
        options.Children.Add(cutList);options.Children.Add(joinClips);
        cutList.SelectionChanged+=(_,_)=>
        {
            if(updatingCuts||cutList.SelectedIndex<0)return;var selected=cutList.SelectedIndex;
            try{CommitCutFields();cutIndex=selected;LoadCutFields();RefreshCutList();Seek(cutRanges[cutIndex].Start);}
            catch(ArgumentException error){updatingCuts=true;cutList.SelectedIndex=cutIndex;updatingCuts=false;ShowMediaValidation(error);}
        };
        RefreshCutList();
    }
    private void RefreshCutList()
    {
        if(cutRanges.Length==0)return;updatingCuts=true;
        cutList.ItemsSource=cutRanges.Select((r,i)=>$"{L10n.Text("Clip")} {i+1} · {Number(r.Start)}–{Number(r.End)} {L10n.Text("Seconds")}").ToArray();cutList.SelectedIndex=cutIndex;updatingCuts=false;
    }
    private void LoadCutFields()
    {
        var range=cutRanges[cutIndex];updatingCuts=true;fields["Start"].Text=Number(range.Start);fields["End"].Text=Number(range.End);timeline.Set(duration,range.Start,range.End);updatingCuts=false;
    }
    private void CommitCutFields()
    {
        if(tool!="trimVideo"||updatingCuts||cutRanges.Length==0)return;
        var (a,b)=ReadPlaybackRange();
        if(b-a<RangeTimeline.MinimumSpan-.00001)throw new ArgumentException("Each clip must be at least 0.05 seconds long.");
        cutRanges[cutIndex]=new(a,b);RefreshCutList();
    }
    private void EditMediaRange(double a,double b)
    {
        if(processedPlayback)RestoreOriginalPreview(false);
        var valid=double.TryParse(fields["Start"].Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out var previous)&&double.IsFinite(previous);if(!valid)previous=-1;
        if(tool=="trimVideo"&&cutRanges.Length>0)
        {
            a=Math.Clamp(a,0,duration-RangeTimeline.MinimumSpan);b=Math.Clamp(b,a+RangeTimeline.MinimumSpan,duration);
            cutRanges[cutIndex]=new(a,b);RefreshCutList();
        }
        fields["Start"].Text=Number(a);fields["End"].Text=Number(b);timeline.Set(duration,a,b);Seek(Math.Abs(a-previous)>.0001?a:b);MediaSettingsChanged();
    }
    private MediaOptions CutSettings(MediaOptions value)
    {
        if(tool!="trimVideo")return value;CommitCutFields();
        return value with{Start=cutRanges[0].Start,End=cutRanges[0].End,EqualClips=null,SplitPoints=[],VideoClips=cutRanges.ToArray()};
    }
    private void ResetVideoCuts()
    {
        if(tool!="trimVideo")return;cutRanges=[new(0,duration)];cutIndex=0;RefreshCutList();LoadCutFields();
    }
    private void ShowMediaValidation(ArgumentException error)
    {
        if(App.IsSmokeTest)throw error;
        MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Check your settings"),MessageBoxButton.OK,MessageBoxImage.Information);
    }
}
