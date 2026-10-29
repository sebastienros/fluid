using Fluid.Ast;

namespace Fluid.SourceGeneration;

/// <summary>
/// Finds the loops whose <c>forloop</c> variable a compiled template can read, so that the other loops don't
/// create and update it.
/// </summary>
/// <remarks>
/// A loop is a <see cref="ForStatement"/>, or a <see cref="RenderStatement"/> or <see cref="IncludeStatement"/>
/// with a <c>for</c> argument. The analysis follows everything that runs inside a loop: its body, the templates
/// it includes (which share its scope) and the macros it calls (which run in the caller's scope). A template
/// rendered with <c>render</c> is isolated and only sees the loop of a <c>render ... for</c>.
///
/// The analysis is conservative: a loop keeps its variable as soon as something it can't follow runs inside it,
/// such as a filter that isn't built in, a function that isn't a macro of the template, or a variable whose name
/// is computed. Only .NET code that reads the variable through the <see cref="TemplateContext"/>, from a member
/// accessor, a value converter, a context callback or a custom value, can't be detected.
/// </remarks>
internal sealed class ForLoopUsageAnalysis : AstVisitor
{
    private static readonly HashSet<string> BuiltInFilters = GetBuiltInFilters();

    private readonly Func<string, IStatementList> _resolveTemplate;
    private readonly Dictionary<string, List<MacroStatement>> _macros;
    private readonly HashSet<Statement> _loopsUsingVariable = [];
    private readonly HashSet<string> _templatesInProgress = new(StringComparer.Ordinal);
    private readonly HashSet<MacroStatement> _macrosInProgress = [];

    // The loops enclosing the node being visited, innermost last.
    private List<Statement> _loops = [];

    private ForLoopUsageAnalysis(Func<string, IStatementList> resolveTemplate, Dictionary<string, List<MacroStatement>> macros)
    {
        _resolveTemplate = resolveTemplate;
        _macros = macros;
    }

    /// <summary>
    /// Analyzes a template and the templates it references.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="referencedTemplates">The templates referenced by <c>render</c>, <c>include</c> and <c>from</c>, by path.</param>
    public static ForLoopUsageAnalysis Analyze(IStatementList template, IReadOnlyDictionary<string, IStatementList> referencedTemplates)
    {
        // Macros are called by name, and can be defined by any of the templates.
        var macros = new Dictionary<string, List<MacroStatement>>(StringComparer.Ordinal);
        var collector = new MacroCollector(macros);
        collector.VisitStatements(template.Statements);

        foreach (var referencedTemplate in referencedTemplates.Values)
        {
            collector.VisitStatements(referencedTemplate.Statements);
        }

        var analysis = new ForLoopUsageAnalysis(
            path => referencedTemplates.TryGetValue(path, out var statements) ? statements : null,
            macros);

        analysis.VisitStatements(template.Statements);

        return analysis;
    }

    /// <summary>
    /// Gets whether the <c>forloop</c> variable of a loop can be read.
    /// </summary>
    public bool UsesForLoop(Statement loop) => _loopsUsingVariable.Contains(loop);

