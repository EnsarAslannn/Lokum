<p align="center"><img src="Resources/Brand/LokumIcon.png" width="128" alt="Lokum"></p>
<h1 align="center">Lokum</h1>
<p align="center">Dosyaların için tatlı bir kolaylık.</p>
<p align="center"><a href="README.md"><strong>🇹🇷 TR</strong></a> | <a href="README.en.md">🇬🇧 EN</a></p>

Lokum; görsel, video, ses ve belge dosyalarını bilgisayarında dönüştürüp düzenleyebildiğin bir masaüstü uygulamasıdır. Dosyanı seç, çarktan bir araç aç, sonucu önizle ve yeni bir kopya kaydet. Gül kurusu, krem ve fıstık yeşili tonlarında açık ve koyu temaları; Türkçe ve İngilizce arayüzü bulunur.

[Türkçe kullanım](README.tr.md) · [Etkileşimli tanıtım kaynakları](docs/tr/index.html) · [Özellik ve platform matrisi](windows/FEATURE-PARITY.md) · [Bağımlılıklar](docs/dependencies.md)

**Proje deposu:** [EnsarAslannn/Lokum](https://github.com/EnsarAslannn/Lokum) · [Hata bildir / öneri paylaş](https://github.com/EnsarAslannn/Lokum/issues)

## Araç çarkları

Seçtiğin dosyanın türüne göre çarkın araçları değişir. Aşağıdaki görüntüler, güncel Windows uygulamasının kendi arayüzünden oluşturulmuştur.

| Görsel araçları | Video araçları |
| :---: | :---: |
| <img src="docs/assets/showcase/lokum-image-wheel-tr.png" width="420" alt="Lokum görsel araçları çarkı"> | <img src="docs/assets/showcase/lokum-video-wheel-tr.png" width="420" alt="Lokum video araçları çarkı"> |

## Çalıştırma

Bu depo kaynak kodunu içerir; derlenmiş uygulama ve indirilen araçlar Git'e eklenmez. Hazır paket yayımlandığında [Releases](https://github.com/EnsarAslannn/Lokum/releases) sayfasından edinilebilir. Elinde `Lokum-Windows-x64.zip` varsa tamamını yazılabilir bir klasöre çıkarıp içindeki `Lokum.exe` dosyasını aç. Paketin tüm dosyalarını birlikte tut. Bağımsız Windows paketinde ayrı bir .NET kurulumu gerekmez. Kaynaktan çalıştırmak için aşağıdaki derleme adımlarını kullan.

İlk açılışta **Kurulumu tamamla → Eksik araçları indir** akışını kullan. FFmpeg/ffprobe, yt-dlp, Deno, LibreOffice ve metadata araçları ilgili işlevler için kullanılır; uygulama bunları kendi kurulum akışından, sabitlenmiş sürüm ve SHA-256 kontrolleriyle edinir. Yerel görsel ve temel PDF işlemleri hemen kullanılabilir. İndirme iptal edilirse veya başarısız olursa aynı ekrandan yeniden denenebilir.

Kütüphane ve ayarlar `%LOCALAPPDATA%/Lokum` altında saklanır.

## Özellikler

- **45 girdi formatı, 27 araç, 275 dönüşüm yolu:** ortak katalog ve seçim kuralları korunur.
- **Görseller:** dönüştürme, sıkıştırma, boyutlandırma, döndürme/yansıtma, kırpma, işaretleme, çerçeve, kolaj, metadata, gizleme ve QR okuma.
- **Windows arka plan düzenleme:** serbest veya dikdörtgen seçimle korunacak alanı belirleme, isteğe bağlı yerel otomatik kesim, şeffaf PNG, düz renk veya görsel arka plan. Tek görsel ve toplu seçim desteklenir.
- **Video ve ses:** dönüştürme, sıkıştırma, görüntü kırpma, bağımsız başlangıç/bitiş aralıklarıyla klip kesme, birleştirme, hız değiştirme, normalizasyon ve kanal ayarları. Windows'ta videonun tamamını veya yalnızca seçili zaman aralığını sessize alıp sonucu önizleyebilirsin. Video sansürü, seçilen kaplama stilini gerçek işlenmiş önizlemede gösterir. [Kullanım](README.tr.md#sık-kullanılan-akışlar).
- **Belgeler ve arşivler:** PDF oluşturma/birleştirme/bölme/düzenleme, Office/Pages/EPUB/tablo/sunum/altyazı dönüşümleri ve arşiv işlemleri.
- **Günlük akış:** araç çarkı, sürükleme kısayolları, pano içe aktarma, Float rafı, kütüphane, klasör otomasyonu ve pencere düzenleri.
- **Web:** desteklenen herkese açık medya bağlantılarından video, ses ve görsel indirme.

İşlevlerin işletim sistemi, dosya yapısı, kurulu codec ve harici araç gereksinimleri [platform matrisinde](windows/FEATURE-PARITY.md) açıklanır. Karmaşık belge dönüşümleri kaynak belgenin bütün özelliklerini korumayabilir. Web indirme desteği ilgili sitenin davranışına bağlıdır.

### Web’den medya indirme

Lokum sistem tepsisinde çalışırken Chrome’da bir görsel veya videonun üzerinde **Ctrl + sağ tık → Medyayı indir** seçeneğini kullan. Sürüklemek gerekmez; Chrome uzantısı zorunlu değildir. Dosya ayarlardaki **İndirilenler** klasörüne kaydedilir ve yerel araçlarla düzenlemek için Lokum’da seçilir. Medya adresi alınamazsa bağlantı yapıştırabileceğin indirme ekranı açılır. [İsteğe bağlı Chrome uzantısı](browser/README.md) da bulunur.

**Sonucu göster**, desteklenen video araçları ve tüm dönüşüm hedeflerinde gerçek çıktıyı kaydetmeden incelemeni sağlar. Video sıkıştırma sonucu hazır olduğunda orijinal ve çıktı boyutu KB olarak görünür; hedef alanları boş bırakılabilir.

## Arka plan kaldırma (Windows)

Görseli seçip **İnce ayar / Fine-tune → Arka Plan Düzenle / Edit Background**
aracını aç. Arka plan kaldırma ve ekleme çarkta tek seçenek altında toplanır.

1. Soldaki görselde korunacak alanın çevresini **Serbest seçim** ile çiz veya
   **Dikdörtgen seçim** ile bir alan sürükle. Seçimin içi korunur, dışı şeffaflaşır.
   Dikdörtgenin içindeki zemin de kalır; nesnenin çevresini takip etmek için serbest
   seçimi veya **Otomatik seç** düğmesini kullan. Birden fazla alan ekleyebilir,
   **Seçimden çıkar** ile iç boşlukları silebilir ve **Ctrl+Z** ile geri alabilirsin.
2. Önizleme hazır olunca sağdaki arka plan seçeneğini kullan: **Şeffaf**, **Düz renk**
   veya **Arka plan görseli**. Düz renkte ton çubuğundan rengi, paletten doygunluk
   ve parlaklığı seç; istersen `#RRGGBB` kodunu gir. Arka plan görseli,
   özgün tuvali dolduracak biçimde ortalanır; kenarlarından kırpılabilir.
3. **Yeni PNG kaydet** ile önizlemenin kopyasını kaydet. Çoklu seçimde üstteki
   dosya listesinden her görselin korunacak alanını ayrı belirle. Arka plan ayarı
   tüm dosyalara uygulanır; tüm seçimler hazır olunca toplu kaydetme açılır.

Çıktılar şeffafsa `Dosya Adı Cutout.png`, arka plan ekliyse
`Dosya Adı Background.png` olarak, ayarlardaki çıktı konumuna kaydedilir.
Özgün dosyaların üzerine yazılmaz; iptal veya hata halinde yarım sonuç yayımlanmaz.

Otomatik seçim modeli ve çalışma motoru Windows paketine dahildir; Python, API anahtarı veya
görsel yüklemesi gerekmez. Windows'un ONNX Runtime bileşeni için
[Microsoft Visual C++ x64 çalışma ortamı](https://onnxruntime.ai/docs/install/#requirements)
gereklidir; eksik olan bilgisayarlarda önce Microsoft'un resmi yeniden dağıtılabilir
paketi kurulmalıdır. Bu bilgisayarda gerçek önizleme ve kayıt testi geçti. Hafif model saç, saydam nesneler
ve karmaşık zeminlerde kusursuz kesim sağlamayabilir; kaydetmeden önce kenarları
incele. Bu araç henüz macOS sürümüne eklenmemiştir.
[Model kaynağı ve sağlama toplamı](Resources/Models/README.md).

## Görsel düzenleme ve kırpma (Windows)

Çarktaki **Görsel Düzenle / Edit Image**; kırpma, boyutlandırma, döndürme,
yansıtma, ışık, renk ve detay ayarlarını aynı pencerede sunar. Ayrı bir görsel
kırpma seçeneği gösterilmez; video kırpma bağımsız kalır.

**Görseli kırp** düğmesine bas, önizlemede alanı sürükleyerek seç ve
**Kırpmayı önizle** düğmesine bas. Kesilmiş hali aynı önizlemede görünür;
**Yeni kopya kaydet** ile kaydet veya **Görseli kırp** ile seçimi tekrar değiştir.
Kırpma seçimi içindeyken kayıt düğmesi kapalıdır; Enter önce kırpmayı önizler.
**Kırpmayı kaldır**, diğer ışık/renk ayarlarını koruyarak tam görsele döner.
Orijinal dosya değişmez. Çoklu görselde aynı oransal kırpma uygulanır.

Diğer ilgili çark adları **Görsel Sansürle** ve **QR Kod Oku** olarak gösterilir.

## Görünüm

| Açık tema | Koyu tema |
| :---: | :---: |
| ![Lokum Windows açık tema](docs/assets/showcase/lokum-windows-light.png) | ![Lokum Windows koyu tema](docs/assets/showcase/lokum-windows-dark.png) |

Ekranlar, Lokum Windows uygulamasının izole görsel denetiminden alınır. macOS kaynakları da yeni tema ve lokum simgesini içerir; macOS çalıştırılabilir paketi bu Windows ortamında derlenmemiştir.

## Kısayollar

| Windows | İşlem |
| --- | --- |
| Ctrl+O | Dosya seç |
| Ctrl+V | Dosya, metin, görsel veya bağlantı yapıştır |
| Ctrl+Space | Seçili dosyaların araç çarkını aç |
| Ctrl+1 / Ctrl+2 | Ana ekran / kütüphane |
| Ctrl+F | Kütüphanede ara |
| Ctrl+Shift+D | Kopyalanmış indirme bağlantısını aç |
| Space / ← / → / I / O | Desteklenen medya editörlerinde oynatma, gezinme ve aralık işaretleri |
| Shift+sürükleme / Enter | Kırpma oranını kilitle / kırpılmış kopyayı kaydet |

macOS'ta Finder sürüklemesi sırasında Shift dönüştürme, Command araçlar ve Control dosya işlemleri çarkını açar. Sürükleme tuşları ayarlardan değiştirilebilir. Metin alanlarında normal yapıştırma ve gezinme korunur.

## Kaynaktan derleme

Önce kaynakları al:

```sh
git clone https://github.com/EnsarAslannn/Lokum.git
cd Lokum
```

Windows için .NET 10 SDK gerekir. PowerShell'de:

```powershell
dotnet restore windows/Lokum.Windows --locked-mode
dotnet publish windows/Lokum.Windows -c Release --no-restore -o build/windows/Lokum
.\build\windows\Lokum\Lokum.exe
```

Windows çıktısı `build/windows/Lokum` klasörüne yazılır. Tüm klasör birlikte dağıtılmalıdır.

macOS için macOS 13+, Apple Silicon ve Xcode/Swift 6 gerekir:

```sh
scripts/setup-exiftool.sh
swift build --product Lokum
swift test
scripts/build-app.sh release
```

macOS çıktısı `build/Lokum.app` konumuna yazılır. macOS kaynaklarının derleme ve çalışma doğrulaması bir Mac üzerinde yapılmalıdır.

## Geliştirme ve doğrulama

```powershell
dotnet run --project windows/Lokum.Verify -c Release
dotnet run --project windows/Lokum.Verify -c Release -- --background-removal-only
dotnet run --project windows/Lokum.Verify -c Release -- --background-editing-only
dotnet run --project windows/Lokum.Verify -c Release -- --image-workspace-only
python scripts/check-feature-parity.py
python scripts/generate-localizations.py --check
```

Windows arayüzünü kullanıcı kütüphanesine dokunmadan denetlemek için, derledikten sonra:

```powershell
.\build\windows\Lokum\Lokum.exe --ui-audit build/ui-audit
```

Bu denetim açık/koyu tema ve Türkçe/İngilizce sahnelerini, ana ekran ile kütüphanedeki düğmelerin tıklama hedeflerini kontrol eder. Gerçek fare, klavye ve dışa aktarma testlerinin tamamının yerini tutmaz. Ayrıntılar: [arayüz denetimi](docs/testing/UI-AUDIT.md) ve [katkı rehberi](CONTRIBUTING.md).

Derleme çıktıları (`build`, `bin`, `obj`), indirilen araçlar, yerel ayarlar ve gizli anahtarlar `.gitignore` ile dışarıda tutulur. Kendi erişim anahtarlarını veya kişisel test dosyalarını depoya ekleme.

## Gizlilik ve güncellemeler

Dönüştürme ve düzenleme yerelde çalışır. Web indirmeleri seçilen siteye, eksik araçların kurulumu ilgili resmi dağıtım kaynağına bağlanır. Analytics, bulut hesabı veya tarayıcı çerezi içe aktarma bulunmaz.

Lokum imzalı güncellemeleri destekler. Bir yayın deposu ve güvenilir imza anahtarı yapılandırılana kadar otomatik güncelleme kanalı bağlı değildir. [Kanal yapılandırma notları](docs/LOKUM.md) yayınlayan kişi içindir.

## Lisans

[MIT lisansı](LICENSE). Proje sorumlusu: [EnsarAslannn](https://github.com/EnsarAslannn).

Harici araç ve kütüphanelerin kendi lisansları geçerlidir: [macOS bildirimleri](Resources/THIRD-PARTY-NOTICES.txt), [Windows bildirimleri](windows/THIRD-PARTY-NOTICES.txt).
