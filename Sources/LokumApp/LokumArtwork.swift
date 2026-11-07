import AppKit

enum LokumArtwork {
    static let gold=NSColor(srgbRed:0.7137,green:0.2471,blue:0.4078,alpha:1)
    static let ink=NSColor(srgbRed:0.2392,green:0.1529,blue:0.2118,alpha:1)
    static func bodyPath()->CGPath {
        CGPath(roundedRect:CGRect(x:0.17,y:0.25,width:0.66,height:0.60),cornerWidth:0.16,cornerHeight:0.16,transform:nil)
    }
    static func pistachioPath()->CGPath {
        var rotation=CGAffineTransform(translationX:0.57,y:0.17).rotated(by:-25 * .pi / 180)
        return CGPath(roundedRect:CGRect(x:-0.10,y:-0.038,width:0.20,height:0.076),cornerWidth:0.038,cornerHeight:0.038,transform:&rotation)
    }
    static func draw(in rect:NSRect,happy:Bool=false,monochrome:Bool=false) {
        guard let c=NSGraphicsContext.current?.cgContext else { return }
        c.saveGState();defer{c.restoreGState()}
        c.translateBy(x:rect.minX,y:rect.maxY);c.scaleBy(x:rect.width,y:-rect.height)
        if monochrome {
            c.setFillColor(NSColor.black.cgColor);c.addPath(bodyPath());c.fillPath();c.addPath(pistachioPath());c.fillPath()
            return
        }
        c.saveGState();c.addPath(bodyPath());c.clip()
        let colors=[NSColor(srgbRed:0.9569,green:0.6824,blue:0.7725,alpha:1).cgColor,NSColor(srgbRed:0.7961,green:0.3373,blue:0.4824,alpha:1).cgColor] as CFArray
        let gradient=CGGradient(colorsSpace:CGColorSpace(name:CGColorSpace.sRGB),colors:colors,locations:[0,1])!
        c.drawLinearGradient(gradient,start:CGPoint(x:0.25,y:0.30),end:CGPoint(x:0.76,y:0.85),options:[.drawsBeforeStartLocation,.drawsAfterEndLocation])
        let top=CGMutablePath();top.move(to:CGPoint(x:0.17,y:0.25));top.addLine(to:CGPoint(x:0.83,y:0.25));top.addLine(to:CGPoint(x:0.72,y:0.40));top.addQuadCurve(to:CGPoint(x:0.28,y:0.40),control:CGPoint(x:0.50,y:0.45));top.closeSubpath()
        c.setFillColor(NSColor(srgbRed:1,green:0.8980,blue:0.9137,alpha:0.60).cgColor);c.addPath(top);c.fillPath()
        let side=CGMutablePath();side.move(to:CGPoint(x:0.73,y:0.39));side.addLine(to:CGPoint(x:0.83,y:0.25));side.addLine(to:CGPoint(x:0.83,y:0.85));side.addLine(to:CGPoint(x:0.73,y:0.85));side.closeSubpath()
        c.setFillColor(gold.withAlphaComponent(0.18).cgColor);c.addPath(side);c.fillPath()
        c.setFillColor(NSColor.white.withAlphaComponent(0.68).cgColor)
        for point in [CGPoint(x:0.30,y:0.33),CGPoint(x:0.43,y:0.29),CGPoint(x:0.61,y:0.34),CGPoint(x:0.69,y:0.46),CGPoint(x:0.25,y:0.58),CGPoint(x:0.35,y:0.75),CGPoint(x:0.66,y:0.74),CGPoint(x:0.57,y:0.79),CGPoint(x:0.75,y:0.63)] {
            c.addPath(CGPath(roundedRect:CGRect(x:point.x,y:point.y,width:0.022,height:0.018),cornerWidth:0.006,cornerHeight:0.006,transform:nil));c.fillPath()
        }
        c.restoreGState()
        c.setFillColor(NSColor(srgbRed:0.4824,green:0.5882,blue:0.3373,alpha:1).cgColor);c.addPath(pistachioPath());c.fillPath()
        c.setFillColor(ink.cgColor)
        for x:CGFloat in [0.38,0.58] {
            c.addPath(CGPath(roundedRect:CGRect(x:x,y:0.52,width:0.04,height:happy ? 0.025:0.055),cornerWidth:0.02,cornerHeight:0.02,transform:nil));c.fillPath()
        }
        c.setStrokeColor(ink.cgColor);c.setLineWidth(0.025);c.setLineCap(.round)
        c.move(to:CGPoint(x:0.455,y:0.645));c.addQuadCurve(to:CGPoint(x:0.545,y:0.645),control:CGPoint(x:0.50,y:happy ? 0.715:0.695));c.strokePath()
        c.setFillColor(NSColor(srgbRed:0.7137,green:0.2471,blue:0.4078,alpha:0.20).cgColor)
        for x:CGFloat in [0.30,0.65] { c.fillEllipse(in:CGRect(x:x,y:0.61,width:0.05,height:0.025)) }
    }
    static func mark(size:CGFloat,happy:Bool=false)->NSImage {
        NSImage(size:NSSize(width:size,height:size),flipped:false) { rect in draw(in:rect,happy:happy);return true }
    }
    static func menuIcon(updateAvailable:Bool=false)->NSImage {
        let image=NSImage(size:NSSize(width:20,height:20),flipped:false) { rect in
            draw(in:rect.insetBy(dx:0.5,dy:0.5),monochrome:true)
            if updateAvailable { NSColor.black.setFill();NSBezierPath(ovalIn:NSRect(x:15,y:15,width:4,height:4)).fill() }
            return true
        }
        image.isTemplate=true;image.accessibilityDescription=updateAvailable ? "Lokum update available":"Lokum";return image
    }
}
