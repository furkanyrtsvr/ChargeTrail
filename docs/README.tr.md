# ChargeTrail — Türkçe rehber

[English](../README.md) · [Windows EXE indir](https://github.com/furkanyrtsvr/ChargeTrail/releases/latest)

Üreticinin söylediği pil süresini kendi kullanım koşullarınızla karşılaştırmak için bir şarj günlüğü tutun: tam şarjdan sonra ölçümü başlatın, cihazı kullanın ve pili bittiğini gözlemlediğinizde sonucu kaydedin. Her kayda 1000 Hz, 2.4 GHz, RGB kapalı gibi koşulları ekleyip sonraki şarj döngülerinde tekrar bakabilirsiniz.

**Uygulama cihazın fiziksel olarak açık olduğunu kesin doğrulayamaz. Manuel mod PC uyanık süresini, otomatik mod Windows görünürlüğü ve tahmini girdi etkinliğini ölçer. Pil bitişini siz işaretlersiniz.**

v2.0.1 sekme geçişindeki titremeyi düzeltir: menü ve dil seçici yerinde kalır, içerik tamponlanarak tek çizimde değiştirilir. Aynı sekmeye yeniden tıklamak sayfayı yeniden oluşturmaz.

Windows 10/11 x64 için cihaz kullanım ve şarj günlüğü. Tek EXE, Türkçe/İngilizce arayüz, yerel kayıt. Python veya .NET SDK gerektirmez; Windows .NET Framework 4.8 kullanır.

## Kullanım

1. `bin/ChargeTrail.exe` dosyasını açın. Sağ üstten **TR / EN** seçebilirsiniz; tercih kaydedilir.
2. **Tüm cihazlar** bölümünde **Ana ekran** kutusunu işaretleyin. Seçili ekipmanlar **Genel bakış** bölümünde kendi kartlarında görünür. Cihazı tanımak için **Mouse / klavye tanı** seçeneğini kullanıp fiziksel cihazdan girdi gönderin; tanınan cihaz ana ekrana eklenir.
3. **Detaylar** ile cihaz adını ve bağlantısını düzenleyin. USB alıcıların 2.4 GHz olduğu kesin çıkarılamaz; bağlantıyı elle seçebilirsiniz. Bluetooth üst aygıt ağacında görülürse otomatik etiketlenir.
4. Tam şarjdan sonra karttaki **Başlat** ile ölçün; **Duraklat** aynı sayacı durdurur. Tekrar Başlat aynı toplamdan devam eder.
5. Pili bittiğinde o karttaki **Kaydet** seçeneğini kullanın. Açılan pencerede Hz ve kullanım notlarını yazıp **Kayıtlara kaydet** seçin. Sonuç sabitlenir; karttaki toplam otomatik sıfırlanmaz.
6. **Kayıtlar** bölümünde **Aç** veya satıra çift tıklayarak kaydı inceleyin. Polling rate ve notları sonradan düzenleyebilirsiniz: ör. `1000 Hz, 2.4 GHz, RGB kapalı, günde 5 saat oyun`.
7. Yeniden şarj ettiğinizde karttaki **Başlat**, önceki sonucu arşivde koruyarak yeni ölçümü sıfırdan açmak için onay ister.
8. **CSV dışa aktar** kaydedilmiş sonuçları, Hz değerlerini ve notları UTF-8 CSV'ye aktarır.

## Manuel mod

**SC380 Pro** manuel sayacı hazır gelir. **+ Manuel sayaç** ile istediğiniz isimde bağımsız sayaçlar ekleyebilirsiniz. Başlat açıkken PC'nin uyanık kaldığı süre birikir. Mouse uyusa, Hz düşse, kullanılmasa veya bağlantısı kesilse de bu sayaç devam eder. Ekran kilidinde sayar; PC uyku, hazırda bekletme ve kapalı süreleri sayılmaz. Fiziksel cihaz bağlantısı kontrol edilmez.

## Otomatik cihaz takibi

Kartın büyük sayacı cihaz/alıcı arayüzünün Windows Raw Input listesinde göründüğü süreyi gösterir. Altındaki **Etkin süre**, o cihazdan girdi geldikten sonraki etkinlik pencerelerinin birleşimidir; varsayılan 60 saniye. Ayarlardan 1–600 saniye arasında değiştirilebilir ve sonraki ölçümlere uygulanır. Tuş içeriği veya hareket koordinatları okunup kaydedilmez; yalnızca girdi gönderen cihazın kimliği kullanılır.

Görünür süre kesin kablosuz bağlantı veya pil tüketim süresi değildir: 2.4 GHz alıcı cihaz kapalıyken de görünür kalabilir; bazı Bluetooth sürücüleri de cihazı listede tutabilir. Etkinlik penceresi hareketsiz okumayı eksik, değişmeyen rapor gönderen oyun kumandalarını fazla sayabilir. Cihaz listesi 5 saniyede bir ve aygıt değişiminde yenilenir. Otomatik takip mouse, klavye ve Windows Raw Input'a sunulan HID oyun kumandalarını kapsar. Kulaklıkları veya tüm Bluetooth sınıflarını otomatik takip etmez; bunlar için cihaz adıyla manuel PC sayacı oluşturabilirsiniz.

Pil yüzdesi, otomatik pil bitişi/şarj algılama ve kesin pil kapasitesi ölçümü yoktur. Başlangıç ve sonucu siz işaretlersiniz. PC dışında geçen cihaz kullanım süresi ölçülmez.

## Arka plan ve veriler

**Ayarlar ve rehber > Windows ile arka planda başlat** açıkken mevcut kullanıcının oturum açılışında devam eder. Seçenek başlangıçta kapalıdır. EXE `%LOCALAPPDATA%\PilTakip\ChargeTrail.exe` konumuna kopyalanır. X düğmesi pencereyi tepsiye alır; **Çık · takip durur** işlemi tamamen kapatır. İşlem çalışmıyorken PC açık olsa da süre sonradan eklenmez.

Mevcut sürümden geçişte eski veri klasörü korunur: `%LOCALAPPDATA%\PilTakip\tests.json`. Eski süreler ve kayıtlar aktarılır; takip açık cihazlar ve manuel sayaçlar ilk açılışta ana ekrana sabitlenir. Yerel JSON 5 saniyede bir atomik yazılır ve önceki kayıt `.bak` dosyasında tutulur. Bozuk ana dosya varsa yedekten kurtarılır; hasarlı dosya korunur. Ani kapanmada son kayıttan sonraki süre kaybolabilir. İnternet kullanılmaz.

Kaldırmak için önce Windows ile başlat seçeneğini kapatın, tepsiden çıkın ve EXE dosyalarını silin. Geçmişi de silmek isterseniz yerel veri klasörünü ayrıca kaldırabilirsiniz.

## Kaynak ve doğrulama

`./build.ps1`, Windows Framework C# derleyicisiyle `bin/ChargeTrail.exe` üretir. Logo kaynağı `assets/ChargeTrail.svg`, EXE simgesi `assets/ChargeTrail.ico` dosyalarıdır.

`ChargeTrail.exe --self-test selftest.log` 23 sayaç, kayıt, not, CSV ve uyumluluk kontrolünü çalıştırır. `ChargeTrail.exe --integration-test integration.log` ayrı geçici klasörde gerçek formun ileti döngüsüyle arka plan sayımı, kalıcı kayıt ve yedek kurtarma kontrollerini çalıştırır; TR/EN ekranlarını `ui-preview` altına render eder. Kullanıcı verilerini değiştirmez.

Windows API referansları: [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input), [uykuyu dışlayan saat](https://learn.microsoft.com/en-us/windows/win32/sysinfo/interrupt-time), [aygıtın bildirdiği ad](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-busreporteddevicedesc).
