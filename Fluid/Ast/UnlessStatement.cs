using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class UnlessStatement : TagStatement, ISourceable
    {
        public UnlessStatement(
            Expression condition,
            IReadOnlyList<Statement> statements,
            ElseStatement elseStatement = null) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (!result)
            {
                for (var i = 0; i < Statements.Count; i++)
                {
                    var statement = Statements[i];
                    var completion = await statement.WriteToAsync(writer, encoder, context);

                    if (completion != Completion.Normal)
                    {
                        // Stop processing the block statements
                        // We return the completion to flow it to the outer loop
                        return completion;
                    }
                }

                return Completion.Normal;
            }
            else
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(writer, encoder, context);
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitUnlessStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var conditionExpr = context.GetExpressionMethodName(Condition);
            context.WriteLine($"var result = (await {conditionExpr}({context.ContextName})).ToBooleanValue();");
            context.WriteLine("if (!result)");
            context.WriteLine("{");
            using (context.Indent())
            {
                for (var i = 0; i < Statements.Count; i++)
                {
                    var stmtMethod = context.GetStatementMethodName(Statements[i]);
                    context.WriteLine($"var completion = await {stmtMethod}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                    context.WriteLine("if (completion != Completion.Normal) return completion;");
                }
                context.WriteLine("return Completion.Normal;");
            }
            context.WriteLine("}");

            if (Else != null)
            {
                var elseStmt = context.GetStatementMethodName(Else);
                context.WriteLine($"return await {elseStmt}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
            }
            else
            {
                context.WriteLine("return Completion.Normal;");
            }
        }
    }
}
