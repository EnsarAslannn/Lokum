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
- **İşaretleme:** kalem, marker, şekil, ok veya metin aracını seç. Seç/Taşı ile düzenle; Ctrl+Z son çizimi geri alır.
- **Video/ses kırpma:** Space ile önizlemeyi oynat, sağ/sol oklarla gezin, I/O ile başlangıç ve bitişi işaretle ve aralığı yeni dosya olarak kaydet.
- **PDF:** dosyaları seçip birleştir veya tek PDF'nin sayfalarını böl, sırala ve döndür.
- **Web indirme:** herkese açık, desteklenen medya bağlantısını **Bağlantı indir** ekranına yapıştır. Ctrl+Shift+D kopyalanmış bağlantıyı açar.
- **Kütüphane:** Ctrl+2 ile önceki çıktıları aç; Ctrl+F ile ara ve dosyayı yeniden kullan. Veriler `%LOCALAPPDATA%/Lokum` konumundadır.

## Görünüm ve platformlar

Ayarlar → Genel bölümünden Sistem, Açık ve Koyu görünümü seçebilirsin. Sürükleme kısayolları, çark boyutu, geri bildirim ve çıktı klasörleri de ayarlanabilir.

Windows paketi yerel WPF uygulamasıdır ve kendi .NET çalışma ortamını içerir. macOS SwiftUI kaynakları depoda korunur; macOS paketi oluşturmak için bir Mac üzerinde [derleme adımlarını](README.md#kaynaktan-derleme) uygula. [Platform matrisi](windows/FEATURE-PARITY.md) destek ve farklılıkları listeler.

## Güncellemeler

Lokum için ayrı bir yayın deposu ve imza anahtarı tanımlanana kadar güncelleme kanalı bağlı değildir. İmzalı güncelleme altyapısı kaynakta korunur; [kanal yapılandırma notları](docs/LOKUM.md) yayınlayan kişi içindir.

## Lisans

Orijinal proje: [Nashi — Orkun Işıtmak ve katkıcıları](https://github.com/orkunisitmak/nashi-app). [MIT lisansı](LICENSE) ile üçüncü taraf lisansları korunmuştur.
