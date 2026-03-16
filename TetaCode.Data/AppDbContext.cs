using Microsoft.EntityFrameworkCore;
using TetaCode.Core.Entities;

namespace TetaCode.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var note = modelBuilder.Entity<Note>();

        // Note için soft delete (IsDeleted == false) global sorgu filtresi
        note.HasQueryFilter(n => !n.IsDeleted);

        // Arama performansı için Title alanını indexle
        note.HasIndex(n => n.Title);

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Seed: Demo kullanıcı (e-posta: demo@tetacode.com, şifre: password)
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = 1,
                FullName = "Demo Kullanıcı",
                Email = "demo@tetacode.com",
                PasswordHash = "$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi"
            }
        );

        // Seed: İlk açılışta 5 örnek not (UserId = 1)
        var seedDate = new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Note>().HasData(
            new Note
            {
                Id = 1,
                Title = "Nesne Yönelimli Programlama Temelleri",
                Description = "OOP'nin dört temel prensibi: Encapsulation (kapsülleme), Inheritance (kalıtım), Polymorphism (çok biçimlilik) ve Abstraction (soyutlama). Sınıflar ve nesneler arasındaki ilişkiler, SOLID prensipleri ile sürdürülebilir kod yapısı.",
                FilePath = null,
                Tags = "OOP,C#,programlama,temel",
                Category = "Genel",
                UserId = 1,
                CreatedAt = seedDate,
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            },
            new Note
            {
                Id = 2,
                Title = "Veritabanı Tasarımı ve Normalizasyon",
                Description = "1NF, 2NF, 3NF ve BCNF kuralları. Birincil ve yabancı anahtarlar, indeksler, sorgu optimizasyonu. İlişkisel model ve ER diyagramları.",
                FilePath = null,
                Tags = "SQL,veritabanı,normalizasyon",
                Category = "Vize",
                UserId = 1,
                CreatedAt = seedDate.AddDays(1),
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            },
            new Note
            {
                Id = 3,
                Title = "REST API ve HTTP Metodları",
                Description = "GET, POST, PUT, PATCH, DELETE kullanımı. Status kodları (200, 201, 400, 401, 404, 500). JSON formatı, versioning ve güvenlik (JWT, API Key).",
                FilePath = null,
                Tags = "API,REST,HTTP,backend",
                Category = "Final",
                UserId = 1,
                CreatedAt = seedDate.AddDays(2),
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            },
            new Note
            {
                Id = 4,
                Title = "Clean Architecture Katmanları",
                Description = "Domain (Core), Application (Use Cases), Infrastructure (Data, API). Bağımlılık yönü: dış katmanlar içe bağımlı, iç katmanlar dışa bağımlı olmaz. Interface'ler ve Dependency Injection.",
                FilePath = null,
                Tags = "mimari,.NET,Clean Architecture",
                Category = "Genel",
                UserId = 1,
                CreatedAt = seedDate.AddDays(3),
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            },
            new Note
            {
                Id = 5,
                Title = "Entity Framework Core ve Migration",
                Description = "Code-First yaklaşımı, DbContext, Fluent API ve Data Annotations. Migration oluşturma (Add-Migration, Update-Database). HasData ile seed verisi ekleme.",
                FilePath = null,
                Tags = "EF Core,ORM,migration,veritabanı",
                Category = "Vize",
                UserId = 1,
                CreatedAt = seedDate.AddDays(4),
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            }
        );
    }
}

