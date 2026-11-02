using Lokum.Core;
using System.Globalization;

internal static class VideoWorkspaceChecks
{
    internal static async Task Run(Action<bool,string> check,string output,string ffmpeg,string ffprobe)
    {
        Directory.CreateDirectory(output);
        var source=Path.Combine(output,"Video with tone.mp4");
        await ProcessRunner.RunAsync(ffmpeg,["-y","-f","lavfi","-i","testsrc2=size=160x96:rate=15:duration=6","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=6","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",source]);
        var original=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source));
        var engine=new MediaEngine(ffmpeg,ffprobe);
        var muted=(await engine.ToolAsync("muteVideo",[source],output,new(){Start=2,End=4}))[0];
        check((await engine.InspectAsync(muted)).HasAudio,"Range mute removed the whole audio track instead of keeping sound outside 2–4 seconds");
        foreach(var item in new[]{(At:1d,Silent:false),(At:3d,Silent:true),(At:5d,Silent:false)})
        {
            var measured=await ProcessRunner.RunAsync(ffmpeg,["-v","info","-ss",item.At.ToString(CultureInfo.InvariantCulture),"-i",muted,"-t","0.25","-vn","-af","volumedetect","-f","null","-"]);
            var match=System.Text.RegularExpressions.Regex.Match(measured.Error,@"mean_volume: (-?[\d.]+) dB");
            var db=double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
            check(item.Silent?db< -70:db> -35,$"Range mute produced the wrong audio level at {item.At}s: {db} dB");
        }
        var whole=(await engine.ToolAsync("muteVideo",[source],output,new()))[0];
        check(!(await engine.InspectAsync(whole)).HasAudio,"Whole-video mute no longer removes the entire audio track");
        var independent=System.Text.Json.JsonSerializer.Deserialize<MediaOptions>("""{"VideoClips":[{"Start":1,"End":3},{"Start":4,"End":5},{"Start":0.5,"End":1}]}""")!;
        var independentFiles=await engine.ToolAsync("trimVideo",[source],output,independent);
        check(independentFiles.Count==3,"Independent clip intervals were ignored by Cut Video");
        foreach(var item in independentFiles.Zip(new[]{2d,1d,.5}))check(Math.Abs((await engine.InspectAsync(item.First)).Duration-item.Second)<.15,"Independent clip order or duration changed");
        var independentJoined=(await engine.ToolAsync("trimVideo",[source],output,independent with{JoinSplitClips=true}))[0];
        check(Math.Abs((await engine.InspectAsync(independentJoined)).Duration-3.5)<.2,"Joining independent clips retained the gaps between selections");
        var cuts=await engine.ToolAsync("trimVideo",[source],output,new(){Start=.5,End=5.5,SplitPoints=[1.5,4]});
        check(cuts.Count==3,"Cut Video did not export three manually selected clips");
        var expected=new[]{1d,2.5,1.5};
        for(var n=0;n<cuts.Count;n++)check(Math.Abs((await engine.InspectAsync(cuts[n])).Duration-expected[n])<.15,"A manually placed cut changed clip duration");
        var joined=(await engine.ToolAsync("trimVideo",[source],output,new(){Start=.5,End=5.5,SplitPoints=[1.5,4],JoinSplitClips=true}))[0];
        check(Math.Abs((await engine.InspectAsync(joined)).Duration-5)<.2,"Joined cut preview includes frames outside the selected overall range");
        check(original.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))),"Video editing modified the original source");
        var choices=FeatureCatalog.Tools([source]);
        check(choices.Contains("trimVideo")&&choices.Contains("cropVideo")&&!choices.Contains("splitVideo")&&!choices.Contains("videoSnapshots"),"The video menu still exposes duplicate split/snapshot actions");
    }
}
