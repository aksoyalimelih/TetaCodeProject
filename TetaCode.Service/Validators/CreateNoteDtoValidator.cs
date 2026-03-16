using FluentValidation;
using TetaCode.Service.Dtos;

namespace TetaCode.Service.Validators;

public class CreateNoteDtoValidator : AbstractValidator<CreateNoteDto>
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".png", ".doc", ".docx"
    };

    public CreateNoteDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ders Adı zorunludur.")
            .MaximumLength(100).WithMessage("Ders Adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Dosya zorunludur.")
            .Must(file => file is not null && file.Length > 0).WithMessage("Dosya boş olamaz.")
            .Must(file => file is not null && file.Length <= 5 * 1024 * 1024).WithMessage("Dosya boyutu en fazla 5MB olabilir.")
            .Must(file =>
            {
                if (file is null) return false;
                var ext = Path.GetExtension(file.FileName);
                return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
            })
            .WithMessage("Geçersiz dosya türü. İzin verilenler: pdf, jpg, png, doc, docx.");
    }
}

