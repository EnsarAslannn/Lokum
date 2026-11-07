import SwiftUI
import LokumCore

struct AnnotationDraftOverlay:View {
    let draft:ImageAnnotation
    let source:CGImage
    let imageSize:CGSize
    let origin:CGPoint
    let ratio:CGFloat
    var body:some View {
        Canvas { context,_ in
            context.withCGContext { ctx in
                ctx.translateBy(x:origin.x,y:origin.y+imageSize.height*ratio)
                ctx.scaleBy(x:ratio,y:-ratio)
                try? ImageEngine.drawAnnotations([draft],in:ctx,source:source)
            }
        }.blendMode(draft.kind == .highlight ? .multiply:.normal).allowsHitTesting(false)
    }
}
