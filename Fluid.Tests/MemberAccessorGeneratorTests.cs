using Fluid.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fluid.Tests;

public class MemberAccessorGeneratorTests
{
    [Fact]
    public void ShouldGenerateProfileMethodImplementationFromFluidRegisterAttributes()
    {
        var source = """
            using Fluid;

            public class Person
            {
                public string FirstName { get; set; } = "";
                public int Age;
            }

            public static partial class FluidProfiles
            {
                [FluidRegister(typeof(Person))]
                public static partial void ApplyPublic(TemplateOptions options);
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("internal sealed class FluidRegisterAttribute", generated);
        Assert.Contains("public static partial void ApplyPublic(global::Fluid.TemplateOptions options)", generated);
        Assert.Contains("strategy.Register(typeof(global::Person), \"*\", new global::Fluid.SourceGenerated.Person_GeneratedMemberAccessor());", generated);
        Assert.Contains("comparer.Equals(name, \"FirstName\")", generated);
        Assert.Contains("comparer.Equals(name, \"Age\")", generated);
    }

    [Fact]
    public void ShouldGenerateIndependentProfilesForDifferentTemplateOptionsInstances()
    {
        var source = """
            using Fluid;

            public class PublicModel
            {
                public string Title { get; set; } = "";
            }

            public class AdminModel
            {
                public string Secret { get; set; } = "";
            }

            public static partial class FluidProfiles
            {
                [FluidRegister(typeof(PublicModel))]
                public static partial void ApplyPublic(TemplateOptions options);

                [FluidRegister(typeof(AdminModel))]
                public static partial void ApplyAdmin(TemplateOptions options);
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("public static partial void ApplyPublic(global::Fluid.TemplateOptions options)", generated);
        Assert.Contains("public static partial void ApplyAdmin(global::Fluid.TemplateOptions options)", generated);
        Assert.Contains("strategy.Register(typeof(global::PublicModel), \"*\", new global::Fluid.SourceGenerated.PublicModel_GeneratedMemberAccessor());", generated);
        Assert.Contains("strategy.Register(typeof(global::AdminModel), \"*\", new global::Fluid.SourceGenerated.AdminModel_GeneratedMemberAccessor());", generated);
    }

    [Fact]
    public void ShouldGenerateOptionsSubclassRegistrationFromFluidRegisterAttributes()
    {
        var source = """
            using Fluid;

            public class Person
            {
                public string FirstName { get; set; } = "";
            }

            public class Address
            {
                public string City { get; set; } = "";
            }

            [FluidRegister(typeof(Person))]
            [FluidRegister(typeof(Address))]
            public partial class PublicTemplateOptions : TemplateOptions
            {
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("public partial class PublicTemplateOptions : global::Fluid.ITemplateOptionsMemberAccessorRegistrar", generated);
        Assert.Contains("void global::Fluid.ITemplateOptionsMemberAccessorRegistrar.RegisterMemberAccessors(global::Fluid.TemplateOptions options)", generated);
        Assert.Contains("strategy.Register(typeof(global::Person), \"*\", new global::Fluid.SourceGenerated.Person_GeneratedMemberAccessor());", generated);
        Assert.Contains("strategy.Register(typeof(global::Address), \"*\", new global::Fluid.SourceGenerated.Address_GeneratedMemberAccessor());", generated);
    }

    private static string RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MemberAccessorGenerator();
        var driver = CSharpGeneratorDriver.Create(generator).RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        Assert.Equal(2, runResult.GeneratedTrees.Length);
        Assert.Empty(runResult.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error));

        var outputCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(static x => x.Severity == DiagnosticSeverity.Error));

        return string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(static x => x.GetText().ToString()));
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
