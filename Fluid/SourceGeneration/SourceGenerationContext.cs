using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Fluid.Ast;
using Fluid.Values;

namespace Fluid.SourceGeneration;

public sealed class SourceGenerationContext
{
    private readonly StringBuilder _sb;
    private int _indentLevel;
    private int _uniqueId;

    private readonly Dictionary<object, string> _statementMethods = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, string> _expressionMethods = new(ReferenceEqualityComparer.Instance);

    private readonly List<Statement> _pendingStatements = [];
    private readonly List<Expression> _pendingExpressions = [];

    private readonly Dictionary<string, string> _staticStrings = new(StringComparer.Ordinal);
    private readonly List<StaticMember> _staticMembers = [];

    public SourceGenerationContext(StringBuilder builder, SourceGenerationOptions options, IReadOnlyDictionary<string, string> renderTemplateTypeNames = null)
    {
        _sb = builder ?? throw new ArgumentNullException(nameof(builder));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        RenderTemplateTypeNames = renderTemplateTypeNames ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public SourceGenerationOptions Options { get; }

    public IReadOnlyDictionary<string, string> RenderTemplateTypeNames { get; }

    public string GetRenderTemplateTypeName(string path)
    {
        if (RenderTemplateTypeNames != null && RenderTemplateTypeNames.TryGetValue(path, out var typeName))
        {
            return typeName;
        }

        throw new SourceGenerationException($"No compiled template was registered for path '{path}'.");
    }

    /// <summary>
    /// Gets the statements a block writes. Like the interpreter, a block that only contains whitespace, comments and
    /// tags such as <c>assign</c> doesn't write its text.
    /// </summary>
    public static IReadOnlyList<Statement> GetWrittenStatements(IReadOnlyList<Statement> statements, bool isBlank)
    {
        if (!isBlank)
        {
            return statements;
        }

        var result = new List<Statement>(statements.Count);
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not TextSpanStatement)
            {
                result.Add(statements[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the value of a string literal, such as the path of an <c>include</c>, which is known at build time.
    /// </summary>
    public static bool TryGetLiteralString(Expression expression, out string value)
    {
        if (expression is LiteralExpression { Value: StringValue stringValue })
        {
            value = stringValue.Value;
            return true;
        }

        value = null;
        return false;
    }

    public Type ModelType => Options.ModelType;

    internal ForLoopUsageAnalysis ForLoopUsage { get; set; }

    /// <summary>
    /// Gets whether a loop has to maintain its <c>forloop</c> variable, because something can read it.
    /// </summary>
    /// <param name="loop">A <see cref="ForStatement"/>, or a <see cref="RenderStatement"/> or <see cref="IncludeStatement"/> with a <c>for</c> argument.</param>
    public bool UsesForLoop(Statement loop) => ForLoopUsage?.UsesForLoop(loop) ?? true;

    public string WriterName { get; set; } = "writer";
    public string EncoderName { get; set; } = "encoder";
    public string ContextName { get; set; } = "context";

    public void Write(string text) => _sb.Append(text);

    public void WriteLine(string text = "")
    {
        if (text.Length != 0)
        {
            _sb.Append(' ', _indentLevel * 4);
            _sb.Append(text);
        }

        _sb.AppendLine();
    }

    public IDisposable Indent()
    {
        _indentLevel++;
        return new IndentScope(this);
    }

    private sealed class IndentScope : IDisposable
    {
        private readonly SourceGenerationContext _ctx;

        public IndentScope(SourceGenerationContext ctx) => _ctx = ctx;

        public void Dispose() => _ctx._indentLevel--;
    }

    public string GetUniqueId(string prefix) => prefix + Interlocked.Increment(ref _uniqueId).ToString(CultureInfo.InvariantCulture);

    public string GetStatementMethodName(Statement statement)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(statement);
#else
        if (statement == null)
        {
            throw new ArgumentNullException(nameof(statement));
        }
#endif

        if (_statementMethods.TryGetValue(statement, out var name))
        {
            return name;
        }

        name = "Stmt_" + Interlocked.Increment(ref _uniqueId).ToString(CultureInfo.InvariantCulture);
        _statementMethods[statement] = name;
        _pendingStatements.Add(statement);
        return name;
    }

    public string GetExpressionMethodName(Expression expression)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(expression);
#else
        if (expression == null)
        {
            throw new ArgumentNullException(nameof(expression));
        }
#endif

        if (_expressionMethods.TryGetValue(expression, out var name))
        {
            return name;
        }

        name = "Expr_" + Interlocked.Increment(ref _uniqueId).ToString(CultureInfo.InvariantCulture);
        _expressionMethods[expression] = name;
        _pendingExpressions.Add(expression);
        return name;
    }

    /// <summary>
    /// Writes the statements of a block, returning the completion of the first one that doesn't complete normally.
    /// Text spans, which always do, are written inline instead of calling a method.
    /// </summary>
    /// <param name="statements">The statements to write.</param>
    /// <param name="incrementSteps">Whether each statement increments the steps of the template.</param>
    internal void WriteStatements(IReadOnlyList<Statement> statements, bool incrementSteps = false)
    {
        // Declared on first use, an unused variable is a warning in the generated code.
        var declared = false;

        for (var i = 0; i < statements.Count; i++)
        {
            if (incrementSteps)
            {
                WriteLine($"{ContextName}.IncrementSteps();");
            }

            if (statements[i] is TextSpanStatement textSpan)
            {
                textSpan.WriteTextTo(this);
                continue;
            }

            var methodName = GetStatementMethodName(statements[i]);
            WriteLine($"{(declared ? "" : "var ")}completion = await {methodName}({WriterName}, {EncoderName}, {ContextName});");
            WriteLine("if (completion != Completion.Normal) return completion;");
            declared = true;
        }
    }

    public IReadOnlyList<Statement> PendingStatements => _pendingStatements;
    public IReadOnlyList<Expression> PendingExpressions => _pendingExpressions;

    public string GetOrAddStaticString(string buffer, int offset, int length)
    {
        var literal = ToCSharpStringLiteral(buffer, offset, length);

        if (_staticStrings.TryGetValue(literal, out var name))
        {
            return name;
        }

        name = GetUniqueId("Text_");
        _staticStrings[literal] = name;
        _staticMembers.Add(new StaticMember("string", name, literal));
        return name;
    }

    public string GetOrAddStaticStringValue(string value)
    {
        var literal = ToCSharpStringLiteral(value);

        var key = "StringValue:" + literal;
        if (_staticStrings.TryGetValue(key, out var name))
        {
            return name;
        }

        name = GetUniqueId("StrVal_");
        _staticStrings[key] = name;
        _staticMembers.Add(new StaticMember("FluidValue", name, $"StringValue.Create({literal})"));
        return name;
    }

    /// <summary>
    /// Adds a static readonly field to the generated class and returns its name. Fields with the same type and
    /// initializer are shared, so it is only meant for immutable or thread-safe values.
    /// </summary>
    public string GetOrAddStaticField(string typeName, string prefix, string initializer)
    {
        var key = typeName + ":" + initializer;
        if (_staticStrings.TryGetValue(key, out var name))
        {
            return name;
        }

        name = AddStaticField(typeName, prefix, initializer);
        _staticStrings[key] = name;
        return name;
    }

    /// <summary>
    /// Adds a static readonly field to the generated class that is not shared with other call sites, like one that
    /// caches what a single call site resolves at render time, and returns its name.
    /// </summary>
    public string AddStaticField(string typeName, string prefix, string initializer)
    {
        var name = GetUniqueId(prefix);
        _staticMembers.Add(new StaticMember(typeName, name, initializer));
        return name;
    }

    /// <summary>
    /// Gets whether <see cref="TryGetLiteralValue"/> supports a value.
    /// </summary>
    public static bool IsSourceableLiteral(FluidValue value)
        => value is NilValue or EmptyValue or BlankValue or BooleanValue or NumberValue or StringValue;

    /// <summary>
    /// Gets a C# expression returning a literal value without allocating, like the name of a static field holding it.
    /// </summary>
    public bool TryGetLiteralValue(FluidValue value, out string expression)
    {
        switch (value)
        {
            case NilValue:
                expression = "NilValue.Instance";
                return true;
            case EmptyValue:
                expression = "EmptyValue.Instance";
                return true;
            case BlankValue:
                expression = "BlankValue.Instance";
                return true;
            case BooleanValue b:
                expression = b.Value ? "BooleanValue.True" : "BooleanValue.False";
                return true;
            case NumberValue number:
                // The literal keeps the scale of the number, which is part of its identity in Liquid (1.0 is not 1).
                var n = number.Value.ToString(CultureInfo.InvariantCulture);
                expression = GetOrAddStaticField("FluidValue", "NumVal_", $"NumberValue.Create({n}m)");
                return true;
            case StringValue stringValue:
                expression = GetOrAddStaticStringValue(stringValue.Value);
                return true;
            default:
                expression = null;
                return false;
        }
    }

    public void WriteStaticMembers()
    {
        if (_staticMembers.Count == 0)
        {
            return;
        }

        WriteLine();
        for (var i = 0; i < _staticMembers.Count; i++)
        {
            var member = _staticMembers[i];
            WriteLine($"private static readonly {member.TypeName} {member.FieldName} = {member.Initializer};");
        }
    }

    public static string ToCSharpStringLiteral(string value)
    {
        if (value == null)
        {
            return "null";
        }

        return ToCSharpStringLiteral(value.AsSpan());
    }

    public static string ToCSharpStringLiteral(string buffer, int offset, int length)
    {
        if (buffer == null)
        {
            return "null";
        }

        if ((uint)offset > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if ((uint)length > (uint)(buffer.Length - offset))
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return ToCSharpStringLiteral(buffer.AsSpan(offset, length));
    }

    public static string ToCSharpStringLiteral(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        // Rough heuristic: most strings have no escaping.
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    [DoesNotReturn]
    public static void ThrowNotSourceable(object node)
    {
        throw new SourceGenerationException($"{(node == null ? "<null>" : node.GetType().FullName)} is not compatible with source generation.");
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly struct StaticMember
    {
        public StaticMember(string typeName, string fieldName, string initializer)
        {
            TypeName = typeName;
            FieldName = fieldName;
            Initializer = initializer;
        }

        public string TypeName { get; }
        public string FieldName { get; }
        public string Initializer { get; }
    }
}
