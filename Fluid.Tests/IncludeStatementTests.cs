using Fluid.Ast;
using Fluid.Parser;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Fluid.Tests
{
    public class IncludeStatementTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task IncludeStatement_ShouldThrowFileNotFoundException_IfTheFileProviderIsNotPresent()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            try
            {
                await new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());
                Assert.True(false);
            }
            catch (FileNotFoundException)
            {
                return;
            }

            Assert.True(false);
        }

        [Fact]
        public async Task IncludeStatement_ShouldLoadPartial_IfThePartialsFolderExist()
        {

            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: ''
shape: ''";

            await new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeStatement_ShouldLoadCorrectTemplate_IfTheMemberExpressionValueChanges()
        {
            var expression = new MemberExpression(new IdentifierSegment("Firstname"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_First.liquid", @"{{ 'Partial Content One' }}
Partials_One: '{{ Partials }}'
color_One: '{{ color }}'
shape_One: '{{ shape }}'");

            fileProvider.Add("_Second.liquid", @"{{ 'Partial Content Two' }}
Partials_Two: '{{ Partials }}'
color_Two: '{{ color }}'
shape_Two: '{{ shape }}'");

            var model = new Domain.Person { Firstname = "_First.liquid" };

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(model, options);
            var expectedResultFirstCall = @"Partial Content One
Partials_One: ''
color_One: ''
shape_One: ''";

            var expectedResultSecondCall = @"Partial Content Two
Partials_Two: ''
color_Two: ''
shape_Two: ''";

            var include = new IncludeStatement(_parser, expression);

            await include.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResultFirstCall, sw.ToString());

            model.Firstname = "_Second.liquid";
            sw = new StringWriter();

            await include.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResultSecondCall, sw.ToString());
        }

        [Fact]
        public async Task IncludeStatement_WithInlinevariableAssignment_ShouldBeEvaluated()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var assignStatements = new List<AssignStatement>
            {
                new AssignStatement("color", new LiteralExpression(new StringValue("blue"))),
                new AssignStatement("shape", new LiteralExpression(new StringValue("circle")))
            };
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: 'circle'";

            await new IncludeStatement(_parser, expression, assignStatements: assignStatements).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeStatement_WithTagParams_ShouldBeEvaluated()
        {
            var pathExpression = new LiteralExpression(new StringValue("color"));
            var withExpression = new LiteralExpression(new StringValue("blue"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("color.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: ''";

            await new IncludeStatement(_parser, pathExpression, with: withExpression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeStatement_ShouldLimitRecursion()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }} {% include '_Partial' %}");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, context).AsTask());
        }

        [Fact]
        public void IncludeTag_With()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            _parser.TryParse("{% include 'product' with products[0] %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm ", result);
        }

        [Fact]
        public void IncludeTag_With_Alias()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product_alias.liquid", "Product: {{ product.title }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            _parser.TryParse("{% include 'product_alias' with products[0] as product %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm ", result);
        }

        [Fact]
        public void RenderTag_With_Alias()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product_alias.liquid", "Product: {{ product.title }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            _parser.TryParse("{% render 'product_alias' with products[0] as product %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm ", result);
        }

        [Fact]
        public void IncludeTag_With_Default_Name()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("product", new { title = "Draft 151cm" });
            _parser.TryParse("{% include 'product' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm ", result);
        }

        [Fact]
        public void RenderTag_With_Default_Name()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("product", new { title = "Draft 151cm" });
            _parser.TryParse("{% render 'product' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm ", result);
        }

        [Fact]
        public void Increment_Is_Isolated_Between_Renders()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("incr.liquid", "{% increment %}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            _parser.TryParse("{% increment %}{% increment %}{% render 'incr' %}", out var template, out var error);
            Assert.Null(error);
            var result = template.Render(context);

            Assert.Equal("010", result);
        }

        [Fact]
        public void RenderTagCantUseDynamicName()
        {
            var fileProvider = new MockFileProvider();
            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            var result = _parser.TryParse("{% assign name = 'snippet' %}{% render name %}", out var template, out var error);
            Assert.False(result);
            Assert.Contains(ErrorMessages.ExpectedStringRender, error);
        }

        [Fact]
        public void IncludeTag_For_Loop()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }} {% if forloop.first %}first{% endif %} {% if forloop.last %}last{% endif %} index:{{ forloop.index }} rindex:{{ forloop.rindex }} rindex0:{{ forloop.rindex0 }} " );

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            _parser.TryParse("{% include 'product' for products %}", out var template);

            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm first  index:1 rindex:2 rindex0:1 Product: Element 155cm  last index:2 rindex:1 rindex0:0 ", result);
        }

        [Fact]
        public void RenderTag_For_Loop()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }} {% if forloop.first %}first{% endif %} {% if forloop.last %}last{% endif %} index:{{ forloop.index }} rindex:{{ forloop.rindex }} rindex0:{{ forloop.rindex0 }} " );

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            _parser.TryParse("{% render 'product' for products %}", out var template);

            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm first  index:1 rindex:2 rindex0:1 Product: Element 155cm  last index:2 rindex:1 rindex0:0 ", result);
        }

        [Fact]
        public void RenderTag_Does_Not_Inherit_Parent_Scope_Variables()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("snippet.liquid", "{{ outer_variable }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("product", new { title = "Draft 151cm" });
            _parser.TryParse("{% assign outer_variable = 'should not be visible' %}{% render 'snippet' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("", result);
        }

        [Fact]
        public void IncludeTag_Does_Inherit_Parent_Scope_Variables()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("snippet.liquid", "{{ outer_variable }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("product", new { title = "Draft 151cm" });
            _parser.TryParse("{% assign outer_variable = 'should be visible' %}{% include 'snippet' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("should be visible", result);
        }

        [Fact]
        public void RenderTag_Inherits_Global_Scope_Variables()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("snippet.liquid", "{{ global_variable }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            options.Scope.SetValue("global_variable", new StringValue("global value"));
            context.SetValue("product", new { title = "Draft 151cm" });
            _parser.TryParse("{% render 'snippet' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("global value", result);
        }

        [Fact]
        public async Task IncludeTag_Cache_Is_ThreadSafe()
        {
            var templates = "abcdefg".Select(x => new string(x, 10)).ToArray();

            var fileProvider = new MockFileProvider();

            foreach (var t in templates)
            {
                fileProvider.Add($"{t[0]}.liquid", t);
            }

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            _parser.TryParse("{%- include file -%}", out var template);

            var stopped = false;

            var tasks = templates.Select(x => Task.Run(() =>
            {
                while (!stopped)
                {
                    var context = new TemplateContext(options);
                    context.SetValue("file", x[0]);
                    var result = template.Render(context);

                    Assert.Equal(x, result);
                }
            })).ToArray();

            await Task.Delay(1000);

            stopped = true;
            Task.WaitAll(tasks);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IncludeTag_Caches_Template(bool useExtension)
        {
            // Ensure the cache works even when the file extension is not set
            var fileProvider = new MockFileProvider();
            fileProvider.Add("a.liquid", "AAAA");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            IFluidTemplate template = null;

            if (useExtension)
            {
                _parser.TryParse("""
                {%- include 'a.liquid' -%}
                """, out template);
            }
            else
            {
                _parser.TryParse("""
                {%- include 'a' -%}
                """, out template);
            }

            var result = template.Render(context);

            Assert.Equal("AAAA", result);

            // Update the content of the file
            fileProvider.Add("a.liquid", "BBBB");
            result = template.Render(context);

            // The previously cached template should be used
            Assert.Equal("AAAA", result);
        }

        [Fact]
        public void IncludeTag_Caches_ParsedTemplate()
        {
            var templates = new Dictionary<string, string>
            {
                ["a.liquid"] = "content1",
                ["folder/a.liquid"] = "content2",
                ["folder/b.liquid"] = "content3",
                ["folder/c.liquid"] = "content4",
                ["folder/other/d.liquid"] = "content5",
                ["b.liquid"] = "content6",
                ["c.liquid"] = "content7",
                ["d.liquid"] = "content8",
            };

            var tempPath = Path.Combine(Path.GetTempPath(), "FluidTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            var fileProvider = new PhysicalFileProvider(tempPath);

            WriteFilesContent(templates, tempPath);

            var fileInfos = templates.ToDictionary(t => t.Key, t => fileProvider.GetFileInfo(t.Key));

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            _parser.TryParse("{%- include file -%}", out var template);

            // The first time a template is included it will be read from the file provider
            foreach (var t in templates)
            {
                var f = fileProvider.GetFileInfo(t.Key);

                var context = new TemplateContext(options);
                context.SetValue("file", t.Key);
                var result = template.Render(context);

                Assert.Equal(t.Value, result);

                Assert.True(options.TemplateCache.TryGetTemplate(t.Key, f.LastModified, out var cachedTemplate));
            }

            // The next time a template is included it should not be accessed from the file provider but cached instead
            foreach (var t in templates)
            {
                var f = fileProvider.GetFileInfo(t.Key);

                options.TemplateCache.SetTemplate(t.Key, f.LastModified, new MockFluidTemplate(t.Key));

                var context = new TemplateContext(options);
                context.SetValue("file", t.Key);
                var result = template.Render(context);

                Assert.Equal(t.Key, result);
            }

            var now = DateTimeOffset.UtcNow;

            Thread.Sleep(500);

            // Update the files so they are accessed again
            WriteFilesContent(templates, tempPath);

            Thread.Sleep(1000); // Wait for the file provider to update the last modified date

            // Assert that all files have their last modified date updated
            foreach (var t in templates)
            {
                var f = fileProvider.GetFileInfo(t.Key);

                Assert.True(f.Exists);
                Assert.True(f.LastModified > now, $"File {t.Key} was not updated.");
            }

            // If the attributes have changed then the template should be reloaded
            foreach (var t in templates)
            {
                var f = fileProvider.GetFileInfo(t.Key);

                var context = new TemplateContext(options);
                context.SetValue("file", t.Key);
                var result = template.Render(context);

                Assert.Equal(t.Value, result);
            }

            static void WriteFilesContent(Dictionary<string, string> templates, string tempPath)
            {
                foreach (var t in templates)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tempPath, t.Key)));
                    File.WriteAllText(Path.Combine(tempPath, t.Key), t.Value);
                }
            }
        }

        [Fact]
        public void IncludeTag_Caches_DifferentFolders()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "FluidTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            Directory.CreateDirectory(tempPath + "/this-folder");
            Directory.CreateDirectory(tempPath + "/this-folder/that-folder");

            var fileProvider = new PhysicalFileProvider(tempPath);

            File.WriteAllText(tempPath + "/this-folder/this_file.liquid", "content1");
            File.WriteAllText(tempPath + "/this-folder/that-folder/this_file.liquid", "content2");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            _parser.TryParse("{%- include file -%}", out var template);

            var context = new TemplateContext(options);
            context.SetValue("file", "this-folder/this_file.liquid");

            Assert.Equal("content1", template.Render(context));

            context.SetValue("file", "this-folder/that-folder/this_file.liquid");

            Assert.Equal("content2", template.Render(context));

            try
            {
                Directory.Delete(tempPath, true);
            }
            catch
            {
                // Ignore any exceptions
            }
        }

        [Fact]
        public void IncludeTag_Caches_HandleFileSystemCasing()
        {
            // We can't rely on the OS to detect if the FS is case sensitive or not. c.f. MacOS
            string file = Path.GetTempPath() + Guid.NewGuid().ToString().ToLower();
            File.CreateText(file).Close();
            bool isCaseInsensitiveFilesystem = File.Exists(file.ToUpper());
            File.Delete(file);

            var tempPath = Path.Combine(Path.GetTempPath(), "FluidTests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            var fileProvider = new PhysicalFileProvider(tempPath);

            File.WriteAllText(tempPath + "/this_file.liquid", "content1");
            File.WriteAllText(tempPath + "/This_file.liquid", "content2");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            _parser.TryParse("{%- include file -%}", out var template);

            var context = new TemplateContext(options);

            if (isCaseInsensitiveFilesystem)
            {
                // Windows is case insensitive, there should be only one file
                context.SetValue("file", "this_file.liquid");
                Assert.Equal("content2", template.Render(context));
                context.SetValue("file", "THIS_FILE.liquid");
                Assert.Equal("content2", template.Render(context));
            }
            else
            {
                // Linux is case sensitive, this should be a new cache entry
                context.SetValue("file", "this_file.liquid");
                Assert.Equal("content1", template.Render(context));
                context.SetValue("file", "This_file.liquid");
                Assert.Equal("content2", template.Render(context));
            }

            try
                {
                    Directory.Delete(tempPath, true);
                }
                catch
                {
                    // Ignore any exceptions
                }
        }

        [Fact]
        public void RenderTag_With_And_NamedArguments()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("icon.liquid", "Icon: {{ icon }}, Class: {{ class }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            _parser.TryParse("{%- render 'icon' with 'rating-star', class: 'rating__star' -%}", out var template);
            var result = template.Render(context);

            Assert.Equal("Icon: rating-star, Class: rating__star", result);
        }

        [Fact]
        public void RenderTag_With_As_And_NamedArguments()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ p.title }}, Price: {{ price }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("my_product", new { title = "Draft 151cm" });
            _parser.TryParse("{% render 'product' with my_product as p, price: '$99' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm, Price: $99", result);
        }

        [Fact]
        public void RenderTag_With_MultipleNamedArguments()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("button.liquid", "Text: {{ button }}, Size: {{ size }}, Color: {{ color }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            _parser.TryParse("{% render 'button' with 'Click Me', size: 'large', color: 'blue' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Text: Click Me, Size: large, Color: blue", result);
        }

        [Fact]
        public void RenderTag_For_And_NamedArguments()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("product.liquid", "Product: {{ product.title }}, Tag: {{ tag }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("products", new[] { new { title = "Draft 151cm" }, new { title = "Element 155cm" } });
            
            var parseResult = _parser.TryParse("{% render 'product' for products, tag: 'sale' %}", out var template, out var error);
            Assert.True(parseResult, $"Parse failed: {error}");
            
            // Check the parsed statement
            var statements = (template as Fluid.Parser.FluidTemplate).Statements;
            var renderStmt = statements.FirstOrDefault() as RenderStatement;
            Assert.NotNull(renderStmt);
            Assert.NotNull(renderStmt.For);
            Assert.Single(renderStmt.AssignStatements);
            Assert.Equal("tag", renderStmt.AssignStatements[0].Identifier);
            
            // Check that the For expression evaluates correctly
            Assert.IsType<MemberExpression>(renderStmt.For);
            var forValue = renderStmt.For.EvaluateAsync(context).GetAwaiter().GetResult();
            var items = forValue.Enumerate(context).ToList();
            Assert.Equal(2, items.Count);  // Should have 2 items
            
            // Also check that For is really the "products" variable
            var memberExpr = renderStmt.For as MemberExpression;
            Assert.Single(memberExpr.Segments);
            Assert.IsType<IdentifierSegment>(memberExpr.Segments[0]);
            Assert.Equal("products", ((IdentifierSegment)memberExpr.Segments[0]).Identifier);
            
            var result = template.Render(context);

            Assert.Equal("Product: Draft 151cm, Tag: sale Product: Element 155cm, Tag: sale ", result);
        }

        [Fact]
        public void RenderTag_For_As_And_NamedArguments()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("item.liquid", "Item: {{ i.name }}, Status: {{ status }} ");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            context.SetValue("items", new[] { new { name = "First" }, new { name = "Second" } });
            _parser.TryParse("{% render 'item' for items as i, status: 'active' %}", out var template);
            var result = template.Render(context);

            Assert.Equal("Item: First, Status: active Item: Second, Status: active ", result);
        }

        [Fact]
        public void RenderTag_NamedArguments_DoNotLeakToParentScope()
        {
            var fileProvider = new MockFileProvider();
            fileProvider.Add("snippet.liquid", "{{ class }}");

            var options = new TemplateOptions() { FileProvider = fileProvider, MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var context = new TemplateContext(options);
            _parser.TryParse("{% render 'snippet', class: 'test' %}{{ class }}", out var template);
            var result = template.Render(context);

            Assert.Equal("test", result);
        }
    }
}
