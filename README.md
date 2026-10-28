# Liman 🚢

Konteyner sıralama bulmacası (Android, Unity). Rıhtımdaki karışık konteynerleri vinçle doğru renkteki gemilere yükle; gemiler dolunca kalkar, sıradaki gemi yanaşır.

## Oyun kuralları

- **Rıhtım:** Konteyner yığınları. Sadece en üstteki konteyner hareket ettirilebilir.
- **Yükleme:** Üstteki konteyner, rıhtıma yanaşmış **aynı renkteki** gemiye yüklenir.
- **Aktarma:** Üstteki konteyner başka bir yığına konabilir; ancak hedef **boşsa** ya da üstü **aynı renkse** ve yığın dolu değilse.
- **Gemi sırası:** Gemiler sabit sırayla gelir. Dolan gemi kalkar, yerine sıradaki yanaşır.
- **Ağır konteyner** (kalın çerçeveli, 8. seviyeden itibaren): Başka bir konteynerin üstüne konamaz; sadece boş yığına ya da gemiye gider.
- **Kilitli konteyner** (asma kilitli, 15. seviyeden itibaren): Kendi rengindeki gemi rıhtıma yanaşana kadar hareket ettirilemez.
- **Amaç:** Bütün gemileri doldurup göndermek. Optimum hamle sayısı (Hedef) = 3 yıldız.

## Mimari

```
Assets/_Project/
  Scripts/Core/   Liman.Core  — saf C#, Unity'ye bağımlı değil (oyun kuralları, çözücü, seviye üretici)
  Scripts/Game/   Liman.Game  — Unity katmanı (akış, görünüm, girdi, kayıt, reklam arayüzü)
  Tests/EditMode/ NUnit testleri — hem Unity Test Runner'da hem dotnet ile çalışır
tools/CoreTests/  Testleri Unity olmadan çalıştıran .NET projesi (CI bunu kullanır)
```

Öne çıkan noktalar:

- **Sonsuz seviye, veri dosyası yok:** `LevelGenerator`, seviye numarasından türetilen tohumla (seed) rastgele dizilim üretir ve sadece çözücünün çözülebilir olduğunu kanıtladıklarını kabul eder. Zorluk, optimum çözümün gerektirdiği minimum aktarma sayısıyla ayarlanır (`GeneratorSettings.ForLevel`).
- **Deterministik RNG:** `System.Random` çalışma ortamları (Mono / IL2CPP / .NET) arasında aynı sonucu garanti etmediği için kendi xorshift64* üretecimiz var. Aynı seviye her cihazda aynıdır.
- **BFS çözücü:** En kısa çözümü bulur. Seviye doğrulama, yıldız hedefi ve ipucu bunu kullanır. Durumlar yığın sırasından bağımsız kanonik anahtarla tekilleştirilir. Ayrıca "yüklenebilen konteyneri hemen yükle" budaması uygulanır: 600 örnek seviyede tam BFS ile aynı optimum sonucu verip yaklaşık 13 kat hızlanma sağladı.
- **Adil reklam politikası (`AdPolicy`):** İlk seviyelerde ve seviye ortasında reklam yok, birkaç seviyede bir geçiş reklamı, ipucu için isteğe bağlı ödüllü reklam, "Reklamları kaldır" satın alımıyla geçiş reklamları tamamen kapanır.

## Çalıştırma

### Unity

1. Unity Hub ile **Unity 6 LTS** (6000.x) ve **Android Build Support** modülünü kur.
2. Hub → *Add project from disk* → bu klasörü seç. Farklı bir 6000.x sürümü istenirse onunla aç.
3. Boş bir sahnede **Play**'e bas. Oyun `GameBootstrap` ile koddan kurulur, sahneye bir şey eklemen gerekmez.
4. Build için: *File → New Scene* ile `Assets/_Project/Scenes/Main.unity` kaydet ve *Build Profiles* listesine ekle.
5. Input System uyarısı çıkarsa *Yes* de. *Player Settings → Active Input Handling* "Both" ya da "Input System" olabilir.

### Testler (Unity gerekmez)

```bash
dotnet test tools/CoreTests
```

Unity içinde: *Window → General → Test Runner → EditMode*.

## Yol haritası

- [x] Vinç ve gemi kalkış animasyonları, geçici sentezlenmiş sesler
- [ ] Gerçek görseller (konteyner/gemi sprite'ları) ve kayıtlı sesler
- [ ] Seviye üretimini arka plan thread'ine taşıma ve yükleme ekranı
- [x] Özel konteynerler: ağır, kilitli
- [ ] Soğuk zincir konteyner (belirli hamle içinde yüklenmeli)
- [ ] Liman geliştirme meta oyunu (para → yeni rıhtım/vinç/tema)
- [ ] AdMob entegrasyonu (`IAdService` implementasyonu) ve Google Play Billing ile "Reklamları kaldır"
- [ ] Günlük bulmaca (tarihten türetilen tohum)
- [ ] Play Store: kapalı test (12 test kullanıcısı / 14 gün), mağaza görselleri, gizlilik politikası

## Lisans

[MIT](LICENSE)
