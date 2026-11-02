using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Lokum.Core;
namespace Lokum.Windows;

public sealed partial class ToolEditor
{
    private readonly CheckBox muteRange=new(){Content="Mute only the selected range",Margin=new(4,8,4,12)};
    private void BuildVideoMute()
    {
        if(tool!="muteVideo")return;
        AutomationProperties.SetAutomationId(muteRange,"video.mute.range");options.Children.Add(muteRange);
        options.Children.Add(L10n.Label(new TextBlock{Text="Leave this off to remove all audio. Turn it on to keep the sound before and after the selected range. Preview the result before saving.",TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,12)}));
        void Apply(){fields["Start"].IsEnabled=fields["End"].IsEnabled=timeline.IsEnabled=muteRange.IsChecked==true;MediaSettingsChanged();}
        muteRange.Checked+=(_,_)=>Apply();muteRange.Unchecked+=(_,_)=>Apply();Apply();
    }
    private MediaOptions MuteSettings(MediaOptions value)=>tool=="muteVideo"&&muteRange.IsChecked!=true?value with{Start=0,End=null}:value;
}
