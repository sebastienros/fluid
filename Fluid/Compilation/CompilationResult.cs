using System.Linq.Expressions;
using System.Text;
using Expression = System.Linq.Expressions.Expression;

namespace Fluid.Compilation;

/// <summary>
/// Every statement or expression that is compiled returns an instance of <see cref="CompilationResult"/> which encapsulates the statements to execute in order
/// to parse the expected input.
/// The convention is that these statements are returned in the <see cref="Body"/> property, and any variable that needs to be declared in the block
/// that the <see cref="Body"/> is used in are set in the <see cref="Variables"/> list.
/// The <see cref="Completion"/> property contains the result of the evaluation. For statements it's a <see cref="ValueTask{Completion}"/>, 
/// for expressions it's a <see cref="ValueTask{FluidValue}"/>
/// </summary>
public class CompilationResult
{
    /// <summary>
    /// Gets the list of <see cref="ParameterExpression"/> representing the variables used by the compiled result.
    /// </summary>
    public List<ParameterExpression> Variables { get; } = new();

    /// <summary>
    /// Gets the list of <see cref="Expression"/> representing the body of the compiled results.
    /// </summary>
    public List<Expression> Body { get; } = new();

    /// <summary>
    /// Gets or sets the <see cref="ParameterExpression"/> of the <see cref="ValueTask{Completion}"/> variable representing the value of the statement.
    /// </summary>
    public ParameterExpression Completion { get; set; }

    
    /// <summary>
    /// Gets or sets the <see cref="ParameterExpression"/> of the <see cref="ValueTask"/> variable representing the value of the template.
    /// </summary>
    public string Result { get; set; }

    public string Caller { get; set; }

    public StringBuilder StringBuilder { get; private set; } = new StringBuilder(4096);

    public bool IsAsync { get; set; }

}