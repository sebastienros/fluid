using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Text.Encodings.Web;

namespace Fluid.Compilation;

/// <summary>
/// Reprensents the context of a compilation phase, coordinating all the parsers involved.
/// </summary>
public class CompilationContext
{
    private int _number = 0;

    public CompilationContext() : this(TemplateOptions.Default)
    {
    }

    public CompilationContext(TemplateOptions options)
    {
        Options = options;
    }
    public int IndentLevel { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether the compiled code should generate the forloop helpers.
    /// </summary>
    /// <remarks>
    /// This is set by compilers when it detects that the forloop variable is used in a template.
    /// Not generating it will help performance at rendering since multiple unused variables can be skipped.
    /// </remarks>
    public bool GenerateFoorLoopProperty { get; set; }

    /// <summary>
    /// Gets the expression containing the the <see cref="TextWriter"/> instance for the compiled template.
    /// </summary>
    public string TextWriter { get; set; } = "writer";

    /// <summary>
    /// Gets the expression containing the the <see cref="TextEncoder"/> instance for the compiled template.
    /// </summary>
    public string TextEncoder { get; set; } = "encoder";

    /// <summary>
    /// Gets the expression containing the the <see cref="TemplateContext"/> instance for the compiled template.
    /// </summary>
    public string TemplateContext { get; set; } = "context";

    /// <summary>
    /// Gets or sets a counter used to generate unique variable names.
    /// </summary>
    public int NextNumber => _number++;

    /// <summary>
    /// Gets the list of global members to add to the static constructor of the template class.
    /// </summary>
    public List<string> GlobalMembers { get; } = new();

    /// <summary>
    /// Gets the list of static statements to add to the static constructor of the template class.
    /// </summary>
    public List<string> StaticStatements { get; } = new();

    /// <summary>
    /// Gets the list of global statements to add to the initializing phase of the template class.
    /// </summary>
    public List<string> GlobalStatements { get; } = new();

    /// <summary>
    /// Gets the list of shared lambda expressions representing intermediate statements or expressions.
    /// </summary>
    /// <remarks>
    /// This is used for debug only, in order to inpect the source generated for these intermediate parsers.
    /// </remarks>
    public List<Expression> Lambdas { get; } = new();

    public TemplateOptions Options { get; private set; }

    public string Caller { get; set; }

    public CompilationResult CreateCompilationResult()
    {
        return new CompilationResult(this.IndentLevel);
    }
    
    public void EnterLevel()
    {
        IndentLevel++;
    }

    public void ExitLevel()
    {
        IndentLevel--;
    }
}