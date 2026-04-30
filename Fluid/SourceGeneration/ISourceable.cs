namespace Fluid.SourceGeneration
{
    /// <summary>
    /// Implemented by AST nodes that can generate equivalent C# source code.
    /// </summary>
    public interface ISourceable
    {
        void WriteTo(SourceGenerationContext context);
    }
}
