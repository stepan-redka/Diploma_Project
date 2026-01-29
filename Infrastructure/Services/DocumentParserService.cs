using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using RagWebDemo.Core.Interfaces;
using RagWebDemo.Core.Entities;
using DocType = RagWebDemo.Core.Enums.DocumentType;

namespace RagWebDemo.Infrastructure.Services;

/// <summary>
/// Service for parsing various document formats into plain text
/// </summary>
public class DocumentParserService : IDocumentParserService
{
    private readonly ILogger<DocumentParserService> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".pdf", ".docx", ".md", ".markdown", ".html", ".htm", ".csv", ".json", ".xml"
    };

    public DocumentParserService(ILogger<DocumentParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if a file type is supported
    /// </summary>
    public bool IsSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return SupportedExtensions.Contains(extension);
    }

    /// <summary>
    /// Determine document type from file extension
    /// </summary>
    public DocType GetDocumentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".csv" or ".json" or ".xml" => DocType.PlainText,
            ".pdf" => DocType.Pdf,
            ".docx" => DocType.Docx,
            ".md" or ".markdown" => DocType.Markdown,
            ".html" or ".htm" => DocType.Html,
            _ => DocType.Unknown
        };
    }

    /// <summary>
    /// Parse a document stream into plain text
    /// </summary>
    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        var result = new ParsedDocument
        {
            FileName = fileName,
            Type = GetDocumentType(fileName)
        };

        try
        {
            _logger.LogInformation("Parsing document: {FileName} (Type: {Type})", fileName, result.Type);

            result.Content = result.Type switch
            {
                DocType.PlainText => await ParsePlainTextAsync(fileStream),
                DocType.Pdf => await ParsePdfAsync(fileStream),
                DocType.Docx => await ParseDocxAsync(fileStream),
                DocType.Markdown => await ParsePlainTextAsync(fileStream), // Markdown is text
                DocType.Html => await ParseHtmlAsync(fileStream),
                _ => throw new NotSupportedException($"Document type not supported: {Path.GetExtension(fileName)}")
            };

            result.Success = true;
            _logger.LogInformation("Successfully parsed {FileName}: {CharCount} characters", 
                fileName, result.CharacterCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document: {FileName}", fileName);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Parse plain text files (TXT, CSV, JSON, XML)
    /// </summary>
    private async Task<string> ParsePlainTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Parse PDF documents using PdfPig
    /// </summary>
    private Task<string> ParsePdfAsync(Stream stream)
    {
        var textBuilder = new StringBuilder();

        try
        {
            // Copy to memory stream since PdfPig needs seekable stream
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // Check if this is actually a PDF file by looking for PDF header
            var headerBytes = new byte[5];
            memoryStream.Read(headerBytes, 0, 5);
            memoryStream.Position = 0;
            
            var headerString = Encoding.ASCII.GetString(headerBytes);
            if (!headerString.StartsWith("%PDF"))
            {
                // Not a real PDF, treat as plain text
                _logger.LogWarning("File appears to be text file with .pdf extension, parsing as plain text");
                using var reader = new StreamReader(memoryStream, Encoding.UTF8);
                return Task.FromResult(reader.ReadToEnd());
            }

            // Try with lenient parsing options
            using var document = PdfDocument.Open(memoryStream, new ParsingOptions
            {
                // Don't throw on parsing errors
                UseLenientParsing = true
            });
            
            foreach (var page in document.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    textBuilder.AppendLine(pageText);
                    textBuilder.AppendLine(); // Add spacing between pages
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF, attempting fallback");
            
            // Fallback: try reading as plain text
            try
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var content = reader.ReadToEnd();
                
                if (!string.IsNullOrWhiteSpace(content) && !content.Contains("%PDF"))
                {
                    _logger.LogInformation("Successfully parsed as plain text fallback");
                    return Task.FromResult(content);
                }
            }
            catch
            {
                // Ignore fallback errors and throw original
            }
            
            throw new InvalidOperationException($"Unable to parse PDF document: {ex.Message}", ex);
        }

        return Task.FromResult(textBuilder.ToString().Trim());
    }

    /// <summary>
    /// Parse DOCX documents using OpenXML
    /// </summary>
    private Task<string> ParseDocxAsync(Stream stream)
    {
        var textBuilder = new StringBuilder();

        // Copy to memory stream since OpenXML needs seekable stream
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart?.Document.Body;

        if (body != null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textBuilder.AppendLine(text);
                }
            }
        }

        return Task.FromResult(textBuilder.ToString().Trim());
    }

    /// <summary>
    /// Parse HTML by stripping tags (basic implementation)
    /// </summary>
    private async Task<string> ParseHtmlAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var html = await reader.ReadToEndAsync();

        // Basic HTML tag stripping
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = System.Net.WebUtility.HtmlDecode(text);

        return text.Trim();
    }
}
