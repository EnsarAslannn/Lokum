using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Lokum.Core;
namespace Lokum.Windows;

public sealed partial class ToolEditor
{
    private ConversionOperation? conversionOperation;private Toolchain? conversionTools;private OfficeRuntime? conversionOffice;
    private Button? conversionPreviewButton;private FilePreviewWindow? conversionViewer;private CancellationTokenSource? conversionCancellation;private int conversionRevision;
    internal void ConfigureConversionRuntime(Toolchain tools,OfficeRuntime office,MetadataService metadata)
    {
        conversionTools=tools;conversionOffice=office;conversionOperation=new(tools,office,metadata);
    }
    private void EnsureConversionRuntime()
    {
        if(conversionOperation is not null)return;
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lokum","Tools");
        ConfigureConversionRuntime(previewTools??new Toolchain(Path.Combine(root,"v1")),new OfficeRuntime(root),new MetadataService(root));
    }
    internal Task<IReadOnlyList<string>> ExportConversionAsync(string destination,IProgress<OperationProgress>? progress,CancellationToken token)
    {
        EnsureConversionRuntime();
        return conversionOperation!.RunAsync(new(OrderedInputs,Format,ImageOptions,MediaOptions,DocumentOptions,PreserveImageMetadata),destination,progress,token);
    }
    private void BuildConversionPreview(DockPanel footer)
    {
        if(tool!="convert")return;
        conversionPreviewButton=MainWindow.Button("Show Result",async()=>await PreviewConversion());AutomationProperties.SetAutomationId(conversionPreviewButton,"conversion.preview");DockPanel.SetDock(conversionPreviewButton,Dock.Left);footer.Children.Add(conversionPreviewButton);
        // Profiles add fields after the constructor. Attach once the complete editor
        // is loaded, so every setting (including passwords) invalidates the result.
        Loaded+=(_,_)=>
        {
            foreach(var box in fields.Values)box.TextChanged+=(_,_)=>InvalidateConversion();
            foreach(var combo in new[]{format,sampleDepth,documentLayout,epubLayout})combo.SelectionChanged+=(_,_)=>InvalidateConversion();
            preserveMetadata.Checked+=(_,_)=>InvalidateConversion();preserveMetadata.Unchecked+=(_,_)=>InvalidateConversion();archivePassword.PasswordChanged+=(_,_)=>InvalidateConversion();
        };
        Closed+=(_,_)=>{conversionRevision++;conversionCancellation?.Cancel();conversionViewer?.Close();};
    }
    private void InvalidateConversion()
    {
        conversionRevision++;conversionCancellation?.Cancel();resultSummary.Text="";resultSummary.Visibility=Visibility.Collapsed;
    }
    private async Task PreviewConversion()
    {
        if(processingPreview||!ReadSettings(false))return;
        var revision=++conversionRevision;EnsureConversionRuntime();
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(previewLifetime.Token);conversionCancellation=cancellation;
        processingPreview=true;conversionPreviewButton!.IsEnabled=false;if(saveCopy is not null)saveCopy.IsEnabled=false;SetResultSummary(L10n.Text("Preparing converted result…"));
        try
        {
            var outputs=await ExportConversionAsync(PreviewDirectory(),null,cancellation.Token);
            if(cancellation.IsCancellationRequested||revision!=conversionRevision||!IsVisible)return;
            var bytes=outputs.Sum(path=>new FileInfo(path).Length);
            SetResultSummary(string.Format(System.Globalization.CultureInfo.CurrentCulture,L10n.Text("Converted result: {0} · {1} KB · {2} files"),Format.ToUpperInvariant(),(bytes/1024d).ToString("0.0",System.Globalization.CultureInfo.CurrentCulture),outputs.Count));
            conversionViewer=new FilePreviewWindow(this,outputs,conversionTools,conversionOffice);
            conversionViewer.ShowDialog();conversionViewer=null;
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(IsVisible&&revision==conversionRevision)SetResultSummary(L10n.Text("Preview unavailable. ")+L10n.Text(error.Message));}
        finally
        {
            if(ReferenceEquals(conversionCancellation,cancellation))conversionCancellation=null;
            processingPreview=false;if(IsVisible){conversionPreviewButton.IsEnabled=true;if(saveCopy is not null)saveCopy.IsEnabled=true;}
        }
    }
}
