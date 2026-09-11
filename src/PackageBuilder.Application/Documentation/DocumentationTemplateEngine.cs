using System.Text;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace PackageBuilder.Application.Documentation;

/// <summary>Strict UTF-8 document with deterministic LF endings and no BOM.</summary>
public sealed class ReadmeDocument
{
    internal ReadmeDocument(string text)
    {
        Text = text;
        Utf8Bytes = Array.AsReadOnly(new UTF8Encoding(false, true).GetBytes(text));
    }

    public string Text { get; }
    public IReadOnlyList<byte> Utf8Bytes { get; }
}

/// <summary>Renders only the reviewed embedded template. No caller templates, CLR objects, functions or loaders are exposed.</summary>
internal static class DocumentationTemplateEngine
{
    private static readonly Lazy<Template> _template = new(() =>
    {
        using Stream stream = typeof(DocumentationTemplateEngine).Assembly.GetManifestResourceStream(
            "PackageBuilder.Application.Documentation.Templates.readme.sbn")!;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false);
        var template = Template.Parse(reader.ReadToEnd().ReplaceLineEndings("\n"));
        return template.HasErrors ? throw new InvalidOperationException("Embedded documentation template is invalid.") : template;
    });

    internal static DocumentationResult<ReadmeDocument> Render(string title, IReadOnlyList<string> identity,
        IReadOnlyList<(string Heading, string[] Lines)> sections, IReadOnlyList<ReadmeTable> tables,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        { return Failure("DOC_CANCELLED"); }
        var globals = new ScriptObject
        {
            ["title"] = DocumentationText.Escape(title),
            ["tables"] = new ScriptArray(tables.Select(table => new ScriptObject
            {
                ["heading"] = DocumentationText.Escape(table.Heading),
                ["headers"] = new ScriptArray(table.Headers.Select(DocumentationText.Escape)),
                ["rows"] = new ScriptArray(table.Rows.Select(row => new ScriptArray(row.Select(DocumentationText.Escape)))),
            })),
            ["identity"] = new ScriptArray(identity.Select(DocumentationText.Escape)),
            ["sections"] = new ScriptArray(sections.Select(section => new ScriptObject
            {
                ["heading"] = DocumentationText.Escape(section.Heading),
                ["lines"] = new ScriptArray(section.Lines.Select(DocumentationText.Escape)),
            })),
        };
        // All collections and fields are bounded before projection; the cap also prevents silent truncation.
        var context = new TemplateContext([])
        {
            StrictVariables = true,
            EnableRelaxedMemberAccess = false,
            EnableRelaxedIndexerAccess = false,
            LoopLimit = 32768,
            RecursiveLimit = 8,
            LimitToString = 8 * 1024 * 1024,
            CancellationToken = cancellationToken,
        };
        context.PushGlobal(globals);
        try
        {
            string text = _template.Value.Render(context).TrimEnd('\n') + "\n";
            return text.Length >= context.LimitToString - 4
                ? Failure("DOC_OUTPUT_LIMIT")
                : DocumentationResult<ReadmeDocument>.Success(new(text));
        }
        catch (ScriptAbortException) { return Failure("DOC_CANCELLED"); }
        catch (ScriptRuntimeException) { return Failure("DOC_TEMPLATE_FAILURE"); }
        catch (EncoderFallbackException) { return Failure("DOC_TEXT_INVALID"); }
    }

    private static DocumentationResult<ReadmeDocument> Failure(string code) => DocumentationResult<ReadmeDocument>.Failure(code);
}