    private void VisitStatements(IReadOnlyList<Statement> statements)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            Visit(statements[i]);
        }
    }

    protected internal override Statement VisitForStatement(ForStatement forStatement)
    {
        // The source and modifiers are evaluated before the loop starts, and the else block runs without it.
        Visit(forStatement.Source);
        Visit(forStatement.Limit);
        Visit(forStatement.Offset);

        _loops.Add(forStatement);
        VisitStatements(forStatement.Statements);
        _loops.RemoveAt(_loops.Count - 1);

        Visit(forStatement.Else);

        return forStatement;
    }

    protected internal override Statement VisitIncludeStatement(IncludeStatement includeStatement)
    {
        Visit(includeStatement.With);
        Visit(includeStatement.For);

        foreach (var assignStatement in includeStatement.AssignStatements)
        {
            Visit(assignStatement);
        }

        if (!SourceGenerationContext.TryGetLiteralString(includeStatement.Path, out var path) ||
            _resolveTemplate(path) is not { } template)
        {
            UseAllEnclosingLoops();
            return includeStatement;
        }

        // A template that includes itself is followed once.
        if (!_templatesInProgress.Add("include:" + path))
        {
            UseAllEnclosingLoops();
            return includeStatement;
        }

        // The included template runs in the scope of the include, inside its loop when there is one.
        if (includeStatement.For != null)
        {
            _loops.Add(includeStatement);
        }

        VisitStatements(template.Statements);

        if (includeStatement.For != null)
        {
            _loops.RemoveAt(_loops.Count - 1);
        }

        _templatesInProgress.Remove("include:" + path);

        return includeStatement;
    }

    protected internal override Statement VisitRenderStatement(RenderStatement renderStatement)
    {
        Visit(renderStatement.With);
        Visit(renderStatement.For);

        foreach (var assignStatement in renderStatement.AssignStatements)
        {
            Visit(assignStatement);
        }

        if (_resolveTemplate(renderStatement.Path) is not { } template)
        {
            UseAllEnclosingLoops();

            if (renderStatement.For != null)
            {
                _loopsUsingVariable.Add(renderStatement);
            }

            return renderStatement;
        }

        // The rendered template is isolated: the only loop it sees is the one of a render ... for.
        var enclosingLoops = _loops;
        _loops = [];

        if (renderStatement.For != null)
        {
            _loops.Add(renderStatement);
        }

        if (_templatesInProgress.Add("render:" + renderStatement.Path))
        {
            VisitStatements(template.Statements);
            _templatesInProgress.Remove("render:" + renderStatement.Path);
        }
        else
        {
            // Its loops are already analyzed by the render in progress, only this loop is left.
            UseAllEnclosingLoops();
        }

        _loops = enclosingLoops;

        return renderStatement;
    }

    protected internal override Statement VisitMacroStatement(MacroStatement macroStatement)
    {
        foreach (var argument in macroStatement.Arguments)
        {
            Visit(argument.Expression);
        }

        // The body runs where the macro is called, which VisitMemberExpression follows. Its own loops are analyzed
        // here too, in case the macro is never called by the template.
        var enclosingLoops = _loops;
        _loops = [];
        VisitStatements(macroStatement.Statements);
        _loops = enclosingLoops;

        return macroStatement;
    }

    protected internal override Expression VisitFilterExpression(FilterExpression filterExpression)
    {
        // A custom filter receives the TemplateContext and can read the variable.
        if (!BuiltInFilters.Contains(filterExpression.Name))
        {
            UseAllEnclosingLoops();
        }

        return base.VisitFilterExpression(filterExpression);
    }

    protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
    {
        var segments = memberExpression.Segments;

        AnalyzeVariableAccess(segments);
        AnalyzeFunctionCalls(segments);

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case IndexerSegment indexer:
                    Visit(indexer.Expression);
                    break;
                case FunctionCallSegment call:
                    foreach (var argument in call.Arguments)
                    {
                        Visit(argument.Expression);
                    }
                    break;
            }
        }

        return memberExpression;
    }

    private void AnalyzeVariableAccess(IReadOnlyList<MemberSegment> segments)
    {
        if (segments.Count == 0 || !TryGetName(segments[0], out var variable))
        {
            // A variable whose name is computed, like {{ [name] }}, can be the loop variable.
            UseAllEnclosingLoops();
            return;
        }

        // forloop is the innermost loop, parentloop and each .parentloop the loop around it.
        int outerLoops;
        if (IsName(variable, "forloop"))
        {
            outerLoops = 0;
        }
        else if (IsName(variable, "parentloop"))
        {
            outerLoops = 1;
        }
        else
        {
            return;
        }

        var i = 1;
        string property = null;
        while (i < segments.Count && TryGetName(segments[i], out property) && IsName(property, "parentloop"))
        {
            outerLoops++;
            i++;
        }

        if (_loops.Count == 0)
        {
            // Outside of any loop of this template, the variable comes from the model or a caller.
            return;
        }

        // A chain that goes past the loops of this template still goes through them.
        var loop = Math.Max(0, _loops.Count - 1 - outerLoops);

        // The walk stops on the first segment that isn't parentloop: a named one is a property, anything else
        // (a computed index or a call) leaves property null.
        var first = i < segments.Count && property != null
            // A property of a loop, like forloop.index or forloop.parentloop.index. The loops in between are
            // used too, since the outer loop is reached through their variable, and parentloop is set by them.
            ? loop
            // The loop value itself is used, or one of its properties can't be named: its outer loops can be
            // read through it.
            : 0;

        for (var j = first; j < _loops.Count; j++)
        {
            _loopsUsingVariable.Add(_loops[j]);
        }
    }

    private void AnalyzeFunctionCalls(IReadOnlyList<MemberSegment> segments)
    {
        var calls = 0;
        foreach (var segment in segments)
        {
            if (segment is FunctionCallSegment)
            {
                calls++;
            }
        }

        if (calls == 0)
        {
            return;
        }

        // A macro of the template, like {{ field('name') }}, runs in the scope of the call.
        if (calls == 1 && segments.Count == 2 && segments[1] is FunctionCallSegment &&
            TryGetName(segments[0], out var name) &&
            _macros.TryGetValue(NormalizeName(name), out var macros))
        {
            foreach (var macro in macros)
            {
                if (!_macrosInProgress.Add(macro))
                {
                    // A recursive macro is followed once.
                    UseAllEnclosingLoops();
                    continue;
                }

                VisitStatements(macro.Statements);
                _macrosInProgress.Remove(macro);
            }

            return;
        }

        // Any other function can be .NET code that receives the TemplateContext.
        UseAllEnclosingLoops();
    }

    private void UseAllEnclosingLoops()
    {
        foreach (var loop in _loops)
        {
            _loopsUsingVariable.Add(loop);
        }
    }

    private static bool TryGetName(MemberSegment segment, out string name)
    {
        switch (segment)
        {
            case IdentifierSegment identifier:
                name = identifier.Identifier;
                return true;
            case IndexerSegment indexer when SourceGenerationContext.TryGetLiteralString(indexer.Expression, out var literal):
                name = literal;
                return true;
            default:
                name = null;
                return false;
        }
    }

    // Variable names are compared with TemplateOptions.ModelNamesComparer, which is only known at render time.
    // Ignoring case and underscores matches a name the way any of the built-in comparers can.
    private static bool IsName(string name, string expected) => NormalizeName(name) == expected;

    private static string NormalizeName(string name) => name.Replace("_", "").ToLowerInvariant();

    private static HashSet<string> GetBuiltInFilters()
    {
        var options = new TemplateOptionsBuilder().WithColorFilters().WithMoneyFilters().Build();
        var filters = new HashSet<string>(StringComparer.Ordinal);

        foreach (var filter in options.Filters)
        {
            filters.Add(filter.Key);
        }

        return filters;
    }

    private sealed class MacroCollector : AstVisitor
    {
        private readonly Dictionary<string, List<MacroStatement>> _macros;

        public MacroCollector(Dictionary<string, List<MacroStatement>> macros)
        {
            _macros = macros;
        }

        public void VisitStatements(IReadOnlyList<Statement> statements)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                Visit(statements[i]);
            }
        }

        protected internal override Statement VisitMacroStatement(MacroStatement macroStatement)
        {
            var name = NormalizeName(macroStatement.Identifier);
            if (!_macros.TryGetValue(name, out var macros))
            {
                _macros[name] = macros = [];
            }

            macros.Add(macroStatement);

            return base.VisitMacroStatement(macroStatement);
        }
    }
}
