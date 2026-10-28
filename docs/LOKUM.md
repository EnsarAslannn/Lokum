# Lokum türev notları

Lokum, Nashi kaynaklarından oluşturulan bağımsız bir türevdir. Kaynak sürümü, köken bağlantısı ve lisans bilgisi [ana rehberde](../README.md#kaynak-ve-lisans) bulunur.

## Marka ve veri ayrımı

Uygulama, derleme ürünleri, ad alanları, dosya yolları ve yerel veri klasörü Lokum olarak adlandırılır. Windows kütüphanesi ve ayarları `%LOCALAPPDATA%/Lokum` içindedir. macOS için bağımsız paket kimliği kullanılır. Nashi'nin telif bildirimi ve üçüncü taraf lisansları korunur.

## Güncelleme kanalı

Bu pakette bağımsız bir Lokum yayın deposu veya yayın imza anahtarı tanımlı değildir. Güncelleme kanalı başlangıçta bağlı değildir. İmzalı indirme, doğrulama, hazırlama ve güvenli değiştirme iş akışları kaynakta korunur.

Bir kanal yayımlarken kendi HTTPS yayın deposunu ve kendi Ed25519 imza anahtarının **yalnızca genel anahtarını** yapılandır. Nashi'nin yayın deposunu ve anahtarını Lokum güncelleme kanalı olarak kullanma. Özel imzalama anahtarları uygulamaya veya depoya eklenmez.

Windows'ta `Lokum.exe` yanına `lokum-updates.json` dosyası koy. Dosyada iki alan bulunur:

| Alan | Değer |
| --- | --- |
| `Repository` | Yayıncının kendi GitHub `kullanıcı/depo` yolu |
| `PublicKey` | Base64 kodlu, 32 baytlık Ed25519 **genel** anahtarı |

Uygulama kanal tanımını yerel dosyadan okur; uzak manifest güvenilir depoyu veya anahtarı değiştiremez. Manifest, aynı deponun `releases/latest/download/windows-update.json` dosyasında olmalıdır. Güncelleme arşivi Lokum dosyalarını içermeli ve bu anahtarın eşleşen özel anahtarıyla imzalanmalıdır. `windows/Lokum.Core/PublicUpdates.cs` şemayı ve güven sınırlarını doğrular. Yapılandırılmamış pakete örnek/test anahtarı ekleme.

macOS yapılandırması `Configuration/Updates.plist` ve `scripts/configure-updates.py` üzerinden derlemeye işlenir. Kullanmadan önce ilgili doğrulama testlerini çalıştır.

## Depo otomasyonu

Nashi sahibine özel otomatik PR birleştirme bu türevde etkin değildir. Normal kaynak ve Windows derleme iş akışları korunur. İmzalı Windows güncelleme denetimi yalnızca elle başlatılabilir; kendi Lokum sürümlerini yayımladıktan sonra depo değişkenlerinde `LOKUM_UPDATE_TESTS_ENABLED=true` ve `LOKUM_UPDATE_PUBLIC_KEY` genel anahtarını tanımla. İş akışına yükseltilecek başlangıç sürüm etiketini ver. Doğrulama yalnızca iş akışının çalıştığı deponun paketlerini kullanır.

## Doğrulama sınırları

Windows çalıştırılabilir paketi ve kaynakları birlikte teslim edilir. macOS kaynakları da korunur; Windows üzerinde SwiftUI/AppKit derlemesi veya macOS çalışma testi yapılmış sayılmaz. macOS sürümü yayımlamadan önce gerçek Mac üzerinde derle, araç kurulumunu ve akışları test et.

Web tanıtımındaki etkileşimli demo desteklenen görsel/PDF/ses işlemlerini tarayıcıda yapar. Masaüstüne özel işlemler açıkça önizleme olarak gösterilir. Demo masaüstü uygulamasının yerine geçmez.
