# Lokum · Kullanım

[🇹🇷 TR](README.md) | [🇬🇧 EN](README.en.md)

Lokum; görsel, video, ses ve belgeleri yerelde dönüştürüp düzenleyebildiğin bir dosya araç kutusudur. Tam kurulum ve derleme bilgileri [ana rehberdedir](README.md).

## Başlangıç

**Web’de Ctrl + sağ tık → Medyayı indir:** Lokum’un küçük indirme menüsü açılır.
Lokum sistem tepsisinde açıkken Ctrl tuşunu basılı tutup sağ tuşa bir kez basıp
bırak; sürüklemek gerekmez. “Sürüklemeye devam et” yazısı ayrı sürükleme çarkıdır.
Chrome uzantısı gerekmez. Dosya ayarlardaki **İndirilenler** klasörüne kaydedilir
ve Lokum’da seçilir; ardından bilgisayarındaki dosyalarla aynı araçları kullanırsın.
Tarayıcı bağlantıyı paylaşmıyorsa bağlantı yapıştırabileceğin indirme ekranı açılır.
Korumalı veya giriş gerektiren bazı medyalar indirilemeyebilir.

macOS'ta Finder dosyalarında veya tarayıcı medyasında Control ile tıklayabilirsin.
Başka uygulamanın menüsünden medya adresi okumak için **Ayarlar → Pencere yönetimi →
İzin ayarlarını aç** üzerinden Lokum'a Erişilebilirlik izni ver. Bağlantı alınamıyorsa
**Bağlantı indir** ekranına kendin yapıştırabilirsin. macOS'ta uygulama kısayolları
genellikle Ctrl yerine Command kullanır; platform eşlemesi [buradadır](docs/DESKTOP-PARITY.md#shortcut-mapping).

Explorer/masaüstündeki dosyalar için mevcut Ctrl kısayolları çalışmaya devam eder.
Doğrudan web araç çarkı isteyenler için Chrome uzantısı isteğe bağlıdır:
**Ayarlar → Chrome uzantısı (isteğe bağlı)**. [Ayrıntılar](browser/README.md).

1. Hazır paket yayımlandığında [Releases](https://github.com/EnsarAslannn/Lokum/releases) sayfasından edin. Elindeki `Lokum-Windows-x64.zip` paketinin tamamını yazılabilir bir klasöre çıkar. Kaynak kodunu indirdiysen önce [derleme adımlarını](README.md#kaynaktan-derleme) uygula.
2. Çıkardığın veya derlediğin klasördeki `Lokum.exe` dosyasını aç. Paketin tüm dosyalarını birlikte tut.
3. **Kurulumu tamamla** ekranından eksik medya, Office ve metadata araçlarını indir. Uygulama indirilen dosyaların SHA-256 değerlerini doğrular.
4. **Dosya seç** veya Ctrl+O ile dosyalarını ekle; Ctrl+V ile panodaki dosya, metin ve görselleri de içe aktarabilirsin.
5. Ctrl+Space ile çarkı aç, dönüştürme biçimini veya aracı seç ve yeni kopyayı kaydet.

Dışa aktarımlar kaynak dosyanın üzerine yazmaz; mevcut çıktı adları da korunur. **Taşı** dosya işlemi ise seçilen orijinali taşır.

## Sık kullanılan akışlar

- **Otomatik altyazı:** önce videonun ana dilini seç, transkriptini oluştur ve yanlış sözcükleri veya isimleri düzelt. Ardından Türkçe veya İngilizce altyazı seç; kontrol ettiğin transkript dile özel modellerle bilgisayarında çevrilsin. Sonucu önizleyip düzenle; SRT/TXT/VTT veya altyazılı MP4 kaydet. Konuşma modeli (148 MB) ve gereken çeviri modelleri bir kez indirilir. Ücretli API gerekmez; otomatik sonuçları kontrol etmelisin.
- **Dönüştürme ve sonuç önizleme:** çarktaki **Dönüştür** girişinden veya ana ekrandaki dönüştürme menüsünden biçimi seç. **Sonucu göster**, seçilen biçimde gerçek bir geçici dosya üretir. Video/sesi oynatabilir, görselleri ve PDF sayfalarını görebilir, belge/metin içeriklerini veya arşiv dosya listesini inceleyebilirsin. Çoklu dosyada önizlemedeki oklarla gez. Önizlemeyi kapatıp ayarları değiştirirsen yeniden **Sonucu göster** düğmesine bas. Sonuç biçimi, toplam KB ve dosya sayısı editörde görünür kalır. **Yeni kopya kaydet** ayrı adımdır; önizleme dosyaları Kütüphane'ye veya çıktı klasörüne eklenmez. Belge önizlemeleri için Office araçlarının, medya için medya araçlarının kurulu olması gerekir.
- **Video sıkıştırma sonucu (Windows):** hedef boyut ve küçültme yüzdesini boş/0 bıraktığında da **Sonucu göster** sonrasında **Sonuç: … KB · Orijinal: … KB** bilgisi görünür. Gösterilen boyut gerçek sıkıştırılmış dosyanın boyutudur.

- **Arka plan düzenleme:** **İnce ayar → Arka Plan Düzenle** tek editörü açar. Önce serbest/dikdörtgen seçimle korunacak alanı belirle; seçimin dışı şeffaflaşır. İstersen **Otomatik seç** kullan, alan ekle veya **Seçimden çıkar** ile düzelt. Ardından şeffaf, paletten düz renk veya görsel arka plan seçip **Yeni PNG kaydet** düğmesine bas. Çoklu seçimde her görselin alanını ayrı belirle. Özgün dosyalar korunur; model yerelde çalışır.
- **Görsel düzenleme ve kırpma:** çarktaki **Görsel Düzenle** içinde **Görseli kırp → alan seç → Kırpmayı önizle** akışını kullan. Kesilmiş hali görüp kaydet veya seçimi yeniden düzenle. **Kırpmayı kaldır** diğer düzenlemeleri korur. Ayrı görsel kırpma seçeneği kaldırılmıştır.
- **İşaretleme:** kalem, marker, şekil, ok veya metin aracını seç. Seç/Taşı ile düzenle; Ctrl+Z son çizimi geri alır.
- **Görsel sansürü:** çarktan **Görsel Sansürle** aracını açıp gizlemek istediğin alanı sürükle. Varsayılan stil **Bulanıklaştır**; sonucu önizlemede görürsün. Birden fazla alan seçebilir, **Alanı geri al** ile son seçimi kaldırabilirsin. **Yeni kopya kaydet** bulanıklaştırılmış bir PNG oluşturur; özgün dosya korunur. Düz kaplama ve pikselleştirme de kaplama stili listesindedir.
- **Videoyu Kes:** klip sayısını girip **Klip sayısını uygula** düğmesine bas. Listeden her klibi ayrı seç ve başlangıç/bitişini tutamaçlarla veya saniye alanlarıyla belirle. Örneğin ilk klip 1–3, ikinci 4–5 saniye olabilir; bir klibi değiştirmek diğerini etkilemez. Aralıklar örtüşebilir ve kaynak videodaki zaman sırasından farklı seçilebilir. **Sonucu oynat**, yalnızca seçilen aralıkları listedeki sırayla önizler; aradaki boşlukları eklemez. Klipleri ayrı kaydet veya **Kalan klipleri tek videoda birleştir** seçeneğini kullan. Yeni klip sayısını uygulamak mevcut aralıkları sıfırlar. Ayrı bölme ve kare alma çark seçenekleri kaldırılmıştır. Görüntünün alanını değiştiren **Videoyu Kırp** ayrı kalır.
- **Video sesini kaldırma:** **Videoyu Sessize Al** varsayılan olarak tüm ses kanalını kaldırır. **Yalnızca seçili aralığın sesini kaldır** seçeneğini açıp örneğin 2–4 saniye aralığını belirle; öncesi ve sonrası sesli kalır. **Sonucu önizle** ile işlenmiş videoyu dinleyip izle, ardından yeni kopyayı kaydet.
- **Video sansürü:** önceki alan çizme ve kaydetme akışı kullanılır. Zaman aralığı ve alan seçip kaplama stilini belirle; **Sonucu göster** düğmesiyle işlenmiş videoyu izle. Seçim üzerindeki siyah bant alanı belirtir; sonuç seçtiğin düz kaplama, bulanıklık veya pikselleştirme stilini uygular. Yeni alan çizmek için **Orijinal** düğmesine bas.
- **Video sonucu önizleme:** **Videoyu Sansürle**, **Sıkıştır**, **Video Hızını Değiştir** ve **Videoyu Kırp** ekranlarının altındaki sabit **Sonucu göster** düğmesi, kaydedilecek işlemi uygulayıp sonucu oynatır. **Orijinal** ile kaynağa dönüp düzenlemeye devam et. Ayarları değiştirirsen düğmeye tekrar basarak sonucu yenile. Otomatik önizleme veya otomatik kayıt yapılmaz; **Yeni kopya kaydet** ayrı adımdır. Sıkıştırma önizlemesi gerçek boyut sınırına göre üretilir; sonuç ikinci kez sıkıştırılmaz. MOV/MKV sonuçları yalnızca oynatma kabına aktarılır, tekrar kodlanmaz.
- **Ses kesme:** Space ile önizlemeyi oynat, sağ/sol oklarla gezin, I/O ile başlangıç ve bitişi işaretle ve aralığı yeni dosya olarak kaydet.
- **PDF:** dosyaları seçip birleştir veya tek PDF'nin sayfalarını böl, sırala ve döndür.
- **Meta veriler:** üstte dosyanın türü, piksel ölçüleri ve boyutu; altında kayıtlı düzenlenebilir alanlar görünür. Ekran görüntüsünde bu alanlar yoksa bir açıklama gösterilir. **Meta veri alanı ekle** bölümünden başlık, yazar, açıklama veya telif hakkı bilgisi ekleyebilir; **Tüm meta veriler** bölümünden kamera ve varsa konum dahil okunan tüm ayrıntıları inceleyebilirsin. Paylaşmadan önce **Tüm meta verileri kaldır** ile kişisel bilgileri temizleyebilirsin. Tek görsel/PDF seçildiğinde düzenleyici açılır; birden fazla görsel seçildiğinde toplu temizleme uygulanır. Değişiklikler yeni bir kopyaya kaydedilir, orijinal korunur. Ekran görüntülerinde kamera/GPS bilgisi bulunmayabilir; meta veri temizleme görüntüde görünen yazıları silmez. Türkçe dosya ve klasör adları desteklenir.
- **Web indirme:** herkese açık, desteklenen medya bağlantısını **Bağlantı indir** ekranına yapıştır. Ctrl+Shift+D kopyalanmış bağlantıyı açar.
- **Kütüphane:** Ctrl+2 ile önceki çıktıları aç; Ctrl+F ile ara ve dosyayı yeniden kullan. Veriler `%LOCALAPPDATA%/Lokum` konumundadır.

## Görünüm ve platformlar

Ayarlar → Genel bölümünden Sistem, Açık ve Koyu görünümü seçebilirsin. Sürükleme kısayolları, çark boyutu, geri bildirim ve çıktı klasörleri de ayarlanabilir.

Windows paketi yerel WPF uygulamasıdır ve kendi .NET çalışma ortamını içerir. macOS uygulaması SwiftUI/AppKit kullanır; macOS paketi oluşturmak için bir Mac üzerinde [derleme adımlarını](README.md#kaynaktan-derleme) uygula. [Platform matrisi](windows/FEATURE-PARITY.md) destek ve farklılıkları listeler.

## Güncellemeler

Lokum için ayrı bir yayın deposu ve imza anahtarı tanımlanana kadar güncelleme kanalı bağlı değildir. İmzalı güncelleme altyapısı kaynakta korunur; [kanal yapılandırma notları](docs/LOKUM.md) yayınlayan kişi içindir.

## Lisans

[MIT lisansı](LICENSE) ile üçüncü taraf lisans ve telif bildirimleri korunur. Proje sorumlusu: [EnsarAslannn](https://github.com/EnsarAslannn).
