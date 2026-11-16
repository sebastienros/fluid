namespace Fluid
{
    /// <summary>
    /// Parser options.
    /// </summary>
    public class FluidParserOptions
    {
        /// <summary>
        /// Gets whether functions are allowed in templates. Default is <c>false</c>.
        /// </summary>
        public bool AllowFunctions { get; set; }

        /// <summary>
        /// Gets whether parentheses are allowed in templates. Default is <c>false</c>.
        /// </summary>
        public bool AllowParentheses { get; set; }

        /// <summary>
        /// Gets whether the inline liquid tag is allowed in templates. Default is <c>false</c>.
        /// </summary>
        public bool AllowLiquidTag { get; set; }

        /// <summary>
        /// Gets whether identifiers can end with a question mark (`?`), which will be stripped during parsing. Default is <c>false</c>.
        /// </summary>
        public bool AllowTrailingQuestionMark { get; set; }

        /// <summary>
        /// The default value of <see cref="MaxRecursionDepth"/>.
        /// </summary>
        public const int DefaultMaxRecursionDepth = 100;

        /// <summary>
        /// Gets the maximum nesting depth of a template: nested blocks, nested expressions, and the number of filters or
        /// <c>and</c>/<c>or</c> operators of an expression. Parsing a template that goes deeper fails, which prevents a
        /// template from exhausting the stack when it is parsed or rendered. Default is <see cref="DefaultMaxRecursionDepth"/>,
        /// <c>0</c> means unlimited.
        /// </summary>
        public int MaxRecursionDepth
        {
            get => _maxRecursionDepth;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum recursion depth can't be negative.");
                }

                _maxRecursionDepth = value;
            }
        }

        private int _maxRecursionDepth = DefaultMaxRecursionDepth;
    }
}
