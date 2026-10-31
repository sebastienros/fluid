using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class ContinueStatement : Statement, ISourceable
{
    // Like Liquid, it isn't blank: a block containing it keeps its whitespace.
    public override bool IsWhitespaceOrCommentOnly => false;

    public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        return Continue();
    }

    public void WriteTo(SourceGenerationContext context)
    {
        context.WriteLine("return Completion.Continue;");
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitContinueStatement(this);
}
