using Fluid;
using System.Diagnostics;
using System.Text;

var source = @"
Hello World!!! 
    {%- for person in (1..3) %}
        a {{ 1 }}
    {%- endfor%}
";
//var source = "Hello World!!!";

var parser = new FluidParser();
var compiledTemplate = parser.Compile(source);
var template = parser.Parse(source);

var sw = Stopwatch.StartNew();

var sb = new StringBuilder(1024);
var writer = new StringWriter(sb);

await template.RenderAsync(Console.Out, NullEncoder.Default, new TemplateContext());
await compiledTemplate.RenderAsync(Console.Out, NullEncoder.Default, new TemplateContext());

// Warmup
for (var k = 1; k < 1000; k++)
{
    sb.Clear();
    await template.RenderAsync(writer, NullEncoder.Default, new TemplateContext());
    await compiledTemplate.RenderAsync(writer, NullEncoder.Default, new TemplateContext());
}

sw.Restart();
for (var i = 0; i < 1000000; i++)
{
    sb.Clear();
    await template.RenderAsync(writer, NullEncoder.Default, new TemplateContext());
    writer.Flush();
}

Console.WriteLine();
Console.WriteLine($"Interpreted: {sw.ElapsedMilliseconds} ms");

sw.Restart();
for (var i = 0; i < 1000000; i++)
{
    sb.Clear();
    await compiledTemplate.RenderAsync(writer, NullEncoder.Default, new TemplateContext());
    writer.Flush();
}

Console.WriteLine();
Console.WriteLine($"Compiled: {sw.ElapsedMilliseconds} ms");
