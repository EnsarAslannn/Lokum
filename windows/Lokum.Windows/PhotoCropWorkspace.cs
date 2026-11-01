using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Lokum.Windows;

public sealed partial class ToolEditor
{
    private Rect? photoCrop;
    private bool photoCropEditing,photoPreviewValid;
    private Button? photoCropStart,photoCropPreview,photoCropRemove,saveCopy;
    private CheckBox? photoOriginalToggle;
    private readonly TextBlock photoEditStatus=new(){TextWrapping=TextWrapping.Wrap,Margin=new(4,8,4,12)};
    private bool CropInteractionActive=>tool is "crop" or "cropVideo"||tool=="editImage"&&photoCropEditing;

    private void BuildIntegratedCrop()
    {
        var row=new WrapPanel();
        photoCropStart=MainWindow.Button("Crop image",StartPhotoCrop);AutomationProperties.SetAutomationId(photoCropStart,"PhotoCropStart");row.Children.Add(photoCropStart);
        photoCropPreview=MainWindow.Button("Preview crop",FinishPhotoCrop);AutomationProperties.SetAutomationId(photoCropPreview,"PhotoCropPreview");row.Children.Add(photoCropPreview);
        photoCropRemove=MainWindow.Button("Remove crop",()=>{ResetIntegratedCrop();QueuePhotoPreview();});AutomationProperties.SetAutomationId(photoCropRemove,"PhotoCropRemove");row.Children.Add(photoCropRemove);
        options.Children.Add(row);options.Children.Add(photoEditStatus);BuildCropOptions();
        UpdatePhotoCropButtons();
    }
    private void StartPhotoCrop()
    {
        if(!photoPreviewValid||bitmap is null)return;
        if(photoOriginalToggle is not null)photoOriginalToggle.IsChecked=false;
        photoCropEditing=true;drawing.IsHitTestVisible=false;UpdatePhotoCropButtons();QueuePhotoPreview();
    }
    private void FinishPhotoCrop()
    {
        if(!photoCropEditing||!photoPreviewValid)return;
        photoCropEditing=false;ClearPhotoCropOverlay();UpdatePhotoCropButtons();QueuePhotoPreview();
    }
    private void ResetIntegratedCrop()
    {
        photoCrop=null;photoCropEditing=false;ClearPhotoCropOverlay();UpdatePhotoCropButtons();
    }
    private void ClearPhotoCropOverlay()
    {
        drawing.ReleaseMouseCapture();dragging=false;cropGesture=null;
        if(tool!="editImage")return;
        while(drawing.Children.Count>1)drawing.Children.RemoveAt(drawing.Children.Count-1);
        selection=activeRectangle=null;
    }
    private void StorePhotoCrop(Rect rect)
    {
        if(rect.Width<1||rect.Height<1||drawing.Width<1||drawing.Height<1)return;
        var x=Math.Clamp(rect.X,0,drawing.Width-1);var y=Math.Clamp(rect.Y,0,drawing.Height-1);
        var width=Math.Clamp(rect.Width,1,drawing.Width-x);var height=Math.Clamp(rect.Height,1,drawing.Height-y);
        photoCrop=new(x/drawing.Width,y/drawing.Height,width/drawing.Width,height/drawing.Height);
    }
    private void ShowPhotoCropOverlay()
    {
        if(tool!="editImage")return;
        ClearPhotoCropOverlay();
        if(photoCropEditing&&photoCrop is {} area)
        {
            SetCrop(new(area.X*drawing.Width,area.Y*drawing.Height,area.Width*drawing.Width,area.Height*drawing.Height));
        }
    }
    private void UpdatePhotoCropButtons()
    {
        if(tool!="editImage")return;
        if(photoCropStart is not null)photoCropStart.IsEnabled=photoPreviewValid&&!photoCropEditing;
        if(photoCropPreview is not null)photoCropPreview.IsEnabled=photoPreviewValid&&photoCropEditing;
        if(photoCropRemove is not null)photoCropRemove.IsEnabled=photoPreviewValid&&photoCrop is not null;
        if(photoOriginalToggle is not null)photoOriginalToggle.IsEnabled=!photoCropEditing;
        if(saveCopy is not null)saveCopy.IsEnabled=!photoCropEditing&&(photoPreviewValid||showingOriginal);
        aspectRatio.IsEnabled=photoCropEditing&&photoPreviewValid;
    }
}
