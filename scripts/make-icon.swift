import AppKit

@main struct MakeLokumIcon {
    static func main() throws {
        let output=URL(fileURLWithPath:CommandLine.arguments[1]);try FileManager.default.createDirectory(at:output,withIntermediateDirectories:true)
        func bitmap(_ width:Int,_ height:Int,_ draw:(NSRect)->Void)->NSBitmapImageRep {
            let rep=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:width,pixelsHigh:height,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
            NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current=NSGraphicsContext(bitmapImageRep:rep);draw(NSRect(x:0,y:0,width:width,height:height));NSGraphicsContext.restoreGraphicsState();return rep
        }
        for happy in [false,true] { let mark=bitmap(256,256) { rect in LokumArtwork.draw(in:rect,happy:happy) };try mark.representation(using:.png,properties:[:])!.write(to:output.appendingPathComponent(happy ? "LokumMarkHappy.png":"LokumMark.png")) }
        let icon=bitmap(1024,1024) { _ in
            let background=NSBezierPath(roundedRect:NSRect(x:48,y:48,width:928,height:928),xRadius:205,yRadius:205)
            NSGradient(starting:NSColor(srgbRed:0.2863,green:0.1882,blue:0.2471,alpha:1),ending:NSColor(srgbRed:0.1255,green:0.1020,blue:0.1451,alpha:1))!.draw(in:background,angle:-90)
            LokumArtwork.draw(in:NSRect(x:125,y:90,width:774,height:824),happy:true)
        }
        let png=icon.representation(using:.png,properties:[:])!;try png.write(to:output.appendingPathComponent("LokumIcon.png"))
        let iconset=output.appendingPathComponent("LokumIcon.iconset");try FileManager.default.createDirectory(at:iconset,withIntermediateDirectories:true)
        let image=NSImage(data:png)!
        for size in [16,32,128,256,512] { for factor in [1,2] {
            let pixels=size*factor;let rep=bitmap(pixels,pixels) { rect in NSGraphicsContext.current?.imageInterpolation = .high;image.draw(in:rect) }
            try rep.representation(using:.png,properties:[:])!.write(to:iconset.appendingPathComponent("icon_\(size)x\(size)\(factor==2 ? "@2x":"").png"))
        } }
        let banner=bitmap(1280,640) { rect in
            NSColor(srgbRed:1,green:0.9686,blue:0.9569,alpha:1).setFill();rect.fill()
            LokumArtwork.draw(in:NSRect(x:75,y:115,width:390,height:410),happy:true)
            let font=NSFont.systemFont(ofSize:118,weight:.bold);let rounded=NSFont(descriptor:font.fontDescriptor.withDesign(.rounded)!,size:118)!
            ("Lokum" as NSString).draw(at:NSPoint(x:515,y:315),withAttributes:[.font:rounded,.foregroundColor:LokumArtwork.ink])
            ("Your files, a little sweeter." as NSString).draw(at:NSPoint(x:523,y:265),withAttributes:[.font:NSFont.systemFont(ofSize:32,weight:.medium),.foregroundColor:NSColor(srgbRed:0.5255,green:0.1686,blue:0.3020,alpha:1)])
            ("Convert. Edit. Organize. Download." as NSString).draw(at:NSPoint(x:523,y:208),withAttributes:[.font:NSFont.systemFont(ofSize:23),.foregroundColor:NSColor(srgbRed:0.4549,green:0.3608,blue:0.4275,alpha:1)])
        }
        try banner.representation(using:.png,properties:[:])!.write(to:output.appendingPathComponent("LokumSocial.png"))
        print(output.path)
    }
}
