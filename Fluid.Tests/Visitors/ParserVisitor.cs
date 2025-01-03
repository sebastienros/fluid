using Fluid.Ast;
using Fluid.Parser;
using System.Collections.Generic;

namespace Fluid.Tests.Visitors
{
    internal class ParserVisitor : AstVisitor
    {
        public string TagName { get; set; }
        public object Value { get; set; }
        public IReadOnlyList<Statement> Statements { get; set; }

        protected override Statement VisitParserTagStatement<T>(ParserTagStatement<T> parserTagStatement)
        {
            TagName = parserTagStatement.TagName;
            Value = parserTagStatement.Value;

            return parserTagStatement;
        }

        protected override Statement VisitParserBlockStatement<T>(ParserBlockStatement<T> parserBlockStatement)
        {
            TagName = parserBlockStatement.TagName;
            Value = parserBlockStatement.Value;
            Statements = parserBlockStatement.Statements;

            return parserBlockStatement;
        }

        protected override Statement VisitEmptyTagStatement(EmptyTagStatement emptyTagStatement)
        {
            TagName = emptyTagStatement.TagName;

            return emptyTagStatement;
        }

        protected override Statement VisitEmptyBlockStatement(EmptyBlockStatement emptyBlockStatement)
        {
            TagName = emptyBlockStatement.TagName;
            Statements = emptyBlockStatement.Statements;

            return emptyBlockStatement;
        }
    }
}
