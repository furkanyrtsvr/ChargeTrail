## ChargeTrail v2.0.1

Track a charge, save the result, remember the conditions.

### Features

- Selected equipment on overview cards; a separate collection of Windows input devices.
- Manual PC uptime counters and automatic device-presence/estimated-activity tracking.
- Background tray operation and optional start with Windows.
- Saved charge records with editable polling-rate and usage notes, plus CSV export.
- Turkish and English interfaces with a custom app logo/icon.

### Fix

Tab switching keeps the sidebar and language selector alive, buffers nested layouts and redraws only the replacement content. Clicking the current tab does not rebuild the page.

### Download

Windows 10/11 x64 with .NET Framework 4.8. Run `ChargeTrail.exe` directly or extract `ChargeTrail-windows-x64.zip`. `SHA256SUMS.txt` contains checksums.

### Measurement note

The app measures awake PC time, Windows device presence and estimated input activity. It does not directly verify whether a wireless device is physically on or detect an empty battery. Start after charging and save when you observe the battery has run out. Receivers can remain visible when their devices are off.

---

### Türkçe

Cihaz testi yaparken geçen süreyi takip edin, pil bittiğinde sonucu kaydedin ve 1000 Hz / 2.4 GHz / RGB kapalı gibi kullanım koşullarını not edin. Manuel PC sayacı, cihaz kartları, kayıt arşivi, düzenlenebilir notlar, CSV ve TR/EN desteği bulunur.

Bu sürüm sekme geçişindeki yeniden yüklenme/titreme sorununu düzeltir. Menü ve dil seçici yerinde kalır; yalnızca içerik değiştirilir. Mevcut sayaçlar ve kayıtlar korunur.

Manuel mod PC'nin uyanık süresini ölçer. Otomatik mod cihazın Windows'ta görünmesini ve tahmini girdi etkinliğini takip eder; fiziksel cihazın açık olduğunu veya pilin bittiğini kesin algılamaz.
