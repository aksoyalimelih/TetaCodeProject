using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TetaCode.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed yalnızca tablolar boşsa veya kayıt yoksa uygulanır (idempotent).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 1)
                BEGIN
                    SET IDENTITY_INSERT [Users] ON;
                    INSERT INTO [Users] ([Id], [Email], [FullName], [PasswordHash])
                    VALUES (1, N'demo@tetacode.com', N'Demo Kullanıcı', N'$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi');
                    SET IDENTITY_INSERT [Users] OFF;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Notes] WHERE [Id] = 1)
                BEGIN
                    SET IDENTITY_INSERT [Notes] ON;
                    INSERT INTO [Notes] ([Id], [Category], [CreatedAt], [DeletedAt], [Description], [FilePath], [IsDeleted], [Tags], [Title], [UpdatedAt], [UserId])
                    VALUES
                    (1, N'Genel', '2025-03-01T10:00:00', NULL, N'OOP''nin dört temel prensibi: Encapsulation (kapsülleme), Inheritance (kalıtım), Polymorphism (çok biçimlilik) ve Abstraction (soyutlama). Sınıflar ve nesneler arasındaki ilişkiler, SOLID prensipleri ile sürdürülebilir kod yapısı.', NULL, 0, N'OOP,C#,programlama,temel', N'Nesne Yönelimli Programlama Temelleri', NULL, 1),
                    (2, N'Vize', '2025-03-02T10:00:00', NULL, N'1NF, 2NF, 3NF ve BCNF kuralları. Birincil ve yabancı anahtarlar, indeksler, sorgu optimizasyonu. İlişkisel model ve ER diyagramları.', NULL, 0, N'SQL,veritabanı,normalizasyon', N'Veritabanı Tasarımı ve Normalizasyon', NULL, 1),
                    (3, N'Final', '2025-03-03T10:00:00', NULL, N'GET, POST, PUT, PATCH, DELETE kullanımı. Status kodları (200, 201, 400, 401, 404, 500). JSON formatı, versioning ve güvenlik (JWT, API Key).', NULL, 0, N'API,REST,HTTP,backend', N'REST API ve HTTP Metodları', NULL, 1),
                    (4, N'Genel', '2025-03-04T10:00:00', NULL, N'Domain (Core), Application (Use Cases), Infrastructure (Data, API). Bağımlılık yönü: dış katmanlar içe bağımlı, iç katmanlar dışa bağımlı olmaz. Interface''ler ve Dependency Injection.', NULL, 0, N'mimari,.NET,Clean Architecture', N'Clean Architecture Katmanları', NULL, 1),
                    (5, N'Vize', '2025-03-05T10:00:00', NULL, N'Code-First yaklaşımı, DbContext, Fluent API ve Data Annotations. Migration oluşturma (Add-Migration, Update-Database). HasData ile seed verisi ekleme.', NULL, 0, N'EF Core,ORM,migration,veritabanı', N'Entity Framework Core ve Migration', NULL, 1);
                    SET IDENTITY_INSERT [Notes] OFF;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Notes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Notes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Notes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Notes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Notes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
