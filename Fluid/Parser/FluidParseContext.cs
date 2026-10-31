using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser;

public class FluidParseContext : ParseContext
{
    public FluidParseContext(string text) : base(new Scanner(text))
    {
        WhiteSpaceSet = LiquidWhiteSpace.Set;
    }

    public string PreviousRenderTag { get; set; }
    public int LiquidTagDepth { get; set; } // Used in the {% liquid %} tag to ensure a new line corresponds to '%}'

    private Parser<TextSpan> _whiteSpaceParserOutsideLiquidTag;

    /// <summary>
    /// Like in Shopify, each line of a <c>{% liquid %}</c> tag is a tag: inside one, the parsers only skip the whitespace
    /// of the line, so that the markup of a tag never continues on the next line.
    /// </summary>
    internal void EnterLiquidTag()
    {
        if (LiquidTagDepth++ == 0)
        {
            _whiteSpaceParserOutsideLiquidTag = WhiteSpaceParser;
            WhiteSpaceParser = LiquidWhiteSpace.SkipLine;
        }
    }

    internal void ExitLiquidTag()
    {
        if (--LiquidTagDepth == 0)
        {
            WhiteSpaceParser = _whiteSpaceParserOutsideLiquidTag;
        }
    }
}
