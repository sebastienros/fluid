namespace Fluid
{
    public class CompilerOptions
    {
        public static readonly CompilerOptions Default = new();

        /// <summary>
        /// Whether to limit the number of operations executed or not. Default is <code>true</code>.
        /// </summary>
        /// <remarks>
        /// If the source of the script is trusted, this option should can be disabled to improve performance.
        /// </remarks>
        public bool LimitMaxSteps { get; set; } = true;
    }
}
