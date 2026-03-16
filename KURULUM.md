# TetaCode Projesi – Sıfır Bilgisayarda Kurulum Rehberi

Bu dokümanda projeyi ilk kez çalıştıracak bir geliştiricinin yapması gereken tüm adımlar teknik detaylarıyla listelenmiştir.

---

## 1. Gereksinimler

- **.NET 8.0 SDK** – [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** ve **npm** – [https://nodejs.org](https://nodejs.org)
- **SQL Server** – LocalDB, SQL Server Express (`.\\SQLEXPRESS`) veya tam SQL Server
- **Git** (isteğe bağlı, projeyi klonlamak için)

---

## 2. Backend (API) Kurulumu

### 2.1 Projeyi açma ve restore

```bash
cd TetaCodeProject
dotnet restore
```

### 2.2 appsettings.json – Hangi alanlar doldurulmalı?

| Alan | Açıklama | Boş bırakılabilir? | Varsayılan / Not |
|------|----------|---------------------|------------------|
| **ConnectionStrings:DefaultConnection** | SQL Server bağlantı dizesi | Hayır | Varsayılan isim: **DefaultConnection**. Değer örnek: `Server=.\\SQLEXPRESS;Database=TetaCodeDb;Trusted_Connection=True;TrustServerCertificate=True` |
| **Jwt:Issuer** | JWT token issuer | Hayır (uygulama çalışır) | Örn. `TetaCodeIssuer` |
| **Jwt:Audience** | JWT token audience | Hayır (uygulama çalışır) | Örn. `TetaCodeAudience` |
| **Jwt:Key** | JWT imzalama anahtarı (en az 32 karakter) | Hayır | Geliştirme için örnek bir anahtar kullanılabilir; **production’da mutlaka güçlü ve gizli bir değer kullanın.** |
| **Gemini:ApiKey** | Google Gemini API anahtarı | OCR / AI / Ses özellikleri için hayır | Boş bırakılırsa OCR, AI Özet/Sınav, Sesli Not, Akıllı Birleştirme çalışmaz. [Google AI Studio](https://aistudio.google.com/apikey) üzerinden alınır. |
| **Gemini:ModelName** | Kullanılacak model adı | Evet | Kod tarafında “flash” içeren model dinamik seçiliyor; bu alan fallback için. |

**Özet:**

- **Boş bırakılabilecek:** Sadece `Gemini:ModelName` (isteğe bağlı).
- **Yeni makinede mutlaka ayarlanacak / kontrol edilecek:**
  - `ConnectionStrings:DefaultConnection` – kendi SQL Server adresinize göre.
  - `Jwt:Key` – production’da kendi gizli anahtarınız.
  - `Gemini:ApiKey` – AI/OCR/Ses özellikleri için.

**Connection string:** Projede kullanılan bağlantı dizesi adı **DefaultConnection**’dır; `Program.cs` içinde `GetConnectionString("DefaultConnection")` ile okunur.

### 2.3 JWT anahtarı – Environment variable (isteğe bağlı)

Production veya farklı ortamlar için JWT anahtarını ortam değişkeni ile verebilirsiniz:

- **Değişken adı:** `TETACODE_JWT_KEY`
- **Öncelik:** Ortam değişkeni varsa o kullanılır; yoksa `appsettings.json` içindeki `Jwt:Key` kullanılır.
- Boş veya `__SET_IN_ENV__` ise uygulama başlarken hata fırlatır.

### 2.4 Veritabanı – Migration (EF Core)

Projede **yalnızca EF Core migration** kullanılır; ek bir SQL script yok.

```bash
# Migration'ları veritabanına uygula (Database = TetaCodeDb oluşturulur / güncellenir)
dotnet ef database update --project TetaCode.Data --startup-project TetaCode.API
```

- İlk çalıştırmada veritabanı ve tablolar oluşturulur.
- **SeedSampleData** migration’ı varsa, ilk açılışta demo kullanıcı (`demo@tetacode.com` / `password`) ve 5 örnek not eklenir.
- **Ek bir .sql dosyası çalıştırmanız gerekmez.**

### 2.5 API’yi çalıştırma

```bash
cd TetaCode.API
dotnet run
```

- Varsayılan adres: **http://localhost:5047** (`Properties/launchSettings.json` içindeki `applicationUrl`).
- Swagger: **http://localhost:5047/swagger**

---

## 3. Frontend (Next.js / React) Kurulumu

### 3.1 Bağımlılıklar ve çalıştırma

```bash
cd frontend
npm install
npm run dev
```

- Varsayılan adres: **http://localhost:3000**

### 3.2 API URL’i nerede tanımlı?

- **Dosya:** `frontend/src/lib/axios.ts`
- **Mantık:**  
  `process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5047/api"`
- Yani önce **NEXT_PUBLIC_API_BASE_URL** ortam değişkeni okunur; yoksa **http://localhost:5047/api** kullanılır.

### 3.3 .env dosyası gerekli mi?

**Zorunlu değil**; varsayılanlar localhost:5047 ile çalışır. Farklı bir backend adresi veya port kullanacaksanız `.env.local` kullanın.

Projede **`.env.local.example`** var; bunu kopyalayıp `.env.local` yapabilirsiniz:

```bash
cp .env.local.example .env.local
```

**Örnek `.env.local` içeriği:**

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5047/api
NEXT_PUBLIC_ASSET_BASE_URL=http://localhost:5047
```

- **NEXT_PUBLIC_API_BASE_URL** – Backend API base URL (örn. `http://localhost:5047/api`).
- **NEXT_PUBLIC_ASSET_BASE_URL** – Yüklenen dosyalar için temel URL (örn. `http://localhost:5047`).

API farklı portta (örn. 5000) ise bu değerleri buna göre değiştirmeniz yeterli.

---

## 4. Bağımlılıklar – Harici araçlar (Tesseract, Ghostscript vb.)

- **Tesseract, Ghostscript veya benzeri harici bir binary kurulumu yok.**
- OCR: **Google Gemini API** (HTTP isteği; sadece API Key gerekir).
- PDF: **QuestPDF**, **UglyToad.PdfPig**, **DocumentFormat.OpenXml** – hepsi **NuGet** paketi.
- Tüm backend bağımlılıkları **NuGet** ile; frontend bağımlılıkları **npm** ile hallolur.

---

## 5. Kritik ayarlar – CORS ve JWT

### 5.1 CORS

- **Dosya:** `TetaCode.API/Program.cs`
- **Politika adı:** `FrontendCors`
- **İzin verilen origin:** Yalnızca **http://localhost:3000**
- Frontend’i farklı bir portta (örn. 3001) çalıştırırsanız CORS hatası alırsınız. O zaman `Program.cs` içinde:

  ```csharp
  policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
  ```
  şeklinde o portu da eklemeniz gerekir.

### 5.2 JWT (Yetkilendirme)

- **Şema:** Bearer JWT (`Authorization: Bearer <token>`).
- **JWT Key:** `appsettings.json` → `Jwt:Key` veya ortam değişkeni `TETACODE_JWT_KEY`.
- Key boş/geçersizse uygulama başlamaz; token üretilemez ve tüm `[Authorize]` endpoint’leri 401 döner.
- Production’da **Jwt:Key** (veya `TETACODE_JWT_KEY`) mutlaka güçlü ve gizli tutulmalı; `appsettings.json` commit’e hassas değer koymamak için **appsettings.Development.json** veya env kullanılabilir.

---

## 6. Sıfır bilgisayarda adım adım özet

1. **.NET 8**, **Node.js 18+**, **SQL Server** (veya LocalDB) kur.
2. **Backend:**  
   - `appsettings.json` içinde **ConnectionStrings:DefaultConnection**, **Jwt:Key** ve (AI/OCR/Ses için) **Gemini:ApiKey** ayarla.  
   - `dotnet restore` → `dotnet ef database update --project TetaCode.Data --startup-project TetaCode.API` → `dotnet run` (TetaCode.API).
3. **Frontend:**  
   - `cd frontend` → `npm install` → `npm run dev`.  
   - İsteğe bağlı: `.env.local.example` → `.env.local` ve `NEXT_PUBLIC_API_BASE_URL` / `NEXT_PUBLIC_ASSET_BASE_URL` (sadece farklı port/host kullanacaksan).
4. **Harici araç:** Yok; sadece NuGet/npm ve Gemini API Key.
5. **Veritabanı:** Sadece `dotnet ef database update`; ek SQL script yok.
6. **CORS:** Varsayılan frontend `http://localhost:3000`; farklı port kullanıyorsan `Program.cs` içinde CORS origin’e ekle.

Bu adımlarla proje sıfır bir bilgisayarda çalıştırılabilir.
