using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.Tests.Domain;
using Irony.Parsing;
using Xunit;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid.Tests
{
    public class CustomGrammarTests
    {

        [Fact]
        public void CanAddCustomTag()
        {
            var success = YoloTemplate.TryParse("{% yolo a (1..3) %}{{ a }}{% oloy %}", out var template);
            Assert.True(success);
            
            var result = template.Render();

            Assert.Equal("123", result);
        }
    }

    public class YoloGrammar : FluidGrammar
    {
        public YoloGrammar() : base()
        {
            var Yolo = new NonTerminal("yolo");
            var EndYolo = ToTerm("oloy");

            Yolo.Rule = "yolo" + Identifier + Range;
            KnownTags.Rule |= Yolo | EndYolo;

            // Prevent the text from being added in the parsed tree.
            // Only Identifier and Range will be in the tree.
            MarkPunctuation("yolo");
        }
    }

    public class YoloTemplate : FluidTemplate<ActivatorFluidParserFactory<YoloParser>> { }

    public class YoloParser : IronyFluidParser<YoloGrammar> 
    {
        protected override Statement BuildTagStatement(ParseTreeNode node)
        {
            var tag = node.ChildNodes[0];

            switch (tag.Term.Name)
            {
                case "yolo":
                    EnterBlock(tag);
                    return null;

                case "oloy":
                    return BuildYoloStatement();

                default:
                    return base.BuildTagStatement(node);
            }
        }

        private Statement BuildYoloStatement()
        {
            var identifier = _currentContext.Tag.ChildNodes[0].Token.Text;
            var source = _currentContext.Tag.ChildNodes[1];

            ForStatement yoloStatement = new ForStatement(
                _currentContext.Statements, 
                identifier, 
                BuildRangeExpression(source),
                null,
                null,
                false);

            ExitBlock();

            return yoloStatement;
        }
    }
}
