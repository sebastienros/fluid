using Fluid.Utils;
using Fluid.Values;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class ForStatement : TagStatement, ISourceable
{
    private readonly string _continueSourceLiteral;

    // Precomputed for every source shape but a range. Both `forloop.name` and the `offset: continue`
    // key are then constant for the lifetime of the statement, so they don't need to be rebuilt
    // (with LINQ and string.Join) on every execution of the loop.
    private readonly string _staticContinueOffsetLiteral;

    // The source when it is a range, and its `forloop.name` part, like "(1..3)", when the bounds are literals.
    private readonly RangeExpression _rangeSource;
    private readonly string _staticRangeSourceLiteral;

    // Statements never change after construction, and PrepareBuffer can only shrink a TextSpanStatement
    // by trimming whitespace, so a whitespace-only body stays whitespace-only.
    private readonly bool _isBlank;

    public ForStatement(
        IReadOnlyList<Statement> statements,
        string identifier,
        Expression source,
        Expression limit,
        Expression offset,
        bool reversed,
        ElseStatement elseStatement = null
    ) : base(statements)
    {
        Identifier = identifier;
        Source = source;
        Limit = limit;
        Offset = offset;
        Reversed = reversed;
        Else = elseStatement;

        OffsetIsContinue = Offset is MemberExpression l && l.Segments.Count == 1 && l.Segments[0] is IdentifierSegment { Identifier: "continue" };
        _continueSourceLiteral = source is MemberExpression m
            ? string.Join(".", m.Segments.Select(s => (s as IdentifierSegment)?.Identifier).Where(s => s != null))
            : null;

        if (!string.IsNullOrEmpty(_continueSourceLiteral))
        {
            _staticContinueOffsetLiteral = $"for_continue_{Identifier}-{_continueSourceLiteral}";
        }
        else if (source is RangeExpression range)
        {
            _rangeSource = range;

            // A range written with literal bounds, like (1..3), has a name that never changes either.
            if (TryGetLiteralBound(range.From, out var from) && TryGetLiteralBound(range.To, out var to))
            {
                _staticRangeSourceLiteral = BuildRangeSourceLiteral(from, to);
                _staticContinueOffsetLiteral = BuildRangeContinueOffsetLiteral(_staticRangeSourceLiteral);
            }
        }
        else
        {
            // Fallback: stable within the current render (statement instance).
            _staticContinueOffsetLiteral = $"for_continue_{Identifier}-stmt_" + RuntimeHelpers.GetHashCode(this).ToString(CultureInfo.InvariantCulture);
        }

        // Like Liquid, a loop whose body and else branch only contain whitespace, comments or
        // assignments is blank: its whitespace isn't rendered.
        var isBlank = Else == null || Else.IsWhitespaceOrCommentOnly;
        for (var i = 0; isBlank && i < Statements.Count; i++)
        {
            isBlank = Statements[i].IsWhitespaceOrCommentOnly;
        }

        _isBlank = isBlank;
        Else?.SetEnclosingTagIsBlank(isBlank);
    }

    public override bool IsWhitespaceOrCommentOnly => _isBlank;

    public string Identifier { get; }
    public RangeExpression Range { get; }
    public Expression Source { get; }
    public Expression Limit { get; }
    public Expression Offset { get; }
    public bool Reversed { get; }
    public ElseStatement Else { get; }
    public bool OffsetIsContinue { get; }

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        // The `offset: continue` feature uses a per-source key to store the absolute index
        // of the next item to render. In Liquid, this state is updated by every `for` loop
        // over the same source, even if the loop itself doesn't specify `offset: continue`.
        var continueOffsetLiteral = _staticContinueOffsetLiteral;
        var rangeSourceLiteral = _staticRangeSourceLiteral;

        FluidValue evaluatedSource;

        if (_rangeSource is not null && rangeSourceLiteral is null)
        {
            // The name of a range with computed bounds is only known now. Evaluate the bounds once,
            // for both the name and the items, instead of once for each.
            var from = RangeExpression.ToBound(await _rangeSource.From.EvaluateAsync(context), context);
            var to = RangeExpression.ToBound(await _rangeSource.To.EvaluateAsync(context), context);

            evaluatedSource = RangeExpression.CreateRangeValue(from, to, context);
            rangeSourceLiteral = BuildRangeSourceLiteral(from, to);
            continueOffsetLiteral = BuildRangeContinueOffsetLiteral(rangeSourceLiteral);
        }
        else
        {
            evaluatedSource = await Source.EvaluateAsync(context);
        }

        // Golden Liquid: empty strings are treated as empty collections
        if (evaluatedSource.Type == FluidValues.String && string.IsNullOrEmpty(evaluatedSource.ToStringValue(context)))
        {
            if (Else != null)
            {
                await Else.WriteToAsync(output, encoder, context);
            }

            return Completion.Normal;
        }

        // Indexed values are read by index, so limit, offset and reversed only read the items they render.
        // Other values are enumerated into a list.
        var source = await ArrayValue.ToFluidArrayAsync(evaluatedSource, context);

        // Apply options
        var startIndex = 0;
        if (Offset is not null)
        {
            if (OffsetIsContinue)
            {
                startIndex = continueOffsetLiteral is null
                    ? 0
                    : (int)context.GetValue(continueOffsetLiteral).ToNumberValue(context);
            }
            else
            {
                // Like Liquid, a nil offset starts at the first item.
                startIndex = await EvaluateIntegerArgumentAsync("offset", Offset, context) ?? 0;
            }
        }

        // Liquid starts a negative offset at the first item, but still ends the slice at offset + limit.
        var itemsBeforeFirst = 0;
        if (startIndex < 0)
        {
            itemsBeforeFirst = -startIndex;
            startIndex = 0;
        }

        var count = Math.Max(0, source.Count - startIndex);

        // Like Liquid, a nil limit doesn't limit the items.
        if (Limit is not null && await EvaluateIntegerArgumentAsync("limit", Limit, context) is int limit)
        {
            // Like Liquid, the slice ends at offset + limit, so a negative limit renders nothing.
            count = Math.Min(count, Math.Max(0, limit - itemsBeforeFirst));
        }

        if (count == 0)
        {
            // Like Liquid, a loop without items still moves the continue cursor, to the offset as written.
            if (continueOffsetLiteral != null)
            {
                context.SetValue(continueOffsetLiteral, startIndex - itemsBeforeFirst);
            }

            if (Else != null)
            {
                await Else.WriteToAsync(output, encoder, context);
            }

            return Completion.Normal;
        }

        var parentLoop = context.LocalScope.GetValue("forloop");

        using var scope = context.EnterScope(ScopeBehavior.WriteThrough);

        {
            var endIndexExclusive = startIndex + count;

            var forloop = ForLoopValue.Create();
            forloop.Identifier = Identifier;
            forloop.Source = _continueSourceLiteral ?? rangeSourceLiteral;

            forloop.Length = count;

            context.LocalScope.SetOwnValue("forloop", forloop);

            // Render tag forloops should not be accessible as parentloop from nested for loops
            // (render creates an isolated scope where parent relationships don't cross boundaries)
            if (!parentLoop.IsNil() && parentLoop is ForLoopValue parentForLoop && !parentForLoop.IsRenderLoop)
            {
                forloop.ParentLoop = parentForLoop;

                // Legacy Liquid compatibility: expose the parent loop as `parentloop`
                context.LocalScope.SetOwnValue("parentloop", parentForLoop);
            }

            for (var iteration = 0; iteration < count; iteration++)
            {
                context.IncrementSteps();

                // When reversed, iterate the slice in reverse without mutating the underlying list.
                var itemIndex = Reversed ? endIndexExclusive - 1 - iteration : startIndex + iteration;
                var item = source[itemIndex];

                context.LocalScope.SetOwnValue(Identifier, item);

                // Set helper variables
                forloop.Index = iteration + 1;
                forloop.Index0 = iteration;
                forloop.RIndex = count - iteration;
                forloop.RIndex0 = count - iteration - 1;
                forloop.First = iteration == 0;
                forloop.Last = iteration == count - 1;

                var completion = Completion.Normal;

                {
                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];

                        if (_isBlank && statement is TextSpanStatement)
                        {
                            continue;
                        }

                        completion = await statement.WriteToAsync(output, encoder, context);

                        //// Restore the forloop property after every statement in case it replaced it,
                        //// for instance if it contains a nested for loop
                        //context.LocalScope.SetOwnValue("forloop", forloop);

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            break;
                        }
                    }
                }

                if (completion == Completion.Continue)
                {
                    // Go to next iteration
                    continue;
                }

                if (completion == Completion.Break)
                {
                    // Leave the loop
                    break;
                }
            }

            // Persist the continue cursor to the end of the intended slice, regardless of early break.
            // Like Liquid, it is the offset as written plus the items in the slice, so a negative
            // offset counts from where it was, not from the first item.
            if (continueOffsetLiteral != null)
            {
                context.SetValue(continueOffsetLiteral, endIndexExclusive - itemsBeforeFirst);
            }
        }

        return Completion.Normal;
    }

    private static string BuildRangeSourceLiteral(int from, int to)
    {
        return $"({from}..{to})";
    }

    /// <summary>
    /// Builds the key holding the `offset: continue` cursor for a range source.
    /// </summary>
    private string BuildRangeContinueOffsetLiteral(string rangeSourceLiteral)
    {
        // Key is based on Liquid's `forloop.name`: "{identifier}-{source}".
        // This means changing the loop variable changes the key.
        return $"for_continue_{Identifier}-{rangeSourceLiteral}";
    }

    private static bool TryGetLiteralBound(Expression expression, out int bound)
    {
        bound = 0;

        if (expression is not LiteralExpression { Value: NumberValue number })
        {
            return false;
        }

        // Left to the render when out of range, so that it fails there like any other range does.
        var value = number.Value;
        if (value <= int.MinValue || value >= int.MaxValue)
        {
            return false;
        }

        bound = Convert.ToInt32(value);
        return true;
    }

    private static async ValueTask<int?> EvaluateIntegerArgumentAsync(string name, Expression expression, TemplateContext context)
    {
        var value = await expression.EvaluateAsync(context);

        if (value.IsNil())
        {
            return null;
        }

        if (value.Type == FluidValues.Number)
        {
            return NumberConversions.ToInt32Saturated(value.ToNumberValue(context));
        }

        if (value.Type == FluidValues.String)
        {
            var s = value.ToStringValue(context).Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new LiquidException($"for: {name} is not a number");
        }

        throw new LiquidException($"for: {name} must be a number");
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitForStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var sourceExpr = context.GetExpressionMethodName(Source);
        var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier);
        var continueOffsetLiteralName = context.GetUniqueId("continueOffsetLiteral");
        string continueOffsetLit = null;

        // Nothing reads the forloop variable of most loops, which then don't create or update it.
        var usesForLoop = context.UsesForLoop(this);

        // For a range, the "(from..to)" part of forloop.name, which is only known once the bounds are evaluated.
        // It is only built when the loop has a forloop variable to name.
        string rangeSourceName = null;

        if (!string.IsNullOrEmpty(_continueSourceLiteral))
        {
            continueOffsetLit = SourceGenerationContext.ToCSharpStringLiteral($"for_continue_{Identifier}-{_continueSourceLiteral}");
            context.WriteLine($"var {continueOffsetLiteralName} = {continueOffsetLit};");
        }
        else if (Source is RangeExpression range)
        {
            var fromExpr = context.GetExpressionMethodName(range.From);
            var toExpr = context.GetExpressionMethodName(range.To);
            if (_staticRangeSourceLiteral != null)
            {
                // Literal bounds: the name is a constant of the generated code.
                rangeSourceName = SourceGenerationContext.ToCSharpStringLiteral(_staticRangeSourceLiteral);
                context.WriteLine($"var {continueOffsetLiteralName} = {SourceGenerationContext.ToCSharpStringLiteral(_staticContinueOffsetLiteral)};");
            }
            else
            {
                var rangeText = $"\"(\" + global::Fluid.Ast.RangeExpression.GetBound(await {fromExpr}({context.ContextName}), {context.ContextName}).ToString(CultureInfo.InvariantCulture) + \"..\" + global::Fluid.Ast.RangeExpression.GetBound(await {toExpr}({context.ContextName}), {context.ContextName}).ToString(CultureInfo.InvariantCulture) + \")\"";

                // Only build the name separately when the loop has a forloop variable to name.
                if (usesForLoop)
                {
                    rangeSourceName = context.GetUniqueId("rangeSource");
                    context.WriteLine($"var {rangeSourceName} = {rangeText};");
                    rangeText = rangeSourceName;
                }

                context.WriteLine($"var {continueOffsetLiteralName} = {SourceGenerationContext.ToCSharpStringLiteral($"for_continue_{Identifier}-")} + {rangeText};");
            }
        }
        else
        {
            continueOffsetLit = SourceGenerationContext.ToCSharpStringLiteral($"for_continue_{Identifier}-stmt_{RuntimeHelpers.GetHashCode(this).ToString(CultureInfo.InvariantCulture)}");
            context.WriteLine($"var {continueOffsetLiteralName} = {continueOffsetLit};");
        }

        context.WriteLine($"var evaluatedSource = await {sourceExpr}({context.ContextName});");
        context.WriteLine("if (evaluatedSource.Type == FluidValues.String && string.IsNullOrEmpty(evaluatedSource.ToStringValue(context)))");
        context.WriteLine("{");
        using (context.Indent())
        {
            if (Else != null)
            {
                var elseStmt = context.GetStatementMethodName(Else);
                context.WriteLine($"await {elseStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
            }
            context.WriteLine("return Completion.Normal;");
        }
        context.WriteLine("}");
        context.WriteLine();
        context.WriteLine($"var source = await ArrayValue.ToFluidArrayAsync(evaluatedSource, {context.ContextName});");
        context.WriteLine("var startIndex = 0;");
        if (Offset is not null)
        {
            if (OffsetIsContinue)
            {
                context.WriteLine($"startIndex = (int){context.ContextName}.GetValue({continueOffsetLiteralName}).ToNumberValue({context.ContextName});");
            }
            else
            {
                var offsetExpr = context.GetExpressionMethodName(Offset);
                context.WriteLine($"startIndex = (int)(await {offsetExpr}({context.ContextName})).ToNumberValue({context.ContextName});");
            }
        }

        context.WriteLine("var itemsBeforeFirst = 0;");
        context.WriteLine("if (startIndex < 0)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine("itemsBeforeFirst = -startIndex;");
            context.WriteLine("startIndex = 0;");
        }
        context.WriteLine("}");

        context.WriteLine("var count = Math.Max(0, source.Count - startIndex);");
        if (Limit is not null)
        {
            var limitExpr = context.GetExpressionMethodName(Limit);
            context.WriteLine($"var limitValue = await {limitExpr}({context.ContextName});");
            context.WriteLine("if (!limitValue.IsNil())");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"var limit = (int)limitValue.ToNumberValue({context.ContextName});");
                context.WriteLine("count = Math.Min(count, Math.Max(0, limit - itemsBeforeFirst));");
            }
            context.WriteLine("}");
        }

        context.WriteLine("if (count == 0)");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"{context.ContextName}.SetValue({continueOffsetLiteralName}, startIndex - itemsBeforeFirst);");
            if (Else != null)
            {
                var elseStmt = context.GetStatementMethodName(Else);
                context.WriteLine($"await {elseStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
            }
            context.WriteLine("return Completion.Normal;");
        }
        context.WriteLine("}");

        if (usesForLoop)
        {
            context.WriteLine($"var parentLoop = {context.ContextName}.LocalScope.GetValue(\"forloop\");");
        }

        context.WriteLine($"using var scope = {context.ContextName}.EnterScope(ScopeBehavior.WriteThrough);");
        context.WriteLine("try");
        context.WriteLine("{");
        using (context.Indent())
        {
            var sourceLiteral = "null";
            if (_continueSourceLiteral != null)
            {
                sourceLiteral = SourceGenerationContext.ToCSharpStringLiteral(_continueSourceLiteral);
            }
            else if (rangeSourceName != null)
            {
                sourceLiteral = rangeSourceName;
            }

            context.WriteLine("var endIndexExclusive = startIndex + count;");

            if (usesForLoop)
            {
                context.WriteLine("var forloop = ForLoopValue.Create();");
                context.WriteLine($"forloop.Identifier = {identifierLit};");
                context.WriteLine($"forloop.Source = {sourceLiteral};");
                context.WriteLine("forloop.Length = count;");
                context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue(\"forloop\", forloop);");
                context.WriteLine("if (!parentLoop.IsNil() && parentLoop is ForLoopValue parentForLoop && !parentForLoop.IsRenderLoop)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("forloop.ParentLoop = parentForLoop;");
                    context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue(\"parentloop\", parentForLoop);");
                }
                context.WriteLine("}");
            }

            context.WriteLine("for (var iteration = 0; iteration < count; iteration++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.IncrementSteps();");
                context.WriteLine($"var itemIndex = {(Reversed ? "endIndexExclusive - 1 - iteration" : "startIndex + iteration")};");
                context.WriteLine("var item = source[itemIndex];");
                context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue({identifierLit}, item);");

                if (usesForLoop)
                {
                    context.WriteLine("forloop.Index = iteration + 1;");
                    context.WriteLine("forloop.Index0 = iteration;");
                    context.WriteLine("forloop.RIndex = count - iteration;");
                    context.WriteLine("forloop.RIndex0 = count - iteration - 1;");
                    context.WriteLine("forloop.First = iteration == 0;");
                    context.WriteLine("forloop.Last = iteration == count - 1;");
                }

                // Like the interpreter, the whitespace of a blank loop isn't written.
                var statements = SourceGenerationContext.GetWrittenStatements(Statements, _isBlank);

                // Declared on first use, an unused variable is a warning in the generated code.
                var declared = false;

                for (var s = 0; s < statements.Count; s++)
                {
                    // Text always completes normally, it is written inline.
                    if (statements[s] is TextSpanStatement textSpan)
                    {
                        textSpan.WriteTextTo(context);
                        continue;
                    }

                    var stmtMethod = context.GetStatementMethodName(statements[s]);
                    context.WriteLine($"{(declared ? "" : "var ")}completion = await {stmtMethod}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                    declared = true;

                    // The statements are emitted inline in the loop body, so each one leaves or skips the iteration itself.
                    context.WriteLine("if (completion == Completion.Break) break;");
                    context.WriteLine("if (completion == Completion.Continue) continue;");
                }
            }
            context.WriteLine("}");
            context.WriteLine($"{context.ContextName}.SetValue({continueOffsetLiteralName}, endIndexExclusive - itemsBeforeFirst);");
        }
        context.WriteLine("}");
        context.WriteLine("finally");
        context.WriteLine("{");
        using (context.Indent())
        {
        }
        context.WriteLine("}");

        context.WriteLine("return Completion.Normal;");
    }
}
