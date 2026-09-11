using System.Text;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace PackageBuilder.Application.Documentation;

/// <summary>Shared execution limits for reviewed embedded templates and primitive-only globals.
/// Callers own context-appropriate escaping; no CLR imports, builtins or template loaders are installed.</summary>
internal static class ReviewedTemplateRenderer
{
    internal static DocumentationResult<string> Render(Template template, ScriptObject globals,
        int maximumCharacters, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        { return new(null, "DOC_CANCELLED"); }
        var context = new TemplateContext([])
        {
            StrictVariables = true,
            EnableRelaxedMemberAccess = false,
            EnableRelaxedIndexerAccess = false,
            LoopLimit = 32768,
            RecursiveLimit = 8,
            LimitToString = maximumCharacters,
            CancellationToken = cancellationToken,
        };
        context.PushGlobal(globals);
        try
        {
            string text = template.Render(context).TrimEnd('\n') + "\n";
            _ = new UTF8Encoding(false, true).GetByteCount(text);
            return text.Length >= maximumCharacters - 4 ? new(null, "DOC_OUTPUT_LIMIT") : new(text, null);
        }
        catch (ScriptAbortException) { return new(null, "DOC_CANCELLED"); }
        catch (ScriptRuntimeException) { return new(null, "DOC_TEMPLATE_FAILURE"); }
        catch (EncoderFallbackException) { return new(null, "DOC_TEXT_INVALID"); }
    }
}
