using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluid.SourceGenerator;

[Generator]
public sealed class MemberAccessorGenerator : IIncrementalGenerator
{
    private const string RegisterAttributeType = "Fluid.FluidRegisterAttribute";
    private const string TemplateOptionsType = "Fluid.TemplateOptions";

    private static readonly SymbolDisplayFormat TypeExpressionFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly DiagnosticDescriptor InvalidProfileMethod = new(
        id: "FLUIDSG001",
        title: "Invalid Fluid registration profile method",
        messageFormat: "Method '{0}' must be a static partial method declaration with explicit accessibility and signature '(TemplateOptions options)' inside a non-generic, non-nested partial class",
        category: "Fluid.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidOptionsType = new(
        id: "FLUIDSG002",
        title: "Invalid Fluid template options type",
        messageFormat: "Type '{0}' must be a partial, non-generic, non-nested class deriving from TemplateOptions",
        category: "Fluid.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
        {
            output.AddSource("FluidRegisterAttribute.g.cs", AttributeSource);
        });

        var profileMethods = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0,
                transform: static (syntaxContext, _) => GetProfileMethod(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        var optionsTypes = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax type && type.AttributeLists.Count > 0,
                transform: static (syntaxContext, _) => GetOptionsType(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        var combined = context.CompilationProvider.Combine(profileMethods.Collect()).Combine(optionsTypes.Collect());
        context.RegisterSourceOutput(combined, static (sourceContext, source) => Execute(sourceContext, source.Left.Right, source.Right));
    }

    private static ProfileMethodCandidate? GetProfileMethod(GeneratorSyntaxContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var registeredTypes = GetRegisteredTypes(methodSymbol);
        if (registeredTypes.IsDefaultOrEmpty)
        {
            return null;
        }

        var hasPartialModifier = methodSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));
        var hasExplicitAccessibility = methodSyntax.Modifiers.Any(static m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.PrivateKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword));
        var hasBody = methodSyntax.Body is not null || methodSyntax.ExpressionBody is not null;

        return new ProfileMethodCandidate(
            methodSymbol,
            registeredTypes.ToImmutableArray(),
            hasPartialModifier,
            hasExplicitAccessibility,
            hasBody,
            methodSyntax.GetLocation());
    }

    private static OptionsTypeCandidate? GetOptionsType(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax typeSyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeSyntax) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var registeredTypes = GetRegisteredTypes(typeSymbol);
        if (registeredTypes.IsDefaultOrEmpty)
        {
            return null;
        }

        var hasPartialModifier = typeSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));

        return new OptionsTypeCandidate(
            typeSymbol,
            registeredTypes,
            hasPartialModifier,
            typeSyntax.GetLocation());
    }

    private static ImmutableArray<ITypeSymbol> GetRegisteredTypes(ISymbol symbol)
    {
        var registerAttributes = symbol.GetAttributes()
            .Where(static x => string.Equals(x.AttributeClass?.ToDisplayString(), RegisterAttributeType, StringComparison.Ordinal))
            .ToImmutableArray();

        if (registerAttributes.IsDefaultOrEmpty)
        {
            return [];
        }

        var registeredTypes = new List<ITypeSymbol>(registerAttributes.Length);

        foreach (var attribute in registerAttributes)
        {
            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not ITypeSymbol registeredType)
            {
                continue;
            }

            if (registeredType is IErrorTypeSymbol || registeredType.TypeKind == TypeKind.TypeParameter)
            {
                continue;
            }

            registeredTypes.Add(registeredType);
        }

        return registeredTypes.ToImmutableArray();
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ProfileMethodCandidate> methodCandidates, ImmutableArray<OptionsTypeCandidate> optionsTypeCandidates)
    {
        if (methodCandidates.IsDefaultOrEmpty && optionsTypeCandidates.IsDefaultOrEmpty)
        {
            return;
        }

        var validMethods = new List<ProfileMethodRegistration>();
        var validOptionsTypes = new List<OptionsTypeRegistration>();
        var allRegisteredTypes = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);

        foreach (var candidate in methodCandidates)
        {
            if (!TryValidateProfileMethod(candidate, context, out var methodRegistration))
            {
                continue;
            }

            validMethods.Add(methodRegistration);

            foreach (var registeredType in methodRegistration.RegisteredTypes)
            {
                var typeExpression = registeredType.ToDisplayString(TypeExpressionFormat);
                allRegisteredTypes[typeExpression] = registeredType;
            }
        }

        foreach (var candidate in optionsTypeCandidates)
        {
            if (!TryValidateOptionsType(candidate, context, out var optionsTypeRegistration))
            {
                continue;
            }

            validOptionsTypes.Add(optionsTypeRegistration);

            foreach (var registeredType in optionsTypeRegistration.RegisteredTypes)
            {
                var typeExpression = registeredType.ToDisplayString(TypeExpressionFormat);
                allRegisteredTypes[typeExpression] = registeredType;
            }
        }

        if ((validMethods.Count == 0 && validOptionsTypes.Count == 0) || allRegisteredTypes.Count == 0)
        {
            return;
        }

        var accessorsByType = new Dictionary<string, AccessorRegistration>(StringComparer.Ordinal);
        var usedAccessorNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typeEntry in allRegisteredTypes.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            var members = GetMembers(typeEntry.Value);
            if (members.Count == 0)
            {
                continue;
            }

            var accessorName = CreateAccessorName(typeEntry.Value, usedAccessorNames);
            accessorsByType[typeEntry.Key] = new AccessorRegistration(typeEntry.Key, accessorName, members);
        }

        if (accessorsByType.Count == 0)
        {
            return;
        }

        var methodRegistrations = validMethods
            .Select(method => new MethodRegistration(
                method.Method,
                method.RegisteredTypes
                    .Select(type => type.ToDisplayString(TypeExpressionFormat))
                    .Where(typeExpression => accessorsByType.ContainsKey(typeExpression))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static x => x, StringComparer.Ordinal)
                    .Select(typeExpression => accessorsByType[typeExpression])
                    .ToImmutableArray()))
            .Where(static method => !method.Accessors.IsDefaultOrEmpty)
            .OrderBy(static x => x.Method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ThenBy(static x => x.Method.Name, StringComparer.Ordinal)
            .ToList();

        var optionsTypeRegistrations = validOptionsTypes
            .Select(optionsType => new GeneratedOptionsTypeRegistration(
                optionsType.OptionsType,
                optionsType.RegisteredTypes
                    .Select(type => type.ToDisplayString(TypeExpressionFormat))
                    .Where(typeExpression => accessorsByType.ContainsKey(typeExpression))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static x => x, StringComparer.Ordinal)
                    .Select(typeExpression => accessorsByType[typeExpression])
                    .ToImmutableArray()))
            .Where(static optionsType => !optionsType.Accessors.IsDefaultOrEmpty)
            .OrderBy(static x => x.OptionsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToList();

        if (methodRegistrations.Count == 0 && optionsTypeRegistrations.Count == 0)
        {
            return;
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Fluid.SourceGenerated");
        source.AppendLine("{");

        foreach (var accessor in accessorsByType.Values.OrderBy(static x => x.TypeExpression, StringComparer.Ordinal))
        {
            AppendAccessor(source, accessor.AccessorName, accessor.TypeExpression, accessor.Members);
            source.AppendLine();
        }

        source.AppendLine("}");
        source.AppendLine();

        foreach (var containingTypeGroup in methodRegistrations.GroupBy(static x => x.Method.ContainingType, SymbolEqualityComparer.Default))
        {
            AppendProfileType(source, (INamedTypeSymbol)containingTypeGroup.Key!, containingTypeGroup.ToImmutableArray());
            source.AppendLine();
        }

        foreach (var optionsType in optionsTypeRegistrations)
        {
            AppendOptionsType(source, optionsType.OptionsType, optionsType.Accessors);
            source.AppendLine();
        }

        context.AddSource("Fluid.MemberAccessProfiles.g.cs", source.ToString());
    }

    private static bool TryValidateProfileMethod(ProfileMethodCandidate candidate, SourceProductionContext context, out ProfileMethodRegistration registration)
    {
        registration = default!;

        var method = candidate.Method;
        var containingType = method.ContainingType;

        var isValid =
            candidate.HasPartialModifier &&
            candidate.HasExplicitAccessibility &&
            !candidate.HasBody &&
            method.IsStatic &&
            method.ReturnsVoid &&
            method.Arity == 0 &&
            method.Parameters.Length == 1 &&
            string.Equals(method.Parameters[0].Type.ToDisplayString(), TemplateOptionsType, StringComparison.Ordinal) &&
            method.PartialImplementationPart is null &&
            containingType is not null &&
            containingType.TypeKind == TypeKind.Class &&
            containingType.Arity == 0 &&
            containingType.ContainingType is null &&
            IsPartialType(containingType);

        if (!isValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidProfileMethod, candidate.Location, method.ToDisplayString()));
            return false;
        }

        registration = new ProfileMethodRegistration(method, candidate.RegisteredTypes);
        return true;
    }

    private static bool TryValidateOptionsType(OptionsTypeCandidate candidate, SourceProductionContext context, out OptionsTypeRegistration registration)
    {
        registration = default!;

        var type = candidate.OptionsType;

        var isValid =
            candidate.HasPartialModifier &&
            type.TypeKind == TypeKind.Class &&
            type.Arity == 0 &&
            type.ContainingType is null &&
            InheritsFromTemplateOptions(type);

        if (!isValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidOptionsType, candidate.Location, type.ToDisplayString()));
            return false;
        }

        registration = new OptionsTypeRegistration(type, candidate.RegisteredTypes);
        return true;
    }

    private static bool InheritsFromTemplateOptions(INamedTypeSymbol typeSymbol)
    {
        for (var current = typeSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), TemplateOptionsType, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartialType(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is TypeDeclarationSyntax typeSyntax &&
                typeSyntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendProfileType(StringBuilder source, INamedTypeSymbol containingType, ImmutableArray<MethodRegistration> methods)
    {
        var namespaceName = containingType.ContainingNamespace?.IsGlobalNamespace == false
            ? containingType.ContainingNamespace.ToDisplayString()
            : null;

        if (namespaceName is not null)
        {
            source.Append("namespace ").Append(namespaceName).AppendLine();
            source.AppendLine("{");
        }

        source.Append("    ")
            .Append(GetAccessibilityKeyword(containingType.DeclaredAccessibility))
            .Append(containingType.IsStatic ? " static" : string.Empty)
            .Append(" partial class ")
            .Append(containingType.Name)
            .AppendLine();
        source.AppendLine("    {");

        foreach (var method in methods)
        {
            var escapedName = EscapeIdentifier(method.Method.Name);
            source.Append("        ")
                .Append(GetAccessibilityKeyword(method.Method.DeclaredAccessibility))
                .Append(" static partial void ")
                .Append(escapedName)
                .AppendLine("(global::Fluid.TemplateOptions options)");
            source.AppendLine("        {");
            source.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(options);");
            source.AppendLine("            var strategy = options.MemberAccessStrategy;");
            source.AppendLine();

            foreach (var accessor in method.Accessors)
            {
                source.Append("            strategy.Register(typeof(")
                    .Append(accessor.TypeExpression)
                    .Append("), \"*\", new global::Fluid.SourceGenerated.")
                    .Append(accessor.AccessorName)
                    .AppendLine("());");
            }

            source.AppendLine("        }");
            source.AppendLine();
        }

        source.AppendLine("    }");

        if (namespaceName is not null)
        {
            source.AppendLine("}");
        }
    }

    private static void AppendOptionsType(StringBuilder source, INamedTypeSymbol optionsType, ImmutableArray<AccessorRegistration> accessors)
    {
        var namespaceName = optionsType.ContainingNamespace?.IsGlobalNamespace == false
            ? optionsType.ContainingNamespace.ToDisplayString()
            : null;

        if (namespaceName is not null)
        {
            source.Append("namespace ").Append(namespaceName).AppendLine();
            source.AppendLine("{");
        }

        source.Append("    ")
            .Append(GetAccessibilityKeyword(optionsType.DeclaredAccessibility))
            .Append(" partial class ")
            .Append(optionsType.Name)
            .AppendLine(" : global::Fluid.ITemplateOptionsMemberAccessorRegistrar");
        source.AppendLine("    {");
        source.AppendLine("        void global::Fluid.ITemplateOptionsMemberAccessorRegistrar.RegisterMemberAccessors(global::Fluid.TemplateOptions options)");
        source.AppendLine("        {");
        source.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(options);");
        source.AppendLine("            var strategy = options.MemberAccessStrategy;");
        source.AppendLine();

        foreach (var accessor in accessors)
        {
            source.Append("            strategy.Register(typeof(")
                .Append(accessor.TypeExpression)
                .Append("), \"*\", new global::Fluid.SourceGenerated.")
                .Append(accessor.AccessorName)
                .AppendLine("());");
        }

        source.AppendLine("        }");
        source.AppendLine("    }");

        if (namespaceName is not null)
        {
            source.AppendLine("}");
        }
    }

    private static string GetAccessibilityKeyword(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private"
        };
    }

    private static void AppendAccessor(StringBuilder source, string accessorName, string typeExpression, List<MemberAccess> members)
    {
        source.Append("    internal sealed class ").Append(accessorName).AppendLine(" : global::Fluid.IAsyncMemberAccessor");
        source.AppendLine("    {");
        source.AppendLine("        public object Get(object obj, string name, global::Fluid.TemplateContext ctx)");
        source.AppendLine("        {");
        source.Append("            var typed = (").Append(typeExpression).AppendLine(")obj;");
        source.AppendLine("            var comparer = ctx.Options.ModelNamesComparer;");
        source.AppendLine();

        foreach (var member in members)
        {
            source.Append("            if (comparer.Equals(name, \"")
                .Append(member.Name)
                .AppendLine("\"))");
            source.AppendLine("            {");
            source.Append("                return ").Append(member.Expression).AppendLine(";");
            source.AppendLine("            }");
        }

        source.AppendLine();
        source.AppendLine("            return null;");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        public async global::System.Threading.Tasks.Task<object> GetAsync(object obj, string name, global::Fluid.TemplateContext ctx)");
        source.AppendLine("        {");
        source.Append("            var typed = (").Append(typeExpression).AppendLine(")obj;");
        source.AppendLine("            var comparer = ctx.Options.ModelNamesComparer;");
        source.AppendLine();

        foreach (var member in members)
        {
            source.Append("            if (comparer.Equals(name, \"")
                .Append(member.Name)
                .AppendLine("\"))");
            source.AppendLine("            {");

            switch (member.AsyncKind)
            {
                case AsyncKind.Task:
                    source.Append("                var task = ").Append(member.Expression).AppendLine(";");
                    source.AppendLine("                await task.ConfigureAwait(false);");
                    source.AppendLine("                return task.Result;");
                    break;
                case AsyncKind.ValueTask:
                    source.Append("                var valueTask = ").Append(member.Expression).AppendLine(";");
                    source.AppendLine("                return await valueTask.ConfigureAwait(false);");
                    break;
                default:
                    source.Append("                return ").Append(member.Expression).AppendLine(";");
                    break;
            }

            source.AppendLine("            }");
        }

        source.AppendLine();
        source.AppendLine("            return null;");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static List<MemberAccess> GetMembers(ITypeSymbol typeSymbol)
    {
        var members = new List<MemberAccess>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in EnumerateProperties(typeSymbol))
        {
            if (!names.Add(property.Name))
            {
                continue;
            }

            var memberName = EscapeIdentifier(property.Name);
            if (memberName is null)
            {
                continue;
            }

            var expression = property.IsStatic
                ? $"{typeSymbol.ToDisplayString(TypeExpressionFormat)}.{memberName}"
                : $"typed.{memberName}";

            members.Add(new MemberAccess(property.Name, expression, GetAsyncKind(property.Type)));
        }

        foreach (var field in EnumerateFields(typeSymbol))
        {
            if (!names.Add(field.Name))
            {
                continue;
            }

            var memberName = EscapeIdentifier(field.Name);
            if (memberName is null)
            {
                continue;
            }

            var expression = field.IsStatic
                ? $"{typeSymbol.ToDisplayString(TypeExpressionFormat)}.{memberName}"
                : $"typed.{memberName}";

            members.Add(new MemberAccess(field.Name, expression, GetAsyncKind(field.Type)));
        }

        foreach (var method in EnumerateMethods(typeSymbol))
        {
            if (!names.Add(method.Name))
            {
                continue;
            }

            var memberName = EscapeIdentifier(method.Name);
            if (memberName is null)
            {
                continue;
            }

            var expression = method.IsStatic
                ? $"{typeSymbol.ToDisplayString(TypeExpressionFormat)}.{memberName}()"
                : $"typed.{memberName}()";

            members.Add(new MemberAccess(method.Name, expression, GetAsyncKind(method.ReturnType)));
        }

        return members;
    }

    private static IEnumerable<IPropertySymbol> EnumerateProperties(ITypeSymbol typeSymbol)
    {
        foreach (var symbol in EnumerateMembers(typeSymbol).OfType<IPropertySymbol>())
        {
            if (symbol.IsIndexer || symbol.GetMethod is null)
            {
                continue;
            }

            if (symbol.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            yield return symbol;
        }
    }

    private static IEnumerable<IFieldSymbol> EnumerateFields(ITypeSymbol typeSymbol)
    {
        foreach (var symbol in EnumerateMembers(typeSymbol).OfType<IFieldSymbol>())
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public || symbol.IsConst)
            {
                continue;
            }

            yield return symbol;
        }
    }

    private static IEnumerable<IMethodSymbol> EnumerateMethods(ITypeSymbol typeSymbol)
    {
        foreach (var symbol in EnumerateMembers(typeSymbol).OfType<IMethodSymbol>())
        {
            if (symbol.MethodKind != MethodKind.Ordinary || symbol.IsGenericMethod)
            {
                continue;
            }

            if (symbol.DeclaredAccessibility != Accessibility.Public || symbol.Parameters.Length != 0 || symbol.ReturnsVoid)
            {
                continue;
            }

            if (symbol.ContainingType.SpecialType == SpecialType.System_Object)
            {
                continue;
            }

            yield return symbol;
        }
    }

    private static IEnumerable<ISymbol> EnumerateMembers(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                yield return member;
            }

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    yield return member;
                }
            }

            yield break;
        }

        for (var current = typeSymbol; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                yield return member;
            }
        }
    }

    private static AsyncKind GetAsyncKind(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return AsyncKind.None;
        }

        var containingNamespace = namedType.ContainingNamespace?.ToDisplayString();
        if (!string.Equals(containingNamespace, "System.Threading.Tasks", StringComparison.Ordinal))
        {
            return AsyncKind.None;
        }

        return namedType.Name switch
        {
            "Task" => AsyncKind.Task,
            "ValueTask" => AsyncKind.ValueTask,
            _ => AsyncKind.None
        };
    }

    private static string CreateAccessorName(ITypeSymbol typeSymbol, HashSet<string> usedAccessorNames)
    {
        var baseName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var accessorNameBuilder = new StringBuilder(baseName.Length + 16);

        for (var i = 0; i < baseName.Length; i++)
        {
            var c = baseName[i];
            accessorNameBuilder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        accessorNameBuilder.Append("_GeneratedMemberAccessor");
        var accessorName = accessorNameBuilder.ToString();

        if (usedAccessorNames.Add(accessorName))
        {
            return accessorName;
        }

        var suffix = 2;
        while (!usedAccessorNames.Add(accessorName + suffix))
        {
            suffix++;
        }

        return accessorName + suffix;
    }

    private static string? EscapeIdentifier(string identifier)
    {
        if (!SyntaxFacts.IsValidIdentifier(identifier))
        {
            return null;
        }

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ? "@" + identifier : identifier;
    }

    private sealed record ProfileMethodCandidate(
        IMethodSymbol Method,
        ImmutableArray<ITypeSymbol> RegisteredTypes,
        bool HasPartialModifier,
        bool HasExplicitAccessibility,
        bool HasBody,
        Location Location);

    private sealed record OptionsTypeCandidate(
        INamedTypeSymbol OptionsType,
        ImmutableArray<ITypeSymbol> RegisteredTypes,
        bool HasPartialModifier,
        Location Location);

    private sealed record ProfileMethodRegistration(
        IMethodSymbol Method,
        ImmutableArray<ITypeSymbol> RegisteredTypes);

    private sealed record OptionsTypeRegistration(
        INamedTypeSymbol OptionsType,
        ImmutableArray<ITypeSymbol> RegisteredTypes);

    private sealed record AccessorRegistration(
        string TypeExpression,
        string AccessorName,
        List<MemberAccess> Members);

    private sealed record MethodRegistration(
        IMethodSymbol Method,
        ImmutableArray<AccessorRegistration> Accessors);

    private sealed record GeneratedOptionsTypeRegistration(
        INamedTypeSymbol OptionsType,
        ImmutableArray<AccessorRegistration> Accessors);

    private sealed record MemberAccess(string Name, string Expression, AsyncKind AsyncKind);

    private enum AsyncKind
    {
        None,
        Task,
        ValueTask
    }

    private static readonly string AttributeSource = """
        // <auto-generated />
        #nullable enable
        namespace Fluid
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
            internal sealed class FluidRegisterAttribute : global::System.Attribute
            {
                public FluidRegisterAttribute(global::System.Type type)
                {
                    Type = type;
                }

                public global::System.Type Type { get; }
            }
        }
        """;
}
