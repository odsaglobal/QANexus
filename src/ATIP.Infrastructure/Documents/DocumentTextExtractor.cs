using System.Text;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Enums;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace ATIP.Infrastructure.Documents;

/// <summary>
/// Extracts plain text from uploaded documents. PDF uses PdfPig, DOCX uses the Open XML SDK,
/// and text/markdown/swagger are read directly. Unknown types are treated as UTF-8 text.
/// </summary>
public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(
        Stream content,
        RequirementSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        // Buffer to a seekable MemoryStream so libraries that require seeking work reliably.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return sourceType switch
        {
            RequirementSourceType.Pdf => ExtractPdf(buffer),
            RequirementSourceType.Docx => ExtractDocx(buffer),
            _ => await ReadTextAsync(buffer, cancellationToken),
        };
    }

    private static string ExtractPdf(Stream stream)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(stream);
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString().Trim();
    }

    private static string ExtractDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        // InnerText concatenates runs without spacing; split on paragraphs for readability.
        foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        return sb.ToString().Trim();
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return text.Trim();
    }
}
