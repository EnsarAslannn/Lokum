# Lokum · Kullanım

Lokum, Nashi'nin MIT lisanslı kaynaklarından türetilen dosya araç kutusudur. Aynı 45 format / 27 araç kataloğunu yeni lokum simgesi ve gül kurusu–krem temasıyla sunar. Tam kurulum, derleme ve kaynak bilgileri [ana rehberdedir](README.md).

## Başlangıç

1. Hazır paket yayımlandığında [Releases](https://github.com/EnsarAslannn/Lokum/releases) sayfasından edin. Elindeki `Lokum-Windows-x64.zip` paketinin tamamını yazılabilir bir klasöre çıkar. Kaynak kodunu indirdiysen önce [derleme adımlarını](README.md#kaynaktan-derleme) uygula.
2. Çıkardığın veya derlediğin klasördeki `Lokum.exe` dosyasını aç. Paketin tüm dosyalarını birlikte tut.
3. **Kurulumu tamamla** ekranından eksik medya, Office ve metadata araçlarını indir. Uygulama indirilen dosyaların SHA-256 değerlerini doğrular.
4. **Dosya seç** veya Ctrl+O ile dosyalarını ekle; Ctrl+V ile panodaki dosya, metin ve görselleri de içe aktarabilirsin.
5. Ctrl+Space ile çarkı aç, dönüştürme biçimini veya aracı seç ve yeni kopyayı kaydet.

Dışa aktarımlar kaynak dosyanın üzerine yazmaz; mevcut çıktı adları da korunur. **Taşı** dosya işlemi ise seçilen orijinali taşır.

## Sık kullanılan akışlar

- **Görsel kırpma:** görseli seç, çarktan kırpmayı aç, alanı sürükleyerek belirle. Shift oranı kilitler; Enter yeni kırpılmış kopyayı kaydeder.
- **Arka plan kaldırma (Windows):** görseli seç, **İnce ayar → Arka Planı Kaldır** aracını aç. Yerel önizleme hazır olduğunda **Şeffaf PNG kaydet** düğmesine bas. Model pakete dahildir; internet bağlantısı, Python veya API anahtarı gerekmez. Çoklu seçimde her görsel ayrı PNG olarak kaydedilir. İnce kenarları önizlemede kontrol et. Windows çalışma ortamı gereksinimi [ana rehberde](README.md#arka-plan-kaldırma-windows) açıklanır.
- **İşaretleme:** kalem, marker, şekil, ok veya metin aracını seç. Seç/Taşı ile düzenle; Ctrl+Z son çizimi geri alır.
- **Görsel sansürü (Windows):** çarktan **Fotoğrafı Sansürle** aracını açıp gizlemek istediğin alanı sürükle. Varsayılan stil **Bulanıklaştır**; sonucu önizlemede görürsün. Birden fazla alan seçebilir, **Alanı geri al** ile son seçimi kaldırabilirsin. **Yeni kopya kaydet** bulanıklaştırılmış bir PNG oluşturur; özgün dosya korunur. Düz kaplama ve pikselleştirme de kaplama stili listesindedir.
- **Video/ses kırpma:** Space ile önizlemeyi oynat, sağ/sol oklarla gezin, I/O ile başlangıç ve bitişi işaretle ve aralığı yeni dosya olarak kaydet.
- **PDF:** dosyaları seçip birleştir veya tek PDF'nin sayfalarını böl, sırala ve döndür.
- **Meta veriler:** üstte dosyanın türü, piksel ölçüleri ve boyutu; altında kayıtlı düzenlenebilir alanlar görünür. Ekran görüntüsünde bu alanlar yoksa bir açıklama gösterilir. **Meta veri alanı ekle** bölümünden başlık, yazar, açıklama veya telif hakkı bilgisi ekleyebilir; **Tüm meta veriler** bölümünden kamera ve varsa konum dahil okunan tüm ayrıntıları inceleyebilirsin. Paylaşmadan önce **Tüm meta verileri kaldır** ile kişisel bilgileri temizleyebilirsin. Tek görsel/PDF seçildiğinde düzenleyici açılır; birden fazla görsel seçildiğinde toplu temizleme uygulanır. Değişiklikler yeni bir kopyaya kaydedilir, orijinal korunur. Ekran görüntülerinde kamera/GPS bilgisi bulunmayabilir; meta veri temizleme görüntüde görünen yazıları silmez. Türkçe dosya ve klasör adları desteklenir.
- **Web indirme:** herkese açık, desteklenen medya bağlantısını **Bağlantı indir** ekranına yapıştır. Ctrl+Shift+D kopyalanmış bağlantıyı açar.
- **Kütüphane:** Ctrl+2 ile önceki çıktıları aç; Ctrl+F ile ara ve dosyayı yeniden kullan. Veriler `%LOCALAPPDATA%/Lokum` konumundadır.

## Görünüm ve platformlar

Ayarlar → Genel bölümünden Sistem, Açık ve Koyu görünümü seçebilirsin. Sürükleme kısayolları, çark boyutu, geri bildirim ve çıktı klasörleri de ayarlanabilir.

Windows paketi yerel WPF uygulamasıdır ve kendi .NET çalışma ortamını içerir. macOS SwiftUI kaynakları depoda korunur; macOS paketi oluşturmak için bir Mac üzerinde [derleme adımlarını](README.md#kaynaktan-derleme) uygula. [Platform matrisi](windows/FEATURE-PARITY.md) destek ve farklılıkları listeler.

## Güncellemeler

Lokum için ayrı bir yayın deposu ve imza anahtarı tanımlanana kadar güncelleme kanalı bağlı değildir. İmzalı güncelleme altyapısı kaynakta korunur; [kanal yapılandırma notları](docs/LOKUM.md) yayınlayan kişi içindir.

## Lisans

Orijinal proje: [Nashi — Orkun Işıtmak ve katkıcıları](https://github.com/orkunisitmak/nashi-app). [MIT lisansı](LICENSE) ile üçüncü taraf lisansları korunmuştur.
