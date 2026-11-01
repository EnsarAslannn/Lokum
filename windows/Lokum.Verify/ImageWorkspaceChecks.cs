using Lokum.Core;

internal static class ImageWorkspaceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var inputs in new string[][]{["photo.png"],["photo.jpg","second.png"]})
        {
            var tools=FeatureCatalog.Tools(inputs);
            check(tools.Contains("removeBackground")&&!tools.Contains("frameImage"),"Image tools must expose one combined background workspace.");
            check(tools.Contains("editImage")&&!tools.Contains("cropImage"),"Crop must be reached from the image editor, not a duplicate image wheel entry.");
            check(tools.Distinct().Count()==tools.Length,"Consolidated image tools contain duplicate entries.");
        }
        check(FeatureCatalog.Tools(["movie.mp4"]).Contains("cropVideo"),"Combining image tools removed video cropping.");
        foreach(var item in new[]{("removeBackground","Arka Plan Düzenle"),("editImage","Görsel Düzenle"),("redactImage","Görsel Sansürle"),("readQRCode","QR Kod Oku")})
            check(AppLocalization.Text(FeatureCatalog.ToolNames[item.Item1],"tr")==item.Item2,"The visible image tool name is incorrect: "+item.Item1);
    }
}
