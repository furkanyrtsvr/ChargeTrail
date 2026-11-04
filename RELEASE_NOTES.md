## ChargeTrail v2.0.2

### New

- **Reset** on every Overview card. Reset without saving first; confirm before unsaved time is discarded. The counter starts paused at zero. Notes, settings and saved results stay intact.
- **Delete** on manual cards and in their Details dialog. Deletion requires confirmation and preserves previous results in Records.
- **Close interface · keep tracking** in the tray. Hides the window and tray icon while the tracker process continues. Reopen the EXE to return to the same tracker.
- **Stop tracking and exit** now asks for confirmation, with No selected by default.

### Validation

30 core checks passed. Windows integration checks also cover reset/delete confirmation and cancellation, background-only counting and reopening, and cancelled exit preserving tracking. The earlier smooth-navigation fix is retained.

### Download

Windows 10/11 x64 with .NET Framework 4.8. Run `ChargeTrail.exe` directly or extract `ChargeTrail-windows-x64.zip`. Checksums are in `SHA256SUMS.txt`. Existing records and counters are preserved during upgrade.

Closing the interface leaves a background user process running. It is not a Windows service: if you fully stop or kill that process, time does not accumulate. Enable Start with Windows to resume after sign-in. Device sleep is included in manual PC uptime, while PC sleep/shutdown is excluded.

---

### Türkçe

- Genel bakış kartlarına **Sıfırla** eklendi. Kaydetmeden de sıfırlanabilir; önce onay istenir. Sayaç sıfırdan duraklatılmış başlar. Notlar ve arşiv korunur.
- Manuel sayaçlara **Sil** eklendi. Onaydan sonra sayaç kaldırılır; kaydedilmiş sonuçlar Kayıtlar'da kalır.
- Tepside **Arayüzü kapat · takip sürer**, pencereyi ve tepsi simgesini gizleyip arka planda saymaya devam eder. EXE'yi tekrar açınca geri gelir.
- **Takibi durdur ve çık** artık ayrıca onay ister; varsayılan seçim Hayır'dır.

Hiçbir takip süreci çalışmıyorsa ölçüm yapılamaz. Görev Yöneticisi'nden sonlandırmak takibi durdurur; otomatik açılış için Windows ile başlat seçeneğini kullanın. Bu sürüm mevcut verileri korur.
