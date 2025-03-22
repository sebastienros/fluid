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
    }
}
