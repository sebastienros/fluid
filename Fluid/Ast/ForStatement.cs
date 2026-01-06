using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class ForStatement : TagStatement, ISourceable
    {
        private readonly string _continueOffsetLiteral;

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

            OffsetIsContinue = Offset is MemberExpression l && l.Segments.Count == 1 && ((IdentifierSegment)l.Segments[0]).Identifier == "continue";
            _continueOffsetLiteral = source is MemberExpression m ? "for_continue_" + ((IdentifierSegment)m.Segments[0]).Identifier : null;
        }

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
            var evaluatedSource = await Source.EvaluateAsync(context);

            // Fast-path: FluidValue.Create(IEnumerable) and many array-like values already materialize as ArrayValue.
            // Avoid re-enumerating and allocating a new List<T> in this very hot path.
            IReadOnlyList<FluidValue> source = evaluatedSource is ArrayValue array
                ? array.Values
                : await evaluatedSource.EnumerateAsync(context).ToListAsync();

            if (source.Count == 0)
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }

                return Completion.Normal;
            }

            // Apply options
            var startIndex = 0;
            if (Offset is not null)
            {
                if (OffsetIsContinue)
                {
                    startIndex = (int)context.GetValue(_continueOffsetLiteral).ToNumberValue();
                }
                else
                {
                    var offset = (int)(await Offset.EvaluateAsync(context)).ToNumberValue();
                    startIndex = offset;
                }
            }

            var count = Math.Max(0, source.Count - startIndex);

            if (Limit is not null)
            {
                var limit = (int)(await Limit.EvaluateAsync(context)).ToNumberValue();

                // Limit can be negative
                if (limit >= 0)
                {
                    count = Math.Min(count, limit);
                }
                else
                {
                    count = Math.Max(0, count + limit);
                }
            }

            if (count == 0)
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }

                return Completion.Normal;
            }

            var parentLoop = context.LocalScope.GetValue("forloop");

            context.EnterForLoopScope();

            try
            {
                var forloop = new ForLoopValue();

                var length = forloop.Length = startIndex + count;

                context.LocalScope.SetOwnValue("forloop", forloop);

                if (!parentLoop.IsNil())
                {
                    context.LocalScope.SetOwnValue("parentloop", parentLoop);
                }

                for (var i = startIndex; i < length; i++)
                {
                    context.IncrementSteps();

                    // When reversed, iterate the slice in reverse without mutating the underlying list.
                    var itemIndex = Reversed ? startIndex + length - 1 - i : i;
                    var item = source[itemIndex];

                    context.LocalScope.SetOwnValue(Identifier, item);

                    // Set helper variables
                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = length - i;
                    forloop.RIndex0 = length - i - 1;
                    forloop.First = i == 0;
                    forloop.Last = i == length - 1;

                    if (_continueOffsetLiteral != null)
                    {
                        context.SetValue(_continueOffsetLiteral, forloop.Index);
                    }

                    var completion = Completion.Normal;

                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];
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
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitForStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var sourceExpr = context.GetExpressionMethodName(Source);
            var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier);
            var continueOffsetLit = _continueOffsetLiteral == null ? null : SourceGenerationContext.ToCSharpStringLiteral(_continueOffsetLiteral);

            context.WriteLine($"var source = await (await {sourceExpr}({context.ContextName})).EnumerateAsync({context.ContextName}).ToListAsync();");
            context.WriteLine("if (source.Count == 0)");
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

            context.WriteLine("var startIndex = 0;");
            if (Offset is not null)
            {
                if (OffsetIsContinue && continueOffsetLit is not null)
                {
                    context.WriteLine($"startIndex = (int){context.ContextName}.GetValue({continueOffsetLit}).ToNumberValue();");
                }
                else
                {
                    var offsetExpr = context.GetExpressionMethodName(Offset);
                    context.WriteLine($"startIndex = (int)(await {offsetExpr}({context.ContextName})).ToNumberValue();");
                }
            }

            context.WriteLine("var count = Math.Max(0, source.Count - startIndex);");
            if (Limit is not null)
            {
                var limitExpr = context.GetExpressionMethodName(Limit);
                context.WriteLine($"var limit = (int)(await {limitExpr}({context.ContextName})).ToNumberValue();");
                context.WriteLine("if (limit >= 0)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("count = Math.Min(count, limit);");
                }
                context.WriteLine("}");
                context.WriteLine("else");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("count = Math.Max(0, count + limit);");
                }
                context.WriteLine("}");
            }

            context.WriteLine("if (count == 0)");
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

            if (Reversed)
            {
                context.WriteLine("source.Reverse(startIndex, count);");
            }

            context.WriteLine($"var parentLoop = {context.ContextName}.LocalScope.GetValue(\"forloop\");");
            context.WriteLine($"{context.ContextName}.EnterForLoopScope();");
            context.WriteLine("try");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("var forloop = new ForLoopValue();");
                context.WriteLine("var length = forloop.Length = startIndex + count;");
                context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue(\"forloop\", forloop);");
                context.WriteLine("if (!parentLoop.IsNil())");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue(\"parentloop\", parentLoop);");
                }
                context.WriteLine("}");

                context.WriteLine("for (var i = startIndex; i < length; i++)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{context.ContextName}.IncrementSteps();");
                    context.WriteLine("var item = source[i];");
                    context.WriteLine($"{context.ContextName}.LocalScope.SetOwnValue({identifierLit}, item);");
                    context.WriteLine("// Set helper variables");
                    context.WriteLine("forloop.Index = i + 1;");
                    context.WriteLine("forloop.Index0 = i;");
                    context.WriteLine("forloop.RIndex = length - i;");
                    context.WriteLine("forloop.RIndex0 = length - i - 1;");
                    context.WriteLine("forloop.First = i == 0;");
                    context.WriteLine("forloop.Last = i == length - 1;");

                    if (continueOffsetLit is not null)
                    {
                        context.WriteLine($"{context.ContextName}.SetValue({continueOffsetLit}, forloop.Index);");
                    }

                    context.WriteLine("var completion = Completion.Normal;");
                    for (var s = 0; s < Statements.Count; s++)
                    {
                        var stmtMethod = context.GetStatementMethodName(Statements[s]);
                        context.WriteLine($"completion = await {stmtMethod}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                        context.WriteLine("if (completion != Completion.Normal) break;");
                    }

                    context.WriteLine("if (completion == Completion.Continue) continue;");
                    context.WriteLine("if (completion == Completion.Break) break;");
                }
                context.WriteLine("}");
            }
            context.WriteLine("}");
            context.WriteLine("finally");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.ReleaseScope();");
            }
            context.WriteLine("}");

            context.WriteLine("return Completion.Normal;");
        }
    }
}