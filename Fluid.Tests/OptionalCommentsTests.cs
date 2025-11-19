using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class OptionalCommentsTests
    {
        private readonly FluidParser _parser = new();

        [Fact]
        public void ShouldAllowCommentBetweenCaseAndWhen()
        {
            var result = _parser.TryParse(@"
                {%- case 'a' -%}
                {%- comment -%}Comment between case and when{%- endcomment -%}
                  {%- when 'a' -%}
                    Matched A
                  {%- when 'b' -%}
                    Matched B
                {%- endcase -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Matched A", output);
        }

        [Fact]
        public void ShouldAllowCommentBetweenWhenBlocks()
        {
            var result = _parser.TryParse(@"
                {%- case 'b' -%}
                  {%- when 'a' -%}
                    Matched A
                  {%- comment -%}Comment between when blocks{%- endcomment -%}
                  {%- when 'b' -%}
                    Matched B
                {%- endcase -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Matched B", output);
        }

        [Fact]
        public void ShouldAllowMultipleCommentsBetweenCaseAndWhen()
        {
            var result = _parser.TryParse(@"
                {%- case 'a' -%}
                {%- comment -%}First comment{%- endcomment -%}
                {%- comment -%}Second comment{%- endcomment -%}
                  {%- when 'a' -%}
                    Matched
                {%- endcase -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Matched", output);
        }

        [Fact]
        public void ShouldAllowCommentInIfBlockBody()
        {
            // Comments in the if block body are part of the content
            var result = _parser.TryParse(@"
                {%- if false -%}
                  First
                  {%- comment -%}Comment in if body{%- endcomment -%}
                {%- elsif true -%}
                  Second
                {%- endif -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Second", output);
        }

        [Fact]
        public void ShouldAllowCommentInElsifBlockBody()
        {
            // Comments in the elsif block body are part of the content
            var result = _parser.TryParse(@"
                {%- if false -%}
                  First
                {%- elsif false -%}
                  Second
                  {%- comment -%}Comment in elsif body{%- endcomment -%}
                {%- elsif true -%}
                  Third
                {%- endif -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Third", output);
        }

        [Fact]
        public void ShouldAllowCommentInElseBlockBody()
        {
            // Comments in the else block body are part of the content
            var result = _parser.TryParse(@"
                {%- if false -%}
                  First
                {%- elsif false -%}
                  Second
                {%- else -%}
                  {%- comment -%}Comment in else body{%- endcomment -%}
                  Third
                {%- endif -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Third", output);
        }

        [Fact]
        public void ShouldAllowCommentInUnlessBlockBody()
        {
            // Comments in the unless block body are part of the content
            var result = _parser.TryParse(@"
                {%- unless true -%}
                  First
                  {%- comment -%}Comment in unless body{%- endcomment -%}
                {%- else -%}
                  Second
                {%- endunless -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("Second", output);
        }

        [Fact]
        public void ShouldAllowCommentInForBlockBody()
        {
            // Comments in the for block body are part of the content
            var result = _parser.TryParse(@"
                {%- for item in empty_array -%}
                  {{ item }}
                  {%- comment -%}Comment in for body{%- endcomment -%}
                {%- else -%}
                  No items
                {%- endfor -%}
                ", out var template, out var errors);

            var context = new TemplateContext();
            context.SetValue("empty_array", new ArrayValue(new FluidValue[0]));

            Assert.True(result);
            Assert.Null(errors);

            var output = template.Render(context);
            Assert.Equal("No items", output);
        }

        [Fact]
        public void ShouldRejectNonCommentTagsBetweenCaseAndWhen()
        {
            var result = _parser.TryParse(@"
                {%- case 'a' -%}
                {%- assign x = 5 -%}
                  {%- when 'a' -%}
                    Matched
                {%- endcase -%}
                ", out var template, out var errors);

            Assert.False(result);
            Assert.NotNull(errors);
        }

        [Fact]
        public void ShouldAllowTagsInIfBlockBody()
        {
            // Tags between if and elsif are part of the if block body and should be allowed
            var result = _parser.TryParse(@"
                {%- if false -%}
                  First
                  {%- assign x = 5 -%}
                {%- elsif true -%}
                  Second {{ x }}
                {%- endif -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public void ShouldAllowTagsInUnlessBlockBody()
        {
            // Tags between unless and else are part of the unless block body and should be allowed
            var result = _parser.TryParse(@"
                {%- unless true -%}
                  First
                  {%- assign x = 5 -%}
                {%- else -%}
                  Second {{ x }}
                {%- endunless -%}
                ", out var template, out var errors);

            var context = new TemplateContext();

            Assert.True(result);
            Assert.Null(errors);
        }

        [Fact]
        public void ShouldAllowTagsInForBlockBody()
        {
            // Tags between for and else are part of the for block body and should be allowed
            var result = _parser.TryParse(@"
                {%- for item in empty_array -%}
                  {{ item }}
                  {%- assign x = 5 -%}
                {%- else -%}
                  No items {{ x }}
                {%- endfor -%}
                ", out var template, out var errors);

            var context = new TemplateContext();
            context.SetValue("empty_array", new ArrayValue(new FluidValue[0]));

            Assert.True(result);
            Assert.Null(errors);
        }
    }
}
