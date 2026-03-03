using Fluid.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Fluid.Tests;

public class MemberAccessorGeneratorTests
{
    [Fact]
    public void ShouldGenerateMemberAccessRegistrationsFromRegisterInvocation()
    {
        var source = """
            using Fluid;

            public class Person
            {
                public string FirstName { get; set; } = "";
                public int Age;
            }

            public static class Startup
            {
                public static void Configure(TemplateOptions options)
                {
                    options.MemberAccessStrategy.Register<Person, object>((p, name) => p.FirstName);
                }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MemberAccessorGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        Assert.Single(runResult.GeneratedTrees);
        var generated = runResult.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("GeneratedMemberAccessRegistrations", generated);
        Assert.Contains("RegisterAll(global::Fluid.TemplateOptions options)", generated);
        Assert.Contains("Person_GeneratedMemberAccessor", generated);
        Assert.Contains("comparer.Equals(name, \"FirstName\")", generated);
        Assert.Contains("comparer.Equals(name, \"Age\")", generated);
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToDictionary(static reference => reference.Display!, static reference => reference, StringComparer.Ordinal);

        references.TryAdd(typeof(TemplateOptions).Assembly.Location, MetadataReference.CreateFromFile(typeof(TemplateOptions).Assembly.Location));
        references.TryAdd(typeof(MemberAccessorGenerator).Assembly.Location, MetadataReference.CreateFromFile(typeof(MemberAccessorGenerator).Assembly.Location));
        references.TryAdd(typeof(Enumerable).Assembly.Location, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        references.TryAdd(typeof(object).Assembly.Location, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        return references.Values;
    }
}
