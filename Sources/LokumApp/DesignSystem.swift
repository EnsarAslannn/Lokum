import AppKit
import SwiftUI

// One shared visual language, with native light/dark appearance and motion preferences.
enum LokumPalette {
    static func adaptive(_ light:(Double,Double,Double),_ dark:(Double,Double,Double))->NSColor {
        NSColor(name:nil) { appearance in
            let rgb=appearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua ? dark:light
            return NSColor(srgbRed:rgb.0,green:rgb.1,blue:rgb.2,alpha:1)
        }
    }
    static let canvas=Color(nsColor:adaptive((1,0.9686,0.9569),(0.1255,0.1020,0.1451)))
    static let surface=Color(nsColor:adaptive((1,0.9882,0.9804),(0.1686,0.1373,0.1961)))
    static let inset=Color(nsColor:adaptive((0.9686,0.9137,0.9059),(0.1451,0.1176,0.1686)))
    static let text=Color(nsColor:adaptive((0.2392,0.1529,0.2118),(0.9765,0.9294,0.9529)))
    static let muted=Color(nsColor:adaptive((0.4549,0.3608,0.4275),(0.7725,0.6784,0.7412)))
    static let forest=Color(nsColor:adaptive((0.4549,0.2392,0.3490),(0.9059,0.6588,0.7647)))
    static let sand=Color(nsColor:adaptive((0.9686,0.8706,0.8980),(0.2863,0.1882,0.2471)))
    static let sage=Color(nsColor:adaptive((0.8824,0.9216,0.8667),(0.2039,0.2784,0.2078)))
    static let border=Color(nsColor:adaptive((0.9098,0.8275,0.8549),(0.2941,0.2353,0.3137)))
    static let gold=LokumArtwork.gold
    // Selected wheel petals share the button's raspberry background.
    static let ink=NSColor.white
    static let goldInk=Color(nsColor:adaptive((0.5255,0.1686,0.3020),(0.9569,0.6863,0.8039)))
    static let buttonInk=Color.white
    static let goldEnd=Color(red:0.7451,green:0.2902,blue:0.4471)
}
extension Color { static let lokum=Color(nsColor:LokumPalette.gold) }

enum LokumMotion {
    static var enabled:Bool { !UserDefaults.standard.bool(forKey:"reduceAnimations") && !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion }
}
enum LokumAppearance {
    @MainActor static func apply(_ choice:String?=nil) {
        let choice=choice ?? UserDefaults.standard.string(forKey:"appAppearance") ?? "system"
        NSApp.appearance=choice=="light" ? NSAppearance(named:.aqua):choice=="dark" ? NSAppearance(named:.darkAqua):nil
    }
}

struct LokumMotionModifier<Value:Equatable>:ViewModifier {
    let value:Value
    @Environment(\.accessibilityReduceMotion) private var systemReduced
    @AppStorage("reduceAnimations") private var reduced=false
    func body(content:Content)->some View { content.animation(systemReduced || reduced ? nil:.spring(response:0.32,dampingFraction:0.88),value:value) }
}
struct LokumEntrance:ViewModifier {
    var delay=0.0
    @State private var visible=false
    @Environment(\.accessibilityReduceMotion) private var systemReduced
    @AppStorage("reduceAnimations") private var reduced=false
    func body(content:Content)->some View {
        content.opacity(visible ? 1:0).offset(y:visible || systemReduced || reduced ? 0:8)
            .onAppear {
                if systemReduced || reduced { visible=true }
                else { withAnimation(.easeOut(duration:0.24).delay(delay)) { visible=true } }
            }
    }
}
extension View {
    func lokumAnimation<V:Equatable>(value:V)->some View { modifier(LokumMotionModifier(value:value)) }
    func lokumEntrance(delay:Double=0)->some View { modifier(LokumEntrance(delay:delay)) }
    func lokumCard(radius:CGFloat=22)->some View {
        background(LokumPalette.surface,in:RoundedRectangle(cornerRadius:radius,style:.continuous))
            .overlay(RoundedRectangle(cornerRadius:radius,style:.continuous).strokeBorder(LokumPalette.border,lineWidth:1))
    }
}

