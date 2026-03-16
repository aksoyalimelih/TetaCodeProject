using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TetaCode.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnsureDemoUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // demo@tetacode.com varsa şifresini "password" yap; yoksa kullanıcı ekle (Id=1 müsaitse Id=1 ile, değilse identity ile).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'demo@tetacode.com')
                BEGIN
                    UPDATE [Users] SET [FullName] = N'Demo Kullanıcı', [PasswordHash] = N'$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi'
                    WHERE [Email] = N'demo@tetacode.com';
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 1)
                    BEGIN
                        SET IDENTITY_INSERT [Users] ON;
                        INSERT INTO [Users] ([Id], [Email], [FullName], [PasswordHash])
                        VALUES (1, N'demo@tetacode.com', N'Demo Kullanıcı', N'$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi');
                        SET IDENTITY_INSERT [Users] OFF;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Users] ([Email], [FullName], [PasswordHash])
                        VALUES (N'demo@tetacode.com', N'Demo Kullanıcı', N'$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi');
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: demo kullanıcıyı silmeyiz (başka veriler etkilenebilir)
        }
    }
}
