using Parlot;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class TextSpanStatement : Statement, ISynchronousSourceable
{
    // Volatile: the flag is read without the lock, and must not be seen set before _preparedBuffer is (on ARM64 it
    // could, and a concurrent render would write nothing).
    private volatile bool _isBufferPrepared;
    private readonly Lock _synLock = new();
    private TextSpan _text;
    internal string _preparedBuffer;
    private readonly bool _isWhitespaceOrCommentOnly;

    public TextSpanStatement(in TextSpan text)
    {
        _text = text;

        _isWhitespaceOrCommentOnly = IsBlank(_text.Span);
    }

    public TextSpanStatement(string text) : this(new TextSpan(text))
    {
    }

    public bool StripLeft { get; set; }
    public bool StripRight { get; set; }

    public bool NextIsTag { get; set; }
    public bool NextIsOutput { get; set; }
    public bool PreviousIsTag { get; set; }
    public bool PreviousIsOutput { get; set; }

    public ref readonly TextSpan Text => ref _text;

    public string Buffer => _preparedBuffer;

    public override bool IsWhitespaceOrCommentOnly => _isWhitespaceOrCommentOnly;
    public void PrepareBuffer(TemplateOptions options)
    {
        if (_isBufferPrepared)
        {
            return;
        }

        // Prevent two threads from stripping the same statement in case WriteToAsync is called concurrently
        lock (_synLock)
        {
            if (!_isBufferPrepared)
            {
                var trimming = options.Trimming;
                StripLeft |=
                    (PreviousIsTag && (trimming & TrimmingFlags.TagRight) != 0) ||
                    (PreviousIsOutput && (trimming & TrimmingFlags.OutputRight) != 0)
                    ;

                StripRight |=
                    (NextIsTag && (trimming & TrimmingFlags.TagLeft) != 0) ||
                    (NextIsOutput && (trimming & TrimmingFlags.OutputLeft) != 0)
                    ;

                _text = Trim(_text, StripLeft, StripRight, options.Greedy);
                _preparedBuffer = _text.ToString();
                _isBufferPrepared = true;
            }
        }
    }

    // Like Shopify, a text is blank with the chars of Ruby's \s, and the whitespace control trims them and NUL, like Ruby's strip:
    // the Unicode spaces, such as a no-break space, are content.
    private static bool IsLiquidWhiteSpace(char c) => c == ' ' || (uint)(c - '\t') <= '\r' - '\t';

    private static bool IsTrimmed(char c) => IsLiquidWhiteSpace(c) || c == '\0';

    private static bool IsBlank(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!IsLiquidWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Removes the whitespace on each requested side, the way the <c>-</c> markers and <see cref="TrimmingFlags"/> do.
    /// </summary>
    internal static TextSpan Trim(TextSpan text, bool stripLeft, bool stripRight, bool greedy)
    {
        var span = text.Buffer;
        var start = 0;
        var end = text.Length - 1;

        // Does this text need to have its left part trimmed?
        if (stripLeft)
        {
            var firstNewLine = -1;

            for (var i = start; i <= end; i++)
            {
                var c = span[text.Offset + i];

                if (IsTrimmed(c))
                {
                    start++;

                    if (firstNewLine == -1 && (c == '\n'))
                    {
                        firstNewLine = start;
                    }
                }
                else
                {
                    break;
                }
            }

            if (!greedy)
            {
                if (firstNewLine != -1)
                {
                    start = firstNewLine;
                }
            }
        }

        // Does this text need to have its right part trimmed?
        if (stripRight)
        {
            var lastNewLine = -1;

            for (var i = end; i >= start; i--)
            {
                var c = span[text.Offset + i];

                if (IsTrimmed(c))
                {
                    if (lastNewLine == -1 && c == '\n')
                    {
                        lastNewLine = end;
                    }

                    end--;
                }
                else
                {
                    break;
                }
            }

            if (!greedy)
            {
                if (lastNewLine != -1)
                {
                    end = lastNewLine;
                }
            }
        }
        if (end - start + 1 == 0)
        {
            return "";
        }

        if (start != 0 || end != text.Length - 1)
        {
            return new TextSpan(text.Buffer, text.Offset + start, end - start + 1);
        }

        return text;

    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitTextSpanStatement(this);

    public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        if (!_isBufferPrepared)
        {
            PrepareBuffer(context.Options);
        }

        if (_preparedBuffer == "")
        {
            return Statement.NormalCompletion;
        }

        context.IncrementSteps();

        // The Text fragments are not encoded, but kept as-is

        output.Write(_preparedBuffer);

        return Statement.NormalCompletion;
    }

    public void WriteTo(SourceGenerationContext context)
    {
        WriteTextTo(context);
        context.WriteLine("return new ValueTask<Completion>(Completion.Normal);");
    }

    bool ISynchronousSourceable.IsSynchronous => true;

    /// <summary>
    /// Emits the code writing this text, without returning a completion.
    /// </summary>
    internal void WriteTextTo(SourceGenerationContext context)
    {
        // TemplateOptions.Trimming and Greedy are only known at render time. Every text they can produce is
        // computed here, and the generated code picks one. Most spans have a single variant, written directly.
        var leftTerms = new List<string>(2);
        if (!StripLeft)
        {
            if (PreviousIsTag) leftTerms.Add("(trimming & TrimmingFlags.TagRight) != 0");
            if (PreviousIsOutput) leftTerms.Add("(trimming & TrimmingFlags.OutputRight) != 0");
        }

        var rightTerms = new List<string>(2);
        if (!StripRight)
        {
            if (NextIsTag) rightTerms.Add("(trimming & TrimmingFlags.TagLeft) != 0");
            if (NextIsOutput) rightTerms.Add("(trimming & TrimmingFlags.OutputLeft) != 0");
        }

        var leftCondition = leftTerms.Count > 0 ? string.Join(" || ", leftTerms) : null;
        var rightCondition = rightTerms.Count > 0 ? string.Join(" || ", rightTerms) : null;
        var greedyCondition = StripLeft || StripRight || leftCondition != null || rightCondition != null
            ? $"{context.ContextName}.Options.Greedy"
            : null;

        var hasEmptyVariant = false;
        var hasTextVariant = false;

        string Variant(bool stripLeft, bool stripRight, bool greedy)
        {
            var text = Trim(_text, stripLeft, stripRight, greedy);
            if (text.Length == 0)
            {
                hasEmptyVariant = true;
                return "string.Empty";
            }

            hasTextVariant = true;
            return context.GetOrAddStaticString(text.Buffer, text.Offset, text.Length);
        }

        static string Select(string condition, string whenTrue, string whenFalse)
            => whenTrue == whenFalse ? whenTrue : $"(({condition}) ? {whenTrue} : {whenFalse})";

        string SelectRight(bool stripLeft, bool greedy)
            => rightCondition == null
                ? Variant(stripLeft, StripRight, greedy)
                : Select(rightCondition, Variant(stripLeft, true, greedy), Variant(stripLeft, false, greedy));

        string SelectLeft(bool greedy)
            => leftCondition == null
                ? SelectRight(StripLeft, greedy)
                : Select(leftCondition, SelectRight(true, greedy), SelectRight(false, greedy));

        var expression = greedyCondition == null
            ? SelectLeft(greedy: true)
            : Select(greedyCondition, SelectLeft(greedy: true), SelectLeft(greedy: false));

        if (!hasTextVariant)
        {
            return;
        }

        // A single variant is a field name or string.Empty, a choice is a conditional expression.
        if (!hasEmptyVariant && expression[0] != '(')
        {
            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"{context.WriterName}.Write({expression});");
            return;
        }

        context.WriteLine("{");
        using (context.Indent())
        {
            if (leftCondition != null || rightCondition != null)
            {
                context.WriteLine($"var trimming = {context.ContextName}.Options.Trimming;");
            }

            context.WriteLine($"var text = {expression};");

            // The interpreter doesn't count a step for a text that was trimmed away.
            context.WriteLine("if (text.Length != 0)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.IncrementSteps();");
                context.WriteLine($"{context.WriterName}.Write(text);");
            }
            context.WriteLine("}");
        }
        context.WriteLine("}");
    }
}
