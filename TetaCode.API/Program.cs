using Microsoft.AspNetCore.Authentication.JwtBearer;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TetaCode.Data;
using TetaCode.API.Middleware;
using TetaCode.Service.Services;
using TetaCode.Service.Validators;

var builder = WebApplication.CreateBuilder(args);

// DbContext kaydı (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Service katmanı
builder.Services.AddHttpClient();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOCRService, OCRService>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IAudioService, AudioService>();
builder.Services.AddScoped<ISynthesisService, SynthesisService>();

// JWT Authentication (env var öncelikli; yoksa appsettings Jwt:Key)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = builder.Configuration["TETACODE_JWT_KEY"] ?? jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == "__SET_IN_ENV__")
{
    throw new InvalidOperationException("JWT key bulunamadı. appsettings.json 'Jwt:Key' veya TETACODE_JWT_KEY environment variable ayarlayın.");
}

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateNoteDtoValidator>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("FrontendCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
