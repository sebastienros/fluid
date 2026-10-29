namespace Fluid.SourceGeneration;

/// <summary>
/// Implemented by AST nodes that can generate a method returning its <see cref="ValueTask{TResult}"/> directly instead of
/// awaiting, so that it is not compiled to an async state machine. This is much cheaper for the common case of a node that
/// completes synchronously, and the generated method handles the asynchronous case itself.
/// </summary>
internal interface ISynchronousSourceable : ISourceable
{
    /// <summary>
    /// Gets whether <see cref="ISourceable.WriteTo"/> writes the body of a method that is not <c>async</c>.
    /// </summary>
    bool IsSynchronous { get; }
}
