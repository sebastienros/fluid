using System;
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
    private int _indentSize = 4;
    private string _indent;
    private StringBuilder _builder = new StringBuilder();
    private bool _newLine = true;

    public CompilationResult(int indentLevel = 0)
    {
        _indent = new string(' ', indentLevel * _indentSize);
    }

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

    public bool IsAsync { get; set; }

    public CompilationResult Indent()
    {
        _builder.Append(_indent);
        
        return this;
    }
    public CompilationResult Append(string text)
    {
        if (_newLine)
        {
            Indent();
        }

        WriteIndentedLines(text);

        return this;
    }

    public CompilationResult AppendLine()
    {
        _builder.AppendLine();
        _newLine = true;

        return this;
    }

    public CompilationResult AppendLine(string text)
    {
        if (_newLine)
        {
            Indent();
        }

        WriteIndentedLines(text);
        AppendLine();
        _newLine = true;

        return this;
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void WriteIndentedLines(string text)
    {
        foreach (var c in text)
        {
            _builder.Append(c);

            if (c == '\n')
            {
                Indent();
            }
        }
    }
}