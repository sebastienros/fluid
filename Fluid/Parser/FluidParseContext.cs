using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser
{
    public class FluidParseContext : ParseContext
    {
        public FluidParseContext(string text) : this(text, 0)
        {
        }

        /// <param name="text">The template to parse.</param>
        /// <param name="maxRecursionDepth">The maximum nesting depth of the template, or <c>0</c> for no limit.</param>
        public FluidParseContext(string text, int maxRecursionDepth) : base(new Scanner(text))
        {
            MaxRecursionDepth = maxRecursionDepth;
        }

        /// <summary>
        /// Gets the maximum nesting depth of the template, or <c>0</c> for no limit.
        /// </summary>
        public int MaxRecursionDepth { get; }

        private int _recursionDepth;

        /// <summary>
        /// Records entry into a nested block or expression and throws when <see cref="MaxRecursionDepth"/> is exceeded.
        /// </summary>
        internal void EnterRecursion()
        {
            if (MaxRecursionDepth > 0 && _recursionDepth >= MaxRecursionDepth)
            {
                ThrowMaxRecursionDepthExceeded(this);
            }

            _recursionDepth++;
        }

        /// <summary>
        /// Records exit from a nested block or expression.
        /// </summary>
        internal void ExitRecursion()
        {
            if (_recursionDepth > 0)
            {
                _recursionDepth--;
            }
        }

        internal static void ThrowMaxRecursionDepthExceeded(ParseContext context)
        {
            var maxRecursionDepth = (context as FluidParseContext)?.MaxRecursionDepth ?? 0;
            throw new Parlot.ParseException($"The maximum parser recursion depth of {maxRecursionDepth} was exceeded.", context.Scanner.Cursor.Position);
        }

        public string PreviousRenderTag { get; set; }
        public TextSpanStatement PreviousTextSpanStatement { get; set; }
        public bool StripNextTextSpanStatement { get; set; }
        public bool PreviousIsTag { get; set; }
        public bool PreviousIsOutput { get; set; }
        public bool InsideLiquidTag { get; set; } // Used in the {% liquid %} tag to ensure a new line corresponds to '%}'
    }
}