enum LokumButtonKind { case primary,secondary,quiet }
struct LokumButtonStyle:ButtonStyle {
    var kind:LokumButtonKind = .secondary
    var compact=false
    init(_ kind:LokumButtonKind = .secondary,compact:Bool=false) { self.kind=kind;self.compact=compact }
    func makeBody(configuration:Configuration)->some View { LokumButtonBody(configuration:configuration,kind:kind,compact:compact) }
}
private struct LokumButtonBody:View {
    let configuration:ButtonStyleConfiguration
    let kind:LokumButtonKind
    let compact:Bool
    @State private var hovered=false
    @Environment(\.isEnabled) private var enabled
    @Environment(\.isFocused) private var focused
    @Environment(\.accessibilityReduceMotion) private var systemReduced
    @AppStorage("reduceAnimations") private var reduced=false
    private var primary:Bool { kind == .primary }
    var body:some View {
        configuration.label.font(.system(size:compact ? 11:13,weight:.semibold,design:.rounded))
            .multilineTextAlignment(.center)
            .fixedSize(horizontal:false,vertical:true)
            .frame(minHeight:compact ? 14:18,alignment:.center)
            .padding(.horizontal,compact ? 10:15).padding(.vertical,compact ? 7:11)
            .foregroundStyle(primary ? LokumPalette.buttonInk:LokumPalette.text)
            .background {
                RoundedRectangle(cornerRadius:compact ? 10:13,style:.continuous)
                    .fill(primary ? AnyShapeStyle(LinearGradient(colors:[Color.lokum,LokumPalette.goldEnd],startPoint:.leading,endPoint:.trailing)):AnyShapeStyle(kind == .quiet ? Color.clear:LokumPalette.surface))
                    .overlay(RoundedRectangle(cornerRadius:compact ? 10:13,style:.continuous).strokeBorder(primary ? Color.white.opacity(0.12):LokumPalette.border,lineWidth:kind == .quiet ? 0:1))
            }
            .overlay(RoundedRectangle(cornerRadius:compact ? 10:13).fill(Color.primary.opacity(hovered && !primary ? 0.035:0)))
            .overlay(RoundedRectangle(cornerRadius:compact ? 10:13).strokeBorder(focused ? LokumPalette.goldInk:Color.clear,lineWidth:2).padding(-3))
            .shadow(color:primary && enabled ? Color.lokum.opacity(hovered ? 0.20:0.10):.clear,radius:hovered ? 9:5,y:3)
            .scaleEffect(systemReduced || reduced ? 1:configuration.isPressed ? 0.97:hovered && enabled ? 1.015:1)
            .opacity(enabled ? 1:0.4).contentShape(RoundedRectangle(cornerRadius:13))
            .onHover { hovered=$0 }
            .animation(systemReduced || reduced ? nil:.easeOut(duration:0.14),value:configuration.isPressed)
            .animation(systemReduced || reduced ? nil:.easeOut(duration:0.16),value:hovered)
    }
}
struct LokumGroupBoxStyle:GroupBoxStyle {
    func makeBody(configuration:Configuration)->some View {
        VStack(alignment:.leading,spacing:16) {
            configuration.label.font(.system(size:13,weight:.semibold,design:.rounded)).foregroundStyle(LokumPalette.forest)
            configuration.content
        }.padding(20).frame(maxWidth:.infinity,alignment:.leading).lokumCard()
    }
}

struct LokumMark:View {
    var size:CGFloat=44
    var happy=false
    var body:some View {
        Image(nsImage:LokumArtwork.mark(size:size*2,happy:happy)).resizable().interpolation(.high).frame(width:size,height:size).accessibilityHidden(true)
    }
}

struct LokumPill:View {
    let title:String
    var symbol:String?=nil
    var color:Color=LokumPalette.forest
    var body:some View {
        HStack(spacing:5) { if let symbol { Image(systemName:symbol) };LText(title) }
            .font(.system(size:10,weight:.semibold,design:.rounded)).foregroundStyle(color == .lokum ? LokumPalette.goldInk:color)
            .padding(.horizontal,9).padding(.vertical,5).background(color.opacity(0.09),in:Capsule())
    }
}
struct LokumPageHeading:View {
    let eyebrow:String
    let title:String
    let subtitle:String
    var body:some View {
        VStack(alignment:.leading,spacing:8) {
            LText(eyebrow.uppercased()).font(.system(size:10,weight:.bold,design:.rounded)).tracking(1.4).foregroundStyle(LokumPalette.goldInk)
            LText(title).font(.system(size:27,weight:.bold,design:.rounded)).tracking(-0.7).foregroundStyle(LokumPalette.text)
            LText(subtitle).font(.system(size:12)).foregroundStyle(LokumPalette.muted).fixedSize(horizontal:false,vertical:true)
        }
    }
}
struct LokumCheck:View {
    @State private var arrived=false
    var body:some View {
        Image(systemName:"checkmark").font(.system(size:14,weight:.bold)).foregroundStyle(LokumPalette.forest)
            .frame(width:34,height:34).background(LokumPalette.sage,in:Circle())
            .scaleEffect(arrived ? 1:0.75).opacity(arrived ? 1:0)
            .lokumAnimation(value:arrived).onAppear{arrived=true}.accessibilityHidden(true)
    }
}
