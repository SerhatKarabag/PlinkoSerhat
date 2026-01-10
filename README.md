# Plinko Case Çalışması

Bu proje, Plinko’nun temel oyun akışını; servis simülasyonu, anti-cheat kontrolleri ve performans tarafıyla birlikte ele alan bir case çalışmasıdır. Hedefim, oyuncuya gerçek bir client-server sistemi varmış hissi veren; ileride yeni özellikler eklemeyi zorlaştırmayan ve akıcı çalışan bir yapı kurmaktır.

---

## Mimariyi Neden Bu Şekilde Kurguladım?

### Akışı netleştirmek için state machine kullandım.
Oyun `Initializing`, `Playing`, `LevelTransition`, `RunEnding`, `RunFinished`, `Paused`, `Error` gibi durumlara ayrıldı. Böylece her durumda hangi sistemlerin çalışacağı belli oldu; geçişler de kontrol edilebilir hale geldi (`GameStateMachine` ve state sınıfları).

### Servisleri arayüzlerle ayırdım.
Sunucu konuşması, oturum yönetimi, reward doğrulama gibi parçaları `IServerService`, `ISessionManager`, `IRewardBatchManager` gibi arayüzlere böldüm. Bu sayede gerçek backend’e geçmek ya da mock ile test etmek “tek bir implementasyon değişimi” seviyesine indi.

### Bağımlılıkları DI ile toparladım.
`GameBootstrapper` üzerinden `MockServerService`, `SessionManager`, `RewardBatchManager` gibi servisleri kurup `GameManager.Initialize` ile enjekte ediyorum. Bu hem bağımlılıkları tek yerde toplayıp işleri temizliyor, hem de unit test yazmayı ciddi kolaylaştırıyor.

### Konfigürasyonu ScriptableObject’e taşıdım.
Kural seti, seviye parametreleri, fizik ayarları gibi şeyler `GameConfig` içinde ScriptableObject. Bu sayede dengeleme/iterasyon yaparken kodla boğuşmak gerekmiyor.

### UI ve sistemleri event ile bağladım.
`GameEventWiring` ile UI, ball yönetimi, session ve batch süreçleri birbirine event üzerinden bağlı. Bu yaklaşım gereksiz sıkı bağımlılıkları azaltıyor, okunabilirlik ve bakım tarafında rahatlatıyor.

---

## Performans İçin Aldığım Önlemler

- **Object pooling:**  
  Toplar ve UI popup’lar pooling üzerinden gidiyor (`BallManager`, `GameObjectPool`, `UIManager`). Amaç: runtime GC spike’larını mümkün olduğunca azaltmak.

- **FPS izleme + dinamik kalite:**  
  `FPSMonitor` belirli eşiklerin altına düşüş yakalarsa `PhysicsOptimizer` üzerinden fizik kalitesini düşürüyor; oyun toparlayınca tekrar yükseltiyor.

- **Fizik optimizasyonu:**  
  `PhysicsOptimizer.OptimizeForMobile()` ile iterasyon sayıları / `fixedDeltaTime` gibi ayarları mobil odaklı hale getirdim. Ball rigidbody’lerinde de gereksiz maliyet çıkaracak ayarlardan kaçındım.

- **UI’ı her frame güncellememek:**  
  UI sürekli update olmuyor; `UIConfig` içindeki `UpdateInterval` ile periyodik güncelleniyor. Böylece string/text güncellemeleri her frame çalışıp maliyet bindirmiyor.

- **Reward doğrulamayı batch yapmak:**  
  Reward doğrulama istekleri tek tek değil, toplu (batch) gidiyor (`RewardBatchManager`). Bu hem olası network overhead’i simüle etmek için iyi, hem de akışı daha akıcı tutuyor.

---

## Mock Servis Tarafında Nasıl Bir Mantık Kurdum?

Mock servis, “gerçek backend olsa nasıl davranırdı?” sorusunu mümkün olduğunca taklit edecek şekilde tasarlandı:

- **Session yönetimi:**  
  `StartSessionAsync` ve `SyncSessionAsync` ile oyuncu state’i tutuluyor. Session süreleri ve seed üretimi de backend mantığına benzer şekilde ele alındı.

- **Latency ve hata simülasyonu:**  
  `GameConfig` üzerinden `MinServerLatencyMs`, `MaxServerLatencyMs`, `ServerErrorRate` değerleri var. Böylece gecikme + hata oranı üretip istemci tarafındaki retry/batch davranışlarını gerçekçi koşullarda deneyebiliyorum.

- **Batch doğrulama + anti-cheat:**  
  `ValidateBatchAsync` içinde reward entry’leri kontrol ediliyor (aynı ball index tekrar ediyor mu, bucket sınırları doğru mu vb.). Ayrıca `AntiCheatValidator` ile çok uç reward dağılımları, mantıksız bucket tercihleri, rate-limit gibi sinyalleri işaretliyorum.

- **Mock state’in kalıcı olması:**  
  Mock server state’i `PlayerPrefs` ile serialize edip saklıyorum. Böylece “oyunu kapat-aç” senaryosunda wallet ve session bilgisi (mock da olsa) kalıyor.

Bu altyapı sayesinde `latency`, `hata`, `retry`, `anti-cheat` ve `persistence` gibi gerçek hayat senaryoları hızlıca test edebiliyorum.
