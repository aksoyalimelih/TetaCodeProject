using System.Text;
using UglyToad.PdfPig;

namespace TetaCode.Service.Services;

public static class PdfExtractionService
{
    public static string ExtractText(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        using var ms = new MemoryStream();
        pdfStream.CopyTo(ms);
        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            return string.Empty;

        using var document = PdfDocument.Open(bytes);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text.Trim());
                builder.AppendLine();
            }
        }

        return builder.ToString().Trim();
    }
}

