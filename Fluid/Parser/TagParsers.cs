using Fluid.Ast;
using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Fluid.Parser;

public struct ForModifier
{
    public bool IsReversed;
    public bool IsLimit;
    public bool IsOffset;
    public Expression Value;
}

public struct TableRowModifier
{
    public bool IsCols;
    public bool IsLimit;
    public bool IsOffset;
    public Expression Value;
}

public readonly struct TagResult
{
    public static readonly TagResult TagOpen = new(true, false);
    public static readonly TagResult TagOpenTrim = new(true, true);
    public static readonly TagResult TagClose = new(false, false);
    public static readonly TagResult TagCloseTrim = new(false, true);

    public TagResult(bool open, bool trim)
    {
        Open = open;
        Trim = trim;
    }

    public readonly bool Open;
    public readonly bool Trim;
}

/// <summary>
/// The tag and output delimiters. They only read the delimiters: the trimming they request is read from the template
/// around each text (see <see cref="Ast.TextSpanStatement"/>), so they have no side effects on the context.
/// </summary>
public static class TagParsers
{
    /// <summary>
    /// Inside a <c>{% liquid %}</c> tag a tag doesn't start with <c>{%</c>.
    /// </summary>
    public static Parser<TagResult> TagStart() => If<FluidParseContext, TagResult>(static context => context.LiquidTagDepth > 0,
        Always(TagResult.TagOpen),
        NonInlineLiquidTagParsers.TagStart());

    /// <summary>
    /// Search for `%}`, `-%}` to close a tag.
    /// Also, if the tag is inside a `liquid` tag, it will only look for a new line to close the tag.
    /// Note: In {% liquid %} tags, only \n (or \r\n) is valid for line termination, not \r alone.
    /// </summary>
    public static Parser<TagResult> TagEnd() => If<FluidParseContext, TagResult>(static context => context.LiquidTagDepth > 0,
        LiquidTagLineEnd(),
        SkipWhiteSpace(NonInlineLiquidTagParsers.TagEnd()));

    public static Parser<TagResult> OutputTagStart() => NonInlineLiquidTagParsers.OutputTagStart();
    public static Parser<TagResult> OutputTagEnd() => SkipWhiteSpace(NonInlineLiquidTagParsers.OutputTagEnd());

    // A tag of a liquid tag ends with its line, or before the '%}' which ends the liquid tag, without reading it.
    private static Parser<TagResult> LiquidTagLineEnd() => OneOf(
        LiquidWhiteSpace.SkipLine.SkipAnd(OneOrMany(OneOf(Literals.Text("\n"), Literals.Text("\r\n")))).Then(TagResult.TagClose),
        Always(TagResult.TagClose).WhenFollowedBy(LiquidWhiteSpace.SkipLine.SkipAnd(NonInlineLiquidTagParsers.TagEnd())));
}

public static class NonInlineLiquidTagParsers
{
    public static Parser<TagResult> TagStart() => Open("{%");
    public static Parser<TagResult> TagEnd() => Close("%}");
    public static Parser<TagResult> OutputTagStart() => Open("{{");
    public static Parser<TagResult> OutputTagEnd() => Close("}}");

    private static Parser<TagResult> Open(string delimiter) => Literals.Text(delimiter)
        .SkipAnd(Literals.Char('-').Optional())
        .Then(static trim => trim.HasValue ? TagResult.TagOpenTrim : TagResult.TagOpen);

    private static Parser<TagResult> Close(string delimiter) => OneOf(
        Literals.Text("-" + delimiter).Then(TagResult.TagCloseTrim),
        Literals.Text(delimiter).Then(TagResult.TagClose));
}
