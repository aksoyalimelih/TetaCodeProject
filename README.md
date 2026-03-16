# 📚 TetaCode – Gemini AI Destekli Multimodal Ders Notları Yönetim Sistemi

**Tam yığın, yapay zeka destekli bir ders notu platformu.** Öğrencilerin notlarını tek yerden yönetmesini, el yazısı ve sesi metne dönüştürmesini, AI ile özet ve quiz üretmesini ve birden fazla kaynağı tek çalışma notunda birleştirmesini sağlar.

---

## 🎯 Proje Amacı

Bu proje, **TetaCode** teslim beklentilerini karşılayan temel gereksinimlerin üzerine, öğrenci verimliliği için **Google Gemini (2.5 / 3)** tabanlı gelişmiş yetenekler eklenmiş **full stack** bir uygulamadır.

| Beklenti | Durum |
|----------|--------|
| **CRUD** (Not ekleme, listeleme, güncelleme, silme) | ✅ Tam uygulandı |
| **Soft Delete** (Arşive gönderme, geri yükleme) | ✅ Tam uygulandı |
| **Hard Delete** (Kalıcı silme + dosya temizliği) | ✅ Tam uygulandı |
| **Arşiv listesi** ve ayrı ekran | ✅ Tam uygulandı |
| **Kullanıcı kimlik doğrulama (JWT)** | ✅ Tam uygulandı |
| **Dosya yükleme** (PDF, Word, görsel) ve validasyon | ✅ Tam uygulandı |
| **Modern, kullanıcı dostu arayüz** | ✅ Next.js + Tailwind ile sunuldu |
| **Gelişmiş AI özellikleri** (OCR, ses, özet, quiz, birleştirme) | ✅ Gemini multimodal API ile eklendi |

Temel CRUD, soft/hard delete ve arşivleme **tam olarak** karşılanır; üzerine **OCR, sesli not, AI özet/quiz ve akıllı birleştirme** gibi modüller eklenerek proje bir **multimodal ders notu yönetim sistemi**ne dönüştürülmüştür.

---

## ✨ Öne Çıkan Modüller

| Modül | Açıklama |
|-------|----------|
| 📄 **OCR (Görselden Metne)** | El yazısı ve basılı notları **Gemini Vision** ile analiz eder; düzenlenebilir metne çevirir ve isteğe bağlı PDF/not olarak kaydeder. Çok dilli (TR/EN) destek. |
| 🎤 **Voice-to-Text (Sesli Not)** | **Canlı mikrofon kaydı** veya ses dosyası yükleme. Gemini ile transkripsiyon; çıktı doğrudan yeni not olarak kaydedilir. |
| 🧠 **AI Study Tool (Özetle & Sınav Yap)** | Not içeriğinden **5 maddelik özet** ve **çoktan seçmeli quiz** üretir. Interaktif quiz akışı, anında geri bildirim ve skor gösterimi. |
| 🔗 **Smart Synthesis (Akıllı Birleştirme)** | Birden fazla notu ve PDF’i seçip **tek bir çalışma rehberi** halinde birleştirir. Tekrarlar temizlenir, konular mantıklı sıraya dizilir. |
| 🔍 **Full-Text Search & Etiketleme** | Başlık, açıklama ve **etiketler** üzerinden arama; **kategori** filtresi (Vize, Final, Genel + kullanıcı tanımlı). Dinamik kategori/etiket yönetimi. |

Ayrıca: **PDF/Word indirme**, **büyük metin düzenleme penceresi**, **kategori/etiket rozetleri** ve **401’de otomatik çıkış** gibi UX ve güvenlik iyileştirmeleri mevcuttur.

---

## 🛠 Teknik Mimari

| Katman | Teknoloji |
|--------|-----------|
| **Backend** | **ASP.NET Core 8.0** Web API, **Entity Framework Core 9** (SQL Server), **FluentValidation**, **JWT Bearer** kimlik doğrulama |
| **Frontend** | **React 18** + **Next.js** (App Router), **Tailwind CSS**, **TanStack Query**, **Axios**, **react-hot-toast** |
| **AI / Multimodal** | **Google Gemini API** (REST, HttpClient); görsel (OCR), ses (transkripsiyon), metin (özet, quiz, sentez) işlemleri |
| **Veritabanı** | **SQL Server** (LocalDB / Express destekli); **soft delete** global filtre, **migration** + **HasData** ile seed |
| **Dosya & Belge** | **QuestPDF** (PDF üretimi), **PdfPig** (PDF metin çıkarma), **DocumentFormat.OpenXml** (Word dışa aktarma) |

Mimari **Clean Architecture** esaslıdır: **TetaCode.API** (giriş noktası), **TetaCode.Core** (entity, interface), **TetaCode.Data** (DbContext, migration), **TetaCode.Service** (iş mantığı, DTO, servisler). Tüm AI çağrıları **Service** katmanında; **OCR, AI, Audio, Synthesis** servisleri **Gemini** ile konuşur.

---

## 🚀 Kurulum ve Çalıştırma

Backend ve frontend için gereksinimler, **appsettings** / **.env** ayarları, **veritabanı migration** adımları ve **CORS / JWT** notları tek dokümanda toplandı.

👉 **[Kurulum ve Çalıştırma Rehberi İçin Tıklayın](./KURULUM.md)**

---

## 🧪 Test İçin Hazır Veri

Proje ilk kurulumda (EF Core seed migration ile) **demo kullanıcı** ve **5 örnek not** eklenir. Aşağıdaki bilgilerle giriş yapıp tüm özellikleri deneyebilirsiniz.

| Alan | Değer |
|------|--------|
| **E-posta** | `demo@tetacode.com` |
| **Şifre** | `password` |

Bu hesapla giriş yaptıktan sonra **OCR**, **Sesli Not**, **AI Özet/Quiz**, **Akıllı Birleştirme** ve **arama/filtreleme** özelliklerini doğrudan test edebilirsiniz.

---

## 📁 Proje Yapısı (Özet)

```
TetaCodeProject/
├── TetaCode.API/          # Web API, Controller'lar, Program.cs, Middleware
├── TetaCode.Core/         # Entity'ler (Note, AppUser, BaseEntity)
├── TetaCode.Data/         # AppDbContext, Migration'lar, Seed
├── TetaCode.Service/      # Servisler (Note, Auth, OCR, AI, Audio, Synthesis), DTO, Validator
├── frontend/              # Next.js uygulaması (app/, src/lib/)
├── KURULUM.md             # Detaylı kurulum rehberi
└── README.md              # Bu dosya
```

---

## 📄 Lisans ve İletişim

Bu proje **TetaCode** teslim projesi kapsamında geliştirilmiştir.

**Geliştirici:** Ali Melih Aksoy
