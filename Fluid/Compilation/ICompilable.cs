namespace Fluid.Compilation;

public interface ICompilable
{
    /// <summary>
    /// Creates a compiled representation of a statement or expression.
    /// </summary>
    /// <param name="context">The current compilation context.</param>
    CompilationResult Compile(CompilationContext context);
}
