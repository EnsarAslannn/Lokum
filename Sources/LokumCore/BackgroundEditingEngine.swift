import Foundation
import AppKit
import CryptoKit
import OnnxRuntimeBindings

public struct ForegroundRegion:Codable,Sendable {
    public var points:[ImagePoint]
    public var subtract:Bool
    public init(points:[ImagePoint],subtract:Bool=false) {self.points=points;self.subtract=subtract}
}
public struct BackgroundEditingOptions:Codable,Sendable {
    public var regions:[ForegroundRegion]=[]
    public var automatic=false
    public var automaticInputs:[String]?=nil
    public var fill="transparent"
    public var color="#FFFFFF"
    public var image:URL?
    public var selections:[String:[ForegroundRegion]]=[:]
    public init() {}
}
public enum BackgroundEditingEngine {
    public static func render(_ input:URL,settings:BackgroundEditingOptions,token:CancellationToken = .init())throws->CGImage {
        try token.check()
        let source=try ImageEngine.load(input),width=source.width,height=source.height
        guard width<=30000,height<=30000,width*height<=100_000_000 else {throw LokumError.invalidInput("This image exceeds 100 MP or 30,000 pixels.")}
        let regions=settings.selections[input.path] ?? settings.regions
        let automatic=settings.automatic || (settings.automaticInputs?.contains(input.path) ?? false)
        guard automatic || regions.contains(where:{!$0.subtract}) else {throw LokumError.invalidInput("Select an area to keep or use automatic removal.")}
        guard regions.count<=256 else {throw LokumError.invalidInput("Use at most 256 selection areas.")}
        for region in regions {
            guard (3...4096).contains(region.points.count),region.points.allSatisfy({$0.x.isFinite && $0.y.isFinite && $0.x>=0 && $0.x<=1 && $0.y>=0 && $0.y<=1}) else {throw LokumError.invalidInput("Keep selection points inside the image.")}
        }
        var pixels=try rgba(source)
        let originalAlpha=stride(from:3,to:pixels.count,by:4).map{pixels[$0]}
        let mask=automatic ? try automaticMask(source,token:token):nil
        for y in 0..<height {
            try token.check()
            for x in 0..<width {
                let index=y*width+x
                pixels[index*4+3]=mask.map{UInt8((Double(originalAlpha[index])*sample($0,width:320,height:320,x:x,y:y,targetWidth:width,targetHeight:height)).rounded())} ?? 0
            }
        }
        for region in regions {try apply(region,to:&pixels,alpha:originalAlpha,width:width,height:height,token:token)}
        if settings.fill != "transparent" {
            guard ["color","image"].contains(settings.fill) else {throw LokumError.invalidInput("Choose a supported background type.")}
            var background:[UInt8]
            if settings.fill=="color" {
                guard settings.color.range(of:"^#[0-9a-fA-F]{6}$",options:.regularExpression) != nil else {throw LokumError.invalidInput("Choose a color in #RRGGBB format.")}
                let color=NSColor(cgColor:ImageEngine.color(settings.color))!.usingColorSpace(.sRGB)!
                let value=[UInt8((color.redComponent*255).rounded()),UInt8((color.greenComponent*255).rounded()),UInt8((color.blueComponent*255).rounded()),UInt8(255)]
                background=[UInt8](repeating:255,count:pixels.count)
                for index in stride(from:0,to:background.count,by:4) {for channel in 0..<3 {background[index+channel]=value[channel]}}
            } else {
                guard let url=settings.image,url.isFileURL else {throw LokumError.invalidInput("Choose a background image.")}
                let image=try ImageEngine.load(url)
                let scale=max(Double(width)/Double(image.width),Double(height)/Double(image.height))
                let fitted=try ImageEngine.resized(image,width:max(width,Int(ceil(Double(image.width)*scale))),height:max(height,Int(ceil(Double(image.height)*scale))))
                guard let cropped=fitted.cropping(to:CGRect(x:(fitted.width-width)/2,y:(fitted.height-height)/2,width:width,height:height)) else {throw LokumError.failed("The background image could not be fitted.")}
                background=try rgba(cropped)
            }
            for index in 0..<(width*height) {
                if index%width==0 {try token.check()}
                let offset=index*4,foreground=Double(pixels[offset+3])/255,back=Double(background[offset+3])/255
                let alpha=foreground+back*(1-foreground)
                for channel in 0..<3 {pixels[offset+channel]=alpha>0 ? UInt8(min(255,max(0,((Double(pixels[offset+channel])*foreground+Double(background[offset+channel])*back*(1-foreground))/alpha).rounded()))):0}
                pixels[offset+3]=UInt8((alpha*255).rounded())
            }
        }
        try token.check()
        return try image(pixels,width:width,height:height)
    }
    public static func export(_ inputs:[URL],options:OperationOptions,token:CancellationToken)throws->[URL] {
        guard let settings=options.backgroundEditing else {throw LokumError.invalidInput("Select an area to keep or use automatic removal.")}
        var results:[URL]=[]
        for input in inputs {
            try token.check()
            let result=try render(input,settings:settings,token:token)
            let suffix=settings.fill=="transparent" ? " Cutout":" Background"
            let output=OutputFiles.unique(for:input,extension:"png",suffix:suffix,directory:options.outputDirectory)
            try ImageEngine.write(result,to:output);results.append(output)
        }
        return results
    }
    private static func apply(_ region:ForegroundRegion,to pixels:inout [UInt8],alpha:[UInt8],width:Int,height:Int,token:CancellationToken)throws {
        let points=region.points
        let first=max(0,Int(floor(points.map(\.y).min()!*Double(height))))
        let last=min(height,Int(ceil(points.map(\.y).max()!*Double(height))))
        for row in first..<last {
            try token.check()
            var coverage=[Double](repeating:0,count:width)
            for sample in 0..<4 {
                let y=(Double(row)+(Double(sample)+0.5)/4)/Double(height)
                var crossings:[Double]=[]
                for index in points.indices {
                    let a=points[index],b=points[(index+1)%points.count]
                    if (a.y>y)==(b.y>y) {continue}
                    crossings.append((a.x+(y-a.y)*(b.x-a.x)/(b.y-a.y))*Double(width))
                }
                crossings.sort()
                for index in stride(from:0,to:crossings.count-1,by:2) {
                    let left=min(Double(width),max(0,crossings[index])),right=min(Double(width),max(0,crossings[index+1]))
                    let lower=Int(floor(left)),upper=min(width,Int(ceil(right)))
                    if lower>=upper {continue}
                    for x in lower..<upper {coverage[x]+=max(0,min(Double(x+1),right)-max(Double(x),left))/4}
                }
            }
            for x in 0..<width {
                let index=row*width+x,amount=min(1,max(0,coverage[x])),offset=index*4+3
                pixels[offset]=region.subtract ? UInt8((Double(pixels[offset])*(1-amount)).rounded()):max(pixels[offset],UInt8((Double(alpha[index])*amount).rounded()))
            }
        }
    }
    private static func rgba(_ image:CGImage)throws->[UInt8] {
        var pixels=[UInt8](repeating:0,count:image.width*image.height*4)
        try pixels.withUnsafeMutableBytes { buffer in
            guard let context=CGContext(data:buffer.baseAddress,width:image.width,height:image.height,bitsPerComponent:8,bytesPerRow:image.width*4,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else {throw LokumError.failed("Could not read image pixels.")}
            context.draw(image,in:CGRect(x:0,y:0,width:image.width,height:image.height))
        }
        for index in stride(from:0,to:pixels.count,by:4) {
            let alpha=Int(pixels[index+3])
            for channel in 0..<3 {pixels[index+channel]=alpha>0 ? UInt8(min(255,(Int(pixels[index+channel])*255+alpha/2)/alpha)):0}
        }
        return pixels
    }
    private static func image(_ pixels:[UInt8],width:Int,height:Int)throws->CGImage {
        let data=Data(pixels),provider=CGDataProvider(data:data as CFData)!
        guard let image=CGImage(width:width,height:height,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:width*4,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.last.rawValue | CGBitmapInfo.byteOrder32Big.rawValue),provider:provider,decode:nil,shouldInterpolate:true,intent:.defaultIntent) else {throw LokumError.failed("Could not create the background result.")}
        return image
    }
    private static func sample(_ mask:[Float],width:Int,height:Int,x:Int,y:Int,targetWidth:Int,targetHeight:Int)->Double {
        let sx=min(Double(width-1),max(0,(Double(x)+0.5)*Double(width)/Double(targetWidth)-0.5))
        let sy=min(Double(height-1),max(0,(Double(y)+0.5)*Double(height)/Double(targetHeight)-0.5))
        let x0=Int(sx),y0=Int(sy),x1=min(width-1,x0+1),y1=min(height-1,y0+1),fx=sx-Double(x0),fy=sy-Double(y0)
        return min(1,max(0,(Double(mask[y0*width+x0])*(1-fx)+Double(mask[y0*width+x1])*fx)*(1-fy)+(Double(mask[y1*width+x0])*(1-fx)+Double(mask[y1*width+x1])*fx)*fy))
    }
    private static func automaticMask(_ source:CGImage,token:CancellationToken)throws->[Float] {
        guard let model=Bundle.module.url(forResource:"u2netp",withExtension:"onnx",subdirectory:"Resources") else {throw LokumError.missingDependency("Bundled background removal model")}
        let digest=SHA256.hash(data:try Data(contentsOf:model)).map{String(format:"%02x",$0)}.joined()
        guard digest=="309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8" else {throw LokumError.failed("The bundled background removal model failed its integrity check.")}
        let small=try rgba(ImageEngine.resized(source,width:320,height:320)),count=320*320
        var maximum=1
        for index in stride(from:0,to:small.count,by:4) {for channel in 0..<3 {maximum=max(maximum,Int(small[index+channel]))}}
        var values=[Float](repeating:0,count:count*3)
        let means:[Float]=[0.485,0.456,0.406],deviations:[Float]=[0.229,0.224,0.225]
        for index in 0..<count {for channel in 0..<3 {values[channel*count+index]=(Float(small[index*4+channel])/Float(maximum)-means[channel])/deviations[channel]}}
        try token.check()
        let environment=try ORTEnv(loggingLevel:.warning),settings=try ORTSessionOptions()
        try settings.setIntraOpNumThreads(2)
        let session=try ORTSession(env:environment,modelPath:model.path,sessionOptions:settings)
        guard let input=try session.inputNames().first,let output=try session.outputNames().first else {throw LokumError.failed("The background removal model has no image input or output.")}
        let data=values.withUnsafeBytes{NSMutableData(bytes:$0.baseAddress!,length:$0.count)}
        let tensor=try ORTValue(tensorData:data,elementType:.float,shape:[1,3,320,320])
        let result=try session.run(withInputs:[input:tensor],outputNames:Set([output]),runOptions:nil)
        try token.check()
        guard let value=result[output] else {throw LokumError.failed("The model returned no foreground mask.")}
        let raw=try value.tensorData()
        guard raw.length==count*MemoryLayout<Float>.stride else {throw LokumError.failed("The model returned an invalid foreground mask.")}
        var mask=[Float](repeating:0,count:count)
        mask.withUnsafeMutableBytes{raw.getBytes($0.baseAddress!,length:raw.length)}
        guard mask.allSatisfy(\.isFinite),let minimum=mask.min(),let maximum=mask.max() else {throw LokumError.failed("The model returned non-finite mask pixels.")}
        let range=maximum-minimum
        for index in mask.indices {mask[index]=min(1,max(0,range>0.000001 ? (mask[index]-minimum)/range:mask[index]))}
        return mask
    }
}
