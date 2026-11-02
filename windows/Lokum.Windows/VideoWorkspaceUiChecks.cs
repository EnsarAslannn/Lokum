using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;

namespace Lokum.Windows;

internal static class VideoWorkspaceUiChecks
{
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item)yield return item;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static bool Settings(ToolEditor editor)=>(bool)typeof(ToolEditor).GetMethod("ReadSettings",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,[false])!;
    private static async Task Until(Func<bool> ready)
    {
        var deadline=DateTime.UtcNow.AddSeconds(90);
        while(!ready()){if(DateTime.UtcNow>deadline)throw new Exception("Video preview did not finish");await Task.Delay(50);}
    }
    internal static async Task Run(string output,string source,string toolDirectory)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool valid,string message){if(!valid)throw new Exception(message);checks.Add(message);}
        var language=L10n.Language;L10n.Language=Environment.GetEnvironmentVariable("LOKUM_VIDEO_UI_LANGUAGE")??"en";Theme.Apply(Environment.GetEnvironmentVariable("LOKUM_VIDEO_UI_APPEARANCE")??"light");
        var owner=new Window{Width=400,Height=300};owner.Show();
        var tools=new Toolchain(toolDirectory);var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));var info=await engine.InspectAsync(source);
        void Capture(Window window,string name){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);ImageEngine.Save(image,Path.Combine(output,name+".png"),"png");}
        try
        {
            await VideoResultPreviewUiChecks.Run(owner,source,output,tools,Check);
            var fractional=new ToolEditor(owner,"redactVideo","Redact Video",source,info with{Duration=6.1236});
            try
            {
                fractional.Show();fractional.UpdateLayout();
                Check(Settings(fractional)&&Math.Abs(fractional.MediaOptions.End!.Value-6.1236)<.000001,"An untouched rounded redaction end time accepts the exact source duration");
            }
            finally{fractional.Close();}
            var mute=new ToolEditor(owner,"muteVideo","Mute Video",source,info);mute.ConfigureMediaRuntime(tools);
            try
            {
                mute.Show();mute.UpdateLayout();
                var range=Find<CheckBox>(mute).SingleOrDefault(c=>AutomationProperties.GetAutomationId(c)=="video.mute.range");
                Check(range is not null,"Mute Video offers whole-video and selected-range modes");
                range!.IsChecked=true;mute.FieldForTest("Start").Text="2";mute.FieldForTest("End").Text="4";
                Check(Settings(mute)&&mute.MediaOptions.Start==2&&mute.MediaOptions.End==4,"Selected-range mute reaches the export settings");
                Click(Find<Button>(mute).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed"));
                var saveDuringPreview=typeof(ToolEditor).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance)!;
                saveDuringPreview.Invoke(mute,null);
                Check(mute.IsVisible,"Enter cannot accept the editor while its processed preview is being prepared");
                await Until(()=>mute.ProcessedPreviewPath is not null);
                Check((await engine.InspectAsync(mute.ProcessedPreviewPath!)).HasAudio,"Muted preview keeps the audio stream for the unmuted remainder");
                foreach(var sample in new[]{(At:1,Silent:false),(At:3,Silent:true),(At:5,Silent:false)})
                {
                    var result=await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-v","info","-ss",sample.At.ToString(),"-i",mute.ProcessedPreviewPath!,"-t","0.25","-vn","-af","volumedetect","-f","null","-"]);
                    var match=System.Text.RegularExpressions.Regex.Match(result.Error,@"mean_volume: (-?[\d.]+) dB");var db=double.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
                    Check(sample.Silent?db< -70:db> -35,"Muted playback preview has the expected audio level at "+sample.At+"s");
                }
                Capture(mute,"mute-range");range.IsChecked=false;
                Check(Settings(mute)&&mute.MediaOptions.Start==0&&mute.MediaOptions.End is null,"Whole-video mode clears the selected mute range");
                mute.FieldForTest("Start").Text="editing";mute.FieldForTest("End").Text="editing";
                Check(Settings(mute)&&mute.MediaOptions.End is null,"Whole-video mute ignores disabled range fields");
                Click(Find<Button>(mute).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.original"));
                Check(!mute.ProcessedPlayback,"Original switches back from muted playback");
            }
            finally{mute.Close();}
            var cut=new ToolEditor(owner,"trimVideo","Cut Video",source,info);cut.ConfigureMediaRuntime(tools);
            try
            {
                cut.Show();cut.UpdateLayout();
                var count=Find<TextBox>(cut).SingleOrDefault(c=>AutomationProperties.GetAutomationId(c)=="Number of clips");
                Check(count is not null,"Cut Video includes the clip count");count!.Text="3";
                Click(Find<Button>(cut).Single(b=>AutomationProperties.GetAutomationId(b)=="video.cuts.apply"));
                var list=Find<ListBox>(cut).Single(b=>AutomationProperties.GetAutomationId(b)=="video.cuts.list");
                Check(list.Items.Count==3,"Applying a clip count creates three selectable clips");
                list.SelectedIndex=0;cut.Timeline.SetEndpoint(false,3);cut.Timeline.SetEndpoint(true,1);
                list.SelectedIndex=1;Check(NumericInput.ParseFinite(cut.FieldForTest("Start").Text)==2,"Editing the first clip leaves the second clip unchanged");
                cut.Timeline.SetEndpoint(false,5);cut.Timeline.SetEndpoint(true,4);
                list.SelectedIndex=0;Check(NumericInput.ParseFinite(cut.FieldForTest("End").Text)==3,"A second clip starting at 4s does not extend the first clip past 3s");
                list.SelectedIndex=2;cut.Timeline.SetEndpoint(true,2);cut.Timeline.SetEndpoint(false,2.5);
                Check(Settings(cut)&&cut.MediaOptions.VideoClips.SequenceEqual(new[]{new MediaRange(1,3),new(4,5),new(2,2.5)}),"Manual clip ranges allow gaps, overlap and arbitrary source-time order");
                Capture(cut,"cut-three-clips");
                Click(Find<Button>(cut).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed"));await Until(()=>cut.ProcessedPreviewPath is not null);
                Check(Math.Abs((await engine.InspectAsync(cut.ProcessedPreviewPath!)).Duration-3.5)<.25,"Multi-clip preview plays just the independent selections without their gaps");
                var cuts=await engine.ToolAsync("trimVideo",[source],output,cut.MediaOptions);
                Check(cuts.Count==3,"Independent selections export as three separate clips");
                foreach(var item in cuts.Zip(new[]{2d,1d,.5}))Check(Math.Abs((await engine.InspectAsync(item.First)).Duration-item.Second)<.15,"A separate clip has its own selected duration");
                Click(Find<Button>(cut).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.original"));
                list.SelectedIndex=1;Check(NumericInput.ParseFinite(cut.FieldForTest("End").Text)==5,"Processed preview duration does not limit source-time selections");
            }
            finally{cut.Close();}
            var frame=Path.Combine(output,"original.png");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-i",source,"-frames:v","1",frame]);var bitmap=ImageEngine.Load(frame);
            var redact=new ToolEditor(owner,"redactVideo","Redact Video",source,info,bitmap);redact.ConfigureMediaRuntime(tools);
            try
            {
                redact.Show();redact.UpdateLayout();redact.FieldForTest("Width").Text="96";redact.FieldForTest("Height").Text="64";
                var style=Find<ComboBox>(redact).Single(c=>c.Items.Contains("blur"));style.SelectedItem="blur";
                Check(Settings(redact)&&redact.MediaOptions.Regions.Length==1,"Default video range accepts an area without changing time fields");
                Check(!Find<Button>(redact).Any(b=>AutomationProperties.GetAutomationId(b)=="video.redaction.add"),"Video redaction restores the original draw-and-save controls");
                await Task.Delay(600);Check(redact.ProcessedPreviewPath is null,"Video redaction does not automatically render or raise repeated range warnings");
                Click(Find<Button>(redact).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed"));
                await Until(()=>redact.ProcessedPreviewPath is not null);var blurPath=redact.ProcessedPreviewPath!;
                Check(redact.ProcessedPlayback,"The original manual result-preview action plays the rendered redaction");Capture(redact,"redaction-blur");
                var blurFrame=await Frame(blurPath,"blur-frame");var sourceFrame=await Frame(source,"source-frame");
                Check(Mean(blurFrame)>20&&Difference(blurFrame,sourceFrame)>2,"Blur preview changes the selected area without replacing it with a black band");
                style.SelectedItem="pixelate";await Task.Delay(600);Check(!redact.ProcessedPlayback&&redact.ProcessedPreviewPath is null,"Changing cover style waits for an explicit preview request");
                Click(Find<Button>(redact).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed"));await Until(()=>redact.ProcessedPreviewPath is {} path&&path!=blurPath);
                Check(redact.ProcessedPlayback,"Manual preview uses the newly selected pixelation style");Capture(redact,"redaction-pixelate");
                var pixelFrame=await Frame(redact.ProcessedPreviewPath!,"pixelate-frame");
                Check(Mean(pixelFrame)>20&&Difference(pixelFrame,blurFrame)>1,"Pixelated preview contains a distinct visible image rather than a black band");
                Check(Settings(redact)&&redact.MediaOptions.RedactionMode=="pixelate","Preview and export use the same selected cover style");
                var exported=(await engine.ToolAsync("redactVideo",[source],output,redact.MediaOptions))[0];var exportFrame=await Frame(exported,"export-frame");
                Check(Difference(exportFrame,pixelFrame)<12,"Processed preview pixels match the saved pixelation within codec tolerance");
                style.SelectedItem="blur";Click(Find<Button>(redact).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.original"));await Task.Delay(1500);
                Check(!redact.ProcessedPlayback&&Find<Button>(redact).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed").IsEnabled,"Original cancels a queued automatic preview so another region can be drawn");

                async Task<byte[]> Frame(string path,string name)
                {
                    var target=Path.Combine(output,name+".png");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-ss","0.7","-i",path,"-frames:v","1",target]);
                    var decoded=new FormatConvertedBitmap(ImageEngine.Load(target),PixelFormats.Bgra32,null,0);var data=new byte[decoded.PixelWidth*decoded.PixelHeight*4];decoded.CopyPixels(data,decoded.PixelWidth*4,0);return data;
                }
                double Mean(byte[] bytes)=>Enumerable.Range(0,60).SelectMany(y=>Enumerable.Range(0,90).SelectMany(x=>new[]{0,1,2}.Select(c=>(double)bytes[(y*bitmap.PixelWidth+x)*4+c]))).Average();
                double Difference(byte[] a,byte[] b)=>Enumerable.Range(0,60).SelectMany(y=>Enumerable.Range(0,90).SelectMany(x=>new[]{0,1,2}.Select(c=>(double)Math.Abs(a[(y*bitmap.PixelWidth+x)*4+c]-b[(y*bitmap.PixelWidth+x)*4+c])))).Average();
            }
            finally{redact.Close();}
            var menuState=Path.Combine(output,"menu-state");var preferences=new PreferencesStore(menuState);preferences.Save(new(){Language=L10n.Language,Enabled=false,KeepInTray=false,AutomaticUpdateChecks=false,Feedback=false,Animations=false});
            var library=new LibraryStore(menuState);library.SetAppearance(Environment.GetEnvironmentVariable("LOKUM_VIDEO_UI_APPEARANCE")??"light");
            var main=new MainWindow([source],library,checkForUpdates:false,offerToolSetup:false);
            try
            {
                main.Show();main.UpdateLayout();var choices=main.UIAuditToolChoices;
                Check(choices.Single(c=>c.Id=="trimVideo").Title=="Cut Video"&&choices.Single(c=>c.Id=="cropVideo").Title=="Crop Video","The wheel distinguishes temporal cutting from spatial cropping");
                Check(choices.Count==7&&!choices.Any(c=>c.Id is "videoSnapshots" or "splitVideo"),"The visible video wheel has no redundant split or screenshot actions");
                var wheel=new WheelWindow(main,choices,_=>{});
                try{wheel.Show();wheel.UpdateLayout();Capture(wheel,"video-wheel");}finally{wheel.Close();}
            }
            finally{main.Close();}
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks,scope="Own-window WPF controls, real FFmpeg exports and playback sources; no global input or user data."},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{owner.Close();L10n.Language=language;}
    }
}
