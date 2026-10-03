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
                public System.Threading.Tasks.Task Loaded() => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.ValueTask Initialized() => System.Threading.Tasks.ValueTask.CompletedTask;
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
        Assert.Contains("global::Fluid.MemberAccessStrategyExtensions.Register(strategy, typeof(global::Person), \"*\", new global::Fluid.SourceGenerated.Person_GeneratedMemberAccessor());", generated);
        Assert.Contains("comparer.Equals(name, \"FirstName\")", generated);
        Assert.Contains("comparer.Equals(name, \"Age\")", generated);
        Assert.Contains("var task = typed.Loaded();", generated);
        Assert.Contains("await task.ConfigureAwait(false);", generated);
        Assert.Contains("var valueTask = typed.Initialized();", generated);
        Assert.Contains("await valueTask.ConfigureAwait(false);", generated);
        Assert.Contains("return null;", generated);
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
        Assert.Contains("global::Fluid.MemberAccessStrategyExtensions.Register(strategy, typeof(global::PublicModel), \"*\", new global::Fluid.SourceGenerated.PublicModel_GeneratedMemberAccessor());", generated);
        Assert.Contains("global::Fluid.MemberAccessStrategyExtensions.Register(strategy, typeof(global::AdminModel), \"*\", new global::Fluid.SourceGenerated.AdminModel_GeneratedMemberAccessor());", generated);
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
        Assert.Contains("throw new global::System.ArgumentNullException(nameof(options));", generated);
        Assert.Contains("global::Fluid.MemberAccessStrategyExtensions.Register(strategy, typeof(global::Person), \"*\", new global::Fluid.SourceGenerated.Person_GeneratedMemberAccessor());", generated);
        Assert.Contains("global::Fluid.MemberAccessStrategyExtensions.Register(strategy, typeof(global::Address), \"*\", new global::Fluid.SourceGenerated.Address_GeneratedMemberAccessor());", generated);
    }

    [Theory]
    [InlineData("new TemplateContext(person, options)")]
    [InlineData("new TemplateContext(person, options, false)")]
    [InlineData("new TemplateContext(options: options, model: person)")]
    [InlineData("new(person, options)")]
    public void ShouldInferModelWithCustomOptions(string construction)
    {
        var generated = RunGenerator($$"""
            using Fluid;
            public class Person
            {
                public string FirstName { get; set; } = "";
                public string Hidden { private get; set; } = "";
                public string Method() => "";
                public static string Static => "";
                public System.Threading.Tasks.Task PlainTask { get; set; } = System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.ValueTask<int> ValueTask { get; set; }
                public System.Threading.Tasks.Task<int> Count { get; set; } = System.Threading.Tasks.Task.FromResult(42);
            }
            public static class Factory
            {
                public static TemplateContext Create(Person person, TemplateOptions options) => {{construction}};
            }
            """);

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", generated);
        Assert.Contains("RegisterSourceGeneratedAccessor(typeof(global::Person)", generated);
        Assert.Contains("typed.FirstName", generated);
        Assert.Contains("var task = typed.Count;", generated);
        Assert.DoesNotContain("typed.Hidden", generated);
        Assert.DoesNotContain("typed.Method()", generated);
        Assert.DoesNotContain("global::Person.Static", generated);
        Assert.DoesNotContain("typed.PlainTask", generated);
        Assert.DoesNotContain("typed.ValueTask", generated);
    }

    [Theory]
    [InlineData("Person", "new TemplateContext(person)")]
    [InlineData("Person", "new TemplateContext(person, TemplateOptions.Default)")]
    [InlineData("object", "new TemplateContext(person, options)")]
    [InlineData("IPerson", "new TemplateContext(person, options)")]
    public void ShouldNotInferWithoutConcreteModelAndCustomOptions(string type, string construction)
    {
        var generated = RunGenerator($$"""
            using Fluid;
            public interface IPerson { string FirstName { get; } }
            public class Person : IPerson { public string FirstName => ""; }
            public static class Factory
            {
                public static TemplateContext Create({{type}} person, TemplateOptions options) => {{construction}};
            }
            """);

        Assert.DoesNotContain("RegisterSourceGeneratedAccessor", generated);
    }

    [Fact]
    public void ShouldIgnoreInaccessibleAndFileLocalModels()
    {
        var generated = RunGenerator("""
            using Fluid;
            file class FileModel { public string Value => ""; }
            public class Factory
            {
                private class PrivateModel { public string Value => ""; }
                public TemplateContext Create(TemplateOptions options) => new TemplateContext(new PrivateModel(), options);
            }
            public static class FileFactory
            {
                public static TemplateContext Create(TemplateOptions options) => new TemplateContext(new FileModel(), options);
            }
            """);

        Assert.DoesNotContain("RegisterSourceGeneratedAccessor", generated);
    }

    [Fact]
    public void ShouldGenerateIndependentExplicitAndInferredAccessors()
    {
        var generated = RunGenerator("""
            using Fluid;
            public class Person
            {
                public string FirstName => "";
                public string Method() => "";
                public System.Threading.Tasks.Task<string>? Loaded => null;
            }
            public static partial class Profile
            {
                [FluidRegister(typeof(Person))]
                public static partial void Apply(TemplateOptions options);
                public static TemplateContext Create(Person model, TemplateOptions options) => new(model, options);
            }
            """);

        Assert.Contains("Person_GeneratedMemberAccessor : global::Fluid.IAsyncMemberAccessor", generated);
        Assert.Contains("typed.Method()", generated);
        Assert.Contains("Person_GeneratedMemberAccessor_Inferred0 : global::Fluid.IMemberAccessor", generated);
        Assert.Contains("await task!.ConfigureAwait(false);", generated);
    }

    [Fact]
    public void ShouldGenerateAccessibleInheritedMembersWithNameCollisions()
    {
        var generated = RunGenerator("""
            using Fluid;
            public class Base
            {
                public System.Threading.Tasks.Task<string> Value => System.Threading.Tasks.Task.FromResult("");
                public string Hidden => "";
                public string Inherited => "";
            }
            public class Person : Base
            {
                public new string Value = "";
                private new string Hidden => "";
            }
            public static class Factory
            {
                public static TemplateContext Create(Person model, TemplateOptions options) => new(model, options);
            }
            """);

        Assert.Contains("return typed.Value;", generated);
        Assert.Contains("return ((global::Base)typed).Inherited;", generated);
        Assert.DoesNotContain(".Hidden", generated);
        Assert.DoesNotContain("var task =", generated);
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

        Assert.InRange(runResult.GeneratedTrees.Length, 1, 2);
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
            .GroupBy(static reference => reference.Display!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        references.TryAdd(typeof(TemplateOptions).Assembly.Location, MetadataReference.CreateFromFile(typeof(TemplateOptions).Assembly.Location));
        references.TryAdd(typeof(MemberAccessorGenerator).Assembly.Location, MetadataReference.CreateFromFile(typeof(MemberAccessorGenerator).Assembly.Location));
        references.TryAdd(typeof(Enumerable).Assembly.Location, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        references.TryAdd(typeof(object).Assembly.Location, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        return references.Values;
    }
}
