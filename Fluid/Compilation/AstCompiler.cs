using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using Fluid.Tests.Visitors;
using Fluid.Values;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fluid.Compilation
{
    /// <remarks>
    /// Security considerations:
    /// - String literals should not be able to be escaped (like SQL injection but for C#) and are encoded using SymbolDisplay.FormatLiteral
    /// - Introduce compiler options to provider compile time optimizations
    ///     - Ignore MaxSteps
    /// - Add a test to ensure a model property can be assigned
    /// </remarks>
    public class AstCompiler : AstVisitor
    {
        public AstCompiler(TemplateOptions templateOptions, CompilerOptions compilerOptions)
        {
            _templateOptions = templateOptions;
            _compilerOptions = compilerOptions;
        }

        public void RenderTemplate(Type modelType, string _, FluidTemplate template, StringBuilder builder, StringBuilder global, StringBuilder staticConstructor)
        {
            _modelType = modelType;

            // Check if the template uses 'offset: continue'
            var continueOffsetVisitor = new ContinueOffsetVisitor();
            continueOffsetVisitor.VisitTemplate(template);
            _hasContinueOffset = continueOffsetVisitor.HasContinueForLoop;

            // If the type is anonymous don't use strongly-typed properties
            if (modelType.FullName.Contains("AnonymousType"))
            {
                _modelType = typeof(object);
            }

            IncreaseIndent();
            builder.AppendLine(@$"{_indent}public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)");
            builder.AppendLine(@$"{_indent}{{");
            IncreaseIndent();

            _defaultIndent = _indent;

            WriteLine($$"""
                var contextModel = context.Model?.ToObjectValue();
                var model = contextModel as {{_modelType.FullName}};
                """);

            // TODO: Check that the compiled template is used with a model it was created for.
            // This can be done while the template is generated when an accessor is a valid property of the model.
            // If one is found then we can inject this check.

            WriteLine($$"""
            if (model == null && contextModel != null)
            {
                throw new InvalidOperationException("The model provided is not a valid instance of '{{_modelType.FullName}}'.");
            }
            """);

            VisitTemplate(template);

            builder.Append(_localSb);
            builder.Append(_currentBuilder);
            global.Append(_staticsSb);
            staticConstructor.Append(_staticConstructorSb);

            builder.AppendLine(@$"{_indent}await writer.FlushAsync();");

            DecreaseIndent();
            builder.AppendLine(@$"{_indent}}}");
            DecreaseIndent();
        }

        private Variable _lastVariable;

        private StringBuilder _currentBuilder = new();
        private readonly StringBuilder _localSb = new();
        private readonly StringBuilder _staticsSb = new();
        private readonly StringBuilder _staticConstructorSb = new();
        private readonly TemplateOptions _templateOptions;
        private readonly CompilerOptions _compilerOptions;
        private readonly HashSet<string> _localVariables = new();
        private readonly HashSet<string> _modelVariables = new();
        private readonly Dictionary<string, Variable> _variableMappings = new(); // Maps a locally assigned property to its CSharp local_x member
        private string _indent = new string(' ', 4);
        private string _defaultIndent = "";
        private int _indentLevel = 0;
        private Type _modelType;
        private int _locals = 0;
        private readonly Dictionary<string, string> _macroValues = [];
        private readonly Stack<int> _indentLevelsStack = new();
        private readonly Stack<StringBuilder> _stringBuildersStack = new();
        private bool _hasContinueOffset = false;

        public override Expression Visit(Expression expression)
        {
            // Handle case when Expression is null

            if (expression == null)
            {
                return base.Visit(new LiteralExpression(NilValue.Instance));
            }

            return base.Visit(expression);
        }

        protected internal override Statement VisitForStatement(ForStatement forStatement)
        {
            Visit(forStatement.Source);
            var source = _lastVariable;
            var sourceIsModel = source.IsModel;

            // Check if this loop is using the 'forloop' variable
            // If not then we don't have to maintain its state

            var forloopVisitor = new IdentifierIsAccessedVisitor("forloop");
            forloopVisitor.Visit(forStatement);
            var forloopAccessed = forloopVisitor.IsAccessed;

            var hasElseStatement = forStatement.Else != null;

            Variable counterVariable = new(); // To track the number of times the loop was executed

            if (sourceIsModel)
            {
                _modelVariables.Add(forStatement.Identifier);
            }
            else
            {
                source = DeclareLocalValue($@"{source.Name}.Enumerate(context)");
                _localVariables.Add(forStatement.Identifier);
                WriteLine("context.EnterForLoopScope();");
            }

            var continueOffsetLiteral = forStatement.Source is MemberExpression m ? "for_continue_" + ((IdentifierSegment)m.Segments[0]).Identifier : null;

            if (_hasContinueOffset && !_localVariables.Contains(continueOffsetLiteral))
            {
                WriteLine($"var {continueOffsetLiteral} = 0;");
                _localVariables.Add(continueOffsetLiteral);
            }

            Variable startIndexVariable = new();

            if (forStatement.Offset != null)
            {
                if (_hasContinueOffset)
                {
                    startIndexVariable = DeclareLocalValue($"{continueOffsetLiteral}", isModel: true);
                }
                else
                {
                    Visit(forStatement.Offset);
                    startIndexVariable = DeclareLocalValue($"(int){GetLocalObject(FluidValues.Number)}", isModel: true);
                }

                source = DeclareLocalValue($"{source.Name}.Skip({startIndexVariable.Name})", isModel: true);
            }
            else if (forloopAccessed || hasElseStatement || forStatement.Limit != null)
            {
                startIndexVariable = DeclareLocalValue($@"0", isModel: true);
            }

            if (forStatement.Limit != null)
            {
                Visit(forStatement.Limit);

                source = DeclareLocalValue($@"{source.Name}.Take((int){GetLocalObject(FluidValues.Number)})", isModel: true);
            }

            Variable forloopVariable = new();

            if (_hasContinueOffset || forloopAccessed || hasElseStatement)
            {
                counterVariable = DeclareLocalValue($@"{startIndexVariable.Name}", isModel: true);

                if (_hasContinueOffset || forloopAccessed)
                {
                    forloopVariable = DeclareLocalValue($@"new ForLoopValue()");
                }
            }

            if (sourceIsModel)
            {
                WriteLine($$"""
                    foreach (var {{forStatement.Identifier}} in {{source.Name}})
                    {
                    """);
                IncreaseIndent();
            }
            else
            {
                WriteLine($$"""
                    foreach (var {{forStatement.Identifier}} in {{source.Name}})
                    {
                    """);
                IncreaseIndent();
                WriteLine($@"context.SetValue(""{forStatement.Identifier}"", {forStatement.Identifier});");
            }

            if (_hasContinueOffset || forloopAccessed)
            {
                WriteLine($@"{counterVariable.Name}++;");
            }

            if (forloopAccessed)
            {
                WriteLine($$"""
                    {{forloopVariable.Name}}.Index = {counterVariable.Name} + 1;
                    {{forloopVariable.Name}}.Index0 = {counterVariable.Name};
                    {{forloopVariable.Name}}.RIndex = length - {counterVariable.Name} - 1;
                    {{forloopVariable.Name}}.RIndex0 = length - {counterVariable.Name};
                    {{forloopVariable.Name}}.First = {counterVariable.Name} == 0;
                    {{forloopVariable.Name}}.Last = {counterVariable.Name} == length - 1;
                    """);
            }

            if (_hasContinueOffset)
            {
                WriteLine($@"{continueOffsetLiteral} = {counterVariable.Name};");
                _ = DeclareLocalValue($@"{source.Name}.Skip({continueOffsetLiteral})", isModel: true);
            }

            foreach (var statement in forStatement.Statements)
            {
                Visit(statement);
            }

            if (sourceIsModel)
            {
                DecreaseIndent();
                WriteLine($@"}}");
                _modelVariables.Remove(forStatement.Identifier);
            }
            else
            {
                DecreaseIndent();
                WriteLine($@"}}");
                WriteLine($@"context.ReleaseScope();");
                _localVariables.Remove(forStatement.Identifier);
            }

            return forStatement;
        }

        protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            // TODO: Resolve each sub-property from the previous type
            // if the MethodInfo's case doesn't match the identifier, create a local variable that will be assigned and behave as an alias
            // e.g., fortune.id => var local = fortune.Id
            // The recursive function should take a base accessor, a MemberSegment and a Type. If the accessor is null then it means it should resolve the property
            // on the Model or the Context. Otherwise from the 'accessor' argument based on the Type, which can be FluidValue.

            // The outcome of this statements should be a Variable instance which is initialized with the results of the expression.
            // Examples:
            // - FirstName => "model.FirstName"
            // - Address.City => "model.Address.City"

            // Other cases to take into account:
            // - FunctionValue: hello('world')
            // - Macros: hello('world')
            // - Indexers: a.b[c]

            // It the model type doesn't have the property, it is accessed from the TemplateContext

            // TODO: Ensure and unit-test that a method can't be invoked when using a FunctionCallSegment

            var identifierSegment = memberExpression.Segments.FirstOrDefault() as IdentifierSegment;
            var identifier = identifierSegment.Identifier;

            var property = _modelType.GetProperty(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            var field = _modelType.GetField(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

            if (_modelVariables.Contains(identifier))
            {
                // _modelVariables contains the variable names that have been declared from Model properties
                var accessor = identifier;
                foreach (var segment in memberExpression.Segments.Skip(1))
                {
                    accessor += "." + (segment as IdentifierSegment).Identifier;
                }

                _modelVariables.Add(accessor);

                _lastVariable = _lastVariable with { Name = accessor, IsModel = true };

                return memberExpression;
            }
            else if (_localVariables.Contains(identifier))
            {
                // _localVariables contains variable names that are defined directly in code instead of using DeclareLocalValue,
                // hence they can be used directly

                var accessor = identifier;
                foreach (var segment in memberExpression.Segments.Skip(1).Reverse())
                {
                    if (segment is IndexerSegment indexerSegment)
                    {
                        Visit(indexerSegment.Expression);
                        accessor = $@"await ({accessor}).GetIndexAsync({GetLocalValue()}, context)";
                    }
                    else if (segment is IdentifierSegment ifs)
                    {
                        accessor = $@"await ({accessor}).GetValueAsync(""{ifs.Identifier}"", context)";
                    }
                }

                _lastVariable = _lastVariable with { Name = accessor, IsModel = true };

                return memberExpression;
            }
            else if (property != null)
            {
                DeclareLocalValue($@"model.{property.Name}", isModel: true);
                _modelVariables.Add(_lastVariable.Name);
                return memberExpression;

            }
            else if (field != null)
            {
                DeclareLocalValue($@"model.{field.Name}", isModel: true);
                _modelVariables.Add(_lastVariable.Name);
                return memberExpression;

            }
            else if (_variableMappings.TryGetValue(identifier, out var mapped))
            {
                // _variableMappings contains the variables defined with {% assign %}
                _lastVariable = mapped;

                var nextSegment = memberExpression.Segments.Skip(1).FirstOrDefault();

                if (nextSegment != null)
                {
                    if (nextSegment is IdentifierSegment i)
                    {
                        DeclareLocalValue($@"await {_lastVariable.Name}.GetValueAsync(""{i.Identifier}"", context)");
                    }
                    else if (nextSegment is FunctionCallSegment f)
                    {
                        // Invoking a function requires to initialize a FunctionArguments instance
                        if (f.Arguments.Any())
                        {
                            // If all the arguments are literal expressions, define the FunctionArguments statically
                            var _canBeCached = true;
                            var initArguments = new List<string>();
                            foreach (var parameter in f.Arguments)
                            {
                                Visit(parameter.Expression);
                                _canBeCached = _canBeCached && parameter.Expression is LiteralExpression;
                                if (String.IsNullOrEmpty(parameter.Name))
                                {
                                    initArguments.Add($@".Add({_lastVariable.Name})");
                                }
                                else
                                {
                                    initArguments.Add($@".Add(""{parameter.Name}"", {_lastVariable.Name})");
                                }
                            }
                            // FunctionArguments instances that contain only LiteralExpression are declared as static fields in the template class
                            if (_canBeCached)
                            {
                                DeclareStaticVariable($@"new FunctionArguments(){String.Concat(initArguments)}", cSharpType: "FunctionArguments", isModel: true);
                            }
                            else
                            {
                                DeclareLocalValue($@"new FunctionArguments(){String.Concat(initArguments)}", isModel: true);
                            }
                        }
                        else
                        {
                            _lastVariable = new("FunctionArguments.Empty", true);
                        }
                        DeclareLocalValue($@"await {_lastVariable.Name}.InvokeAsync(context)");
                    }
                    else if (nextSegment is IndexerSegment d)
                    {
                        Visit(d.Expression);
                        DeclareLocalValue($@"await {_lastVariable.Name}.GetIndexAsync({GetLocalValue()}, context)");
                    }
                    else
                    {
                        throw new NotSupportedException("Invalid segment");
                    }
                }

                return memberExpression;
            }
            else if (_modelType is IDictionary)
            {
                if (_modelType is IDictionary<string, object>)
                {

                }
                else if (_modelType is IDictionary<string, FluidValue>)
                {

                }
                else
                {

                }
            }
            else
            {
                var accessor = $@"context.GetValue(""{identifier}"")";

                if (_macroValues.TryGetValue(identifier, out var macro))
                {
                    accessor = macro;
                }

                foreach (var segment in memberExpression.Segments.Skip(1))
                {
                    if (segment is IdentifierSegment i)
                    {
                        accessor = $@"await ({accessor}).GetValueAsync(""{i.Identifier}"", context)";
                    }
                    else if (segment is FunctionCallSegment f)
                    {
                        // Invoking a function requires to initialize a FunctionArguments instance

                        if (f.Arguments.Any())
                        {
                            // If all the arguments are literal expressions, define the FunctionArguments statically
                            var _canBeCached = true;

                            var initArguments = new List<string>();

                            foreach (var parameter in f.Arguments)
                            {
                                Visit(parameter.Expression);

                                _canBeCached = _canBeCached && parameter.Expression is LiteralExpression;
                                if (String.IsNullOrEmpty(parameter.Name))
                                {
                                    initArguments.Add($@".Add({_lastVariable.Name})");
                                }
                                else
                                {
                                    initArguments.Add($@".Add(""{parameter.Name}"", {_lastVariable.Name})");
                                }
                            }

                            // FunctionArguments instances that contain only LiteralExpression are declared as static fields in the template class

                            if (_canBeCached)
                            {
                                DeclareStaticVariable($@"new FunctionArguments(){String.Concat(initArguments)}", cSharpType: "FunctionArguments", isModel: true);
                            }
                            else
                            {
                                DeclareLocalValue($@"new FunctionArguments(){String.Concat(initArguments)}", isModel: true);
                            }
                        }
                        else
                        {
                            _lastVariable = new("FunctionArguments.Empty", true);
                        }

                        accessor = $@"await ({accessor}).InvokeAsync({_lastVariable.Name}, context)";
                    }
                    else if (segment is IndexerSegment d)
                    {
                        Visit(d.Expression);

                        accessor = $@"await ({accessor}).GetIndexAsync({GetLocalValue()}, context)";
                    }
                    else
                    {
                        throw new NotSupportedException("Invalid segment");
                    }
                }

                DeclareLocalValue(accessor);
            }

            return memberExpression;
        }

        protected internal override Statement VisitAssignStatement(AssignStatement assignStatement)
        {
            Visit(assignStatement.Value);
            var localValue = GetLocalValue();

            DeclareLocalValue(@$"context.Assigned == null ? {localValue} : await context.Assigned.Invoke(""{assignStatement.Identifier}"", {localValue}, context)");

            // TODO: Maybe it should actually force a call to context.SetValue() ?

            if (_compilerOptions.LimitMaxSteps)
            {
                WriteLine("context.IncrementSteps();");
            }

            _variableMappings[assignStatement.Identifier] = _lastVariable;

            return assignStatement;
        }

        protected internal override Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            Visit(outputStatement.Expression);

            if (_compilerOptions.LimitMaxSteps)
            {
                WriteLine("context.IncrementSteps();");
            }

            if (_lastVariable.IsModel)
            {
                // Dispatching a FluidValue.WriteTo call is expensive
                // Use this to wrap dotnet objects in FluidValue.
                // var wrapped = $"FluidValue.Create({_lastExpressionVariable}, context.Options)";
                // _sb.AppendLine($"{_indent}{wrapped}.WriteTo(writer, encoder, context.CultureInfo);");

                // Invokes CompiledTemplateBase.Write overloads to prevent dynamic dispatch
                WriteLine($"await WriteAsync({_lastVariable.Name}, writer, encoder, context);");
            }
            else
            {
                WriteLine($"await {_lastVariable.Name}.WriteToAsync(writer, encoder, context.CultureInfo);");
            }

            return outputStatement;
        }

        protected internal override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            // Declares the a literal value pushes the resulting variable on the stack

            switch (literalExpression.Value.Type)
            {
                case FluidValues.Number:
                    var number = DeclareStaticVariable($@"NumberValue.Create({SymbolDisplay.FormatPrimitive(literalExpression.Value.ToNumberValue(), false, false)}M)", FluidValues.Number, null, false);
                    _lastVariable = _lastVariable with { Name = number, Type = FluidValues.Number };
                    break;
                case FluidValues.String:
                    var s = DeclareStaticVariable($@"StringValue.Create({SymbolDisplay.FormatLiteral(literalExpression.Value.ToStringValue(), true)})", FluidValues.String, null, false);
                    _lastVariable = _lastVariable with { Name = s, Type = FluidValues.String };
                    break;
                case FluidValues.Boolean:
                    _lastVariable = new(literalExpression.Value.ToBooleanValue() ? "BooleanValue.True" : "BooleanValue.False", false, FluidValues.Boolean);
                    break;
                case FluidValues.Blank:
                    _lastVariable = new("BlankValue.Instance", false, FluidValues.Blank);
                    break;
                case FluidValues.Empty:
                    _lastVariable = new("EmptyValue.Instance", false, FluidValues.Empty);
                    break;
                case FluidValues.Nil:
                    _lastVariable = new("NilValue.Instance", false, FluidValues.Nil);
                    break;
                default:
                    throw new NotSupportedException("Invalid literal expression");
            }

            return literalExpression;
        }

        protected internal override Expression VisitRangeExpression(RangeExpression rangeExpression)
        {
            Visit(rangeExpression.From);
            var from = GetLocalObject(FluidValues.Number);

            Visit(rangeExpression.To);
            var to = GetLocalObject(FluidValues.Number);

            DeclareLocalValue($@"BuildRangeArray((int){from}, (int){to})", FluidValues.Array, false);

            return rangeExpression;
        }

        protected internal override Statement VisitTextSpanStatement(TextSpanStatement textSpanStatement)
        {
            // Apply all trim options to the string

            textSpanStatement.PrepareBuffer(_templateOptions);

            if (String.IsNullOrEmpty(textSpanStatement._preparedBuffer))
            {
                return textSpanStatement;
            }

            if (_compilerOptions.LimitMaxSteps)
            {
                WriteLine("context.IncrementSteps();");
            }

            WriteLine($@"await writer.WriteAsync({SymbolDisplay.FormatLiteral(textSpanStatement.Buffer, true)});");

            return textSpanStatement;
        }

        protected internal override Expression VisitEqualBinaryExpression(EqualBinaryExpression equalBinaryExpression)
        {
            Visit(equalBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(equalBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"{left}.Equals({right})", FluidValues.Boolean, true);

            return equalBinaryExpression;
        }

        protected internal override Expression VisitNotEqualBinaryExpression(NotEqualBinaryExpression notEqualBinaryExpression)
        {
            Visit(notEqualBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(notEqualBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"!{left}.Equals({right})", FluidValues.Boolean, true);

            return notEqualBinaryExpression;
        }

        protected internal override Expression VisitAndBinaryExpression(AndBinaryExpression andBinaryExpression)
        {
            Visit(andBinaryExpression.Left);
            var left = GetLocalObject(FluidValues.Boolean);

            Visit(andBinaryExpression.Right);
            var right = GetLocalObject(FluidValues.Boolean);

            DeclareLocalValue($@"{left} && {right}", FluidValues.Boolean, true);

            return andBinaryExpression;
        }

        protected internal override Expression VisitOrBinaryExpression(OrBinaryExpression orBinaryExpression)
        {
            Visit(orBinaryExpression.Left);
            var left = GetLocalObject(FluidValues.Boolean);

            Visit(orBinaryExpression.Right);
            var right = GetLocalObject(FluidValues.Boolean);

            DeclareLocalValue($@"({left} || {right})", FluidValues.Boolean, true);

            return orBinaryExpression;
        }

        protected internal override Expression VisitContainsBinaryExpression(ContainsBinaryExpression containsBinaryExpression)
        {
            Visit(containsBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(containsBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"{left}.Contains({right})", FluidValues.Boolean, true);

            return containsBinaryExpression;
        }

        protected internal override Expression VisitLowerThanBinaryExpression(LowerThanBinaryExpression lowerThanBinaryExpression)
        {
            Visit(lowerThanBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(lowerThanBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"{nameof(LowerThanBinaryExpression)}.{nameof(LowerThanBinaryExpression.IsLowerThan)}({left}, {right}, {lowerThanBinaryExpression.Strict.ToString().ToLowerInvariant()}).ToBooleanValue()", FluidValues.Boolean, true);

            return lowerThanBinaryExpression;
        }

        protected internal override Expression VisitGreaterThanBinaryExpression(GreaterThanBinaryExpression greaterThanBinaryExpression)
        {
            Visit(greaterThanBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(greaterThanBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"{nameof(GreaterThanBinaryExpression)}.{nameof(GreaterThanBinaryExpression.IsGreaterThan)}({left}, {right}, {greaterThanBinaryExpression.Strict.ToString().ToLowerInvariant()}).ToBooleanValue()", FluidValues.Boolean, true);

            return greaterThanBinaryExpression;
        }

        protected internal override Expression VisitStartsWithBinaryExpression(StartsWithBinaryExpression startsWithBinaryExpression)
        {
            Visit(startsWithBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(startsWithBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"(await {nameof(StartsWithBinaryExpression)}.{nameof(StartsWithBinaryExpression.StartsWithAsync)}({left}, {right}, context)).ToBooleanValue()", FluidValues.Boolean, true);

            return startsWithBinaryExpression;
        }

        protected internal override Expression VisitEndsWithBinaryExpression(EndsWithBinaryExpression endsWithBinaryExpression)
        {
            Visit(endsWithBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(endsWithBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"(await {nameof(EndsWithBinaryExpression)}.{nameof(EndsWithBinaryExpression.EndsWithAsync)}({left}, {right}, context)).ToBooleanValue()", FluidValues.Boolean, true);

            return endsWithBinaryExpression;
        }

        protected internal override Statement VisitIfStatement(IfStatement ifStatement)
        {
            Visit(ifStatement.Condition);

            // TODO: handle completions of each statement

            WriteLine($@"if ({GetLocalObject(FluidValues.Boolean)})");

            WriteBlock(ifStatement.Statements);

            foreach (var elseIf in ifStatement.ElseIfs)
            {
                WriteLine($@"else");

                WriteLine($@"{{");

                IncreaseIndent();

                Visit(elseIf.Condition);
                WriteLine($@"if ({GetLocalObject(FluidValues.Boolean)})");

                WriteBlock(elseIf.Statements);

                if (ifStatement.Else != null && elseIf == ifStatement.ElseIfs.LastOrDefault())
                {
                    WriteLine($@"else");
                    WriteBlock(ifStatement.Else.Statements);
                }
            }

            foreach (var _ in ifStatement.ElseIfs)
            {
                DecreaseIndent();
                WriteLine($@"}}");
            }

            if (!ifStatement.ElseIfs.Any() && ifStatement.Else != null)
            {
                WriteLine($@"else");

                WriteBlock(ifStatement.Else.Statements);
            }

            return ifStatement;
        }

        protected internal override Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            if (filterExpression.Parameters.Any())
            {
                // If all the arguments are literal expressions, define the FilterArguments statically
                var _canBeCached = true;

                var initArguments = new List<string>();

                foreach (var parameter in filterExpression.Parameters)
                {
                    Visit(parameter.Expression);

                    _canBeCached = _canBeCached && parameter.Expression is LiteralExpression;
                    if (String.IsNullOrEmpty(parameter.Name))
                    {
                        initArguments.Add($@".Add({_lastVariable.Name})");
                    }
                    else
                    {
                        initArguments.Add($@".Add(""{parameter.Name}"", {_lastVariable.Name})");
                    }
                }

                if (_canBeCached)
                {
                    DeclareStaticVariable($@"new FilterArguments(){String.Concat(initArguments)}", cSharpType: "FilterArguments", isModel: true);
                }
                else
                {
                    DeclareLocalValue($@"new FilterArguments(){String.Concat(initArguments)}", isModel: true);
                }
            }
            else
            {
                _lastVariable = new("FilterArguments.Empty", true, FluidValues.Empty);
            }

            var arguments = _lastVariable.Name;

            Visit(filterExpression.Input);

            //var input = _lastVariable.Name;

            var inputAsFluidValue = GetLocalValue();

            // TODO: Filters could be resolved at the beginning on the template and referenced directly

            DeclareLocalValue($@"default(FilterDelegate)", isModel: true);

            DeclareLocalValue($@"context.Options.Filters.TryGetValue(""{filterExpression.Name}"", out {_lastVariable.Name}) ? await {_lastVariable.Name}({inputAsFluidValue}, {arguments}, context) : {inputAsFluidValue}");
            return filterExpression;
        }

        protected internal override Statement VisitMacroStatement(MacroStatement macroStatement)
        {
            EnterIsolatedBuilder(new StringBuilder());

            IncreaseIndent();

            WriteLine($@"private static async ValueTask<FluidValue> Macro_{macroStatement.Identifier}(FunctionArguments arguments, TemplateContext context, TextEncoder encoder)");
            WriteLine($@"{{");
            IncreaseIndent();

            var initArguments = new List<string>();

            // Create default arguments as a static FunctionArguments instance
            foreach (var parameter in macroStatement.Arguments)
            {
                Visit(parameter.Expression);

                if (String.IsNullOrEmpty(parameter.Name))
                {
                    initArguments.Add($@".Add({_lastVariable.Name})");
                }
                else
                {
                    initArguments.Add($@".Add(""{parameter.Name}"", {_lastVariable.Name})");
                }
            }

            var functionArguments = DeclareStaticVariable($@"new FunctionArguments(){String.Concat(initArguments)}", cSharpType: "FunctionArguments", isModel: true);

            WriteLine($@"using var writer = new StringWriter();");

            WriteLine($@"try");
            WriteLine($@"{{");
            IncreaseIndent();
            WriteLine($@"context.EnterChildScope();");
            WriteLine($@"InitializeFunctionArguments(context, {functionArguments}, arguments);");

            WriteBlock(macroStatement.Statements, false);

            WriteLine($@"var result = writer.ToString();");

            WriteLine($@"return new StringValue(result, false);");
            DecreaseIndent();
            WriteLine($@"}}");
            WriteLine($@"finally");
            WriteLine($@"{{");
            IncreaseIndent();
            WriteLine($@"context.ReleaseScope();");
            DecreaseIndent();
            WriteLine($@"}}");
            DecreaseIndent();
            WriteLine($@"}}");
            _currentBuilder.AppendLine($@"");

            var function = _currentBuilder.ToString();

            LeaveIsolatedBuilder();

            _staticsSb.Insert(0, function);

            DeclareLocalValue($@"new MacroValue(Macro_{macroStatement.Identifier}, encoder)");

            _macroValues[macroStatement.Identifier] = _lastVariable.Name;

            return macroStatement;
        }

        protected internal override Statement VisitIncrementStatement(IncrementStatement incrementStatement)
        {
            var counterIdentifier = $"counter_{incrementStatement.Identifier}";

            if (_localVariables.Add(counterIdentifier))
            {
                WriteLine($@"var {counterIdentifier} = 0;");
            }
            else
            {
                WriteLine($@"{counterIdentifier}++;");
            }

            WriteLine($"await WriteAsync({counterIdentifier}, writer, encoder, context);");
            return incrementStatement;
        }

        protected internal override Statement VisitDecrementStatement(DecrementStatement decrementStatement)
        {
            var counterIdentifier = $"counter_{decrementStatement.Identifier}";

            if (_localVariables.Add(counterIdentifier))
            {
                WriteLine($@"var {counterIdentifier} = 0;");
            }
            else
            {
                WriteLine($@"{counterIdentifier}--;");
            }

            WriteLine($"await WriteAsync({counterIdentifier}, writer, encoder, context);");
            return decrementStatement;
        }

        protected internal override Statement VisitCycleStatement(CycleStatement cycleStatement)
        {
            var allLiterals = cycleStatement.Values.All(x => x is LiteralExpression);

            // if all values are literals, declare the array as a static field
            // otherwise evaluate on each usage of the statement

            string valuesVariable = null;

            if (allLiterals)
            {
                var values = new List<string>();
                foreach (var value in cycleStatement.Values)
                {
                    Visit(value);
                    values.Add(_lastVariable.Name);
                }
                valuesVariable = DeclareStaticVariable($@"new List<FluidValue> {{ {String.Join(", ", values)} }}", FluidValues.Array, "List<FluidValue>", false);
            }
            else
            {
                var values = new List<string>();
                foreach (var value in cycleStatement.Values)
                {
                    Visit(value);
                    values.Add(_lastVariable.Name);
                }
                valuesVariable = DeclareLocalValue($@"new List<FluidValue> {{ {String.Join(", ", values)} }}", FluidValues.Array, false).Name;
            }

            // The group could also be a variable so it needs to be evaluated

            Visit(cycleStatement.Group);
            var groupVariable = DeclareLocalValue($@"String.Concat(""$cycle_"", {_lastVariable.Name}.ToStringValue())", FluidValues.String, false);

            var cycleCounter = $"cycle_{groupVariable.Name}";
            WriteLine($$"""
                var {{cycleCounter}} = context.GetValue({{groupVariable.Name}});

                if ({{cycleCounter}}.IsNil())
                {
                    {{cycleCounter}} = NumberValue.Zero;
                }

                var index_{{_locals}} = (uint){{cycleCounter}}.ToNumberValue() % {{cycleStatement.Values.Count.ToString(CultureInfo.InvariantCulture)}};
                var value_{{_locals}} = {{valuesVariable}}[(int)index_{{_locals}}];
                context.SetValue({{groupVariable.Name}}, NumberValue.Create(index_{{_locals}} + 1));

                await value_{{_locals}}.WriteToAsync(writer, encoder, context.CultureInfo);
                """);

            return cycleStatement;
        }

        protected internal override Statement VisitRawStatement(RawStatement rawStatement)
        {
            WriteLine($@"await writer.WriteAsync({SymbolDisplay.FormatLiteral(rawStatement.Text.ToString(), true)});");
            return rawStatement;
        }

        protected internal override Statement VisitBreakStatement(BreakStatement breakStatement)
        {
            WriteLine($@"break;");
            return breakStatement;
        }

        protected internal override Statement VisitContinueStatement(ContinueStatement continueStatement)
        {
            WriteLine($@"continue;");
            return continueStatement;
        }

        private void WriteBlock(IEnumerable<Statement> statements, bool renderBraces = true)
        {
            if (renderBraces)
            {
                WriteLine($@"{{");
                IncreaseIndent();
            }

            foreach (var statement in statements)
            {
                Visit(statement);
            }

            if (renderBraces)
            {
                DecreaseIndent();
                WriteLine($@"}}");
            }
        }

        private string DeclareStaticVariable(string value, FluidValues type = FluidValues.Nil, string cSharpType = null, bool isModel = false)
        {
            _locals++;
            var name = $"local_{_locals}";
            _staticsSb.AppendLine($@"    private static readonly {cSharpType ?? GetFluidTypeName(type)} {name};");
            _lastVariable = new(name, isModel, type);

            _staticConstructorSb.AppendLine($@"        {name} = {value};");

            return name;
        }

        private string GetFluidTypeName(FluidValues type)
        {
            return type switch
            {
                FluidValues.Boolean => nameof(BooleanValue),
                FluidValues.Array => nameof(ArrayValue),
                FluidValues.Blank => nameof(BlankValue),
                FluidValues.String => nameof(StringValue),
                FluidValues.Number => nameof(NumberValue),
                FluidValues.Object => nameof(ObjectValue),
                FluidValues.Nil => nameof(FluidValue),
                FluidValues.Empty => nameof(EmptyValue),
                FluidValues.DateTime => nameof(DateTimeValue),
                FluidValues.Function => nameof(FunctionValue),
                _ => throw new NotSupportedException("Invalid FluidValue type")
            };
        }

        /// <summary>
        /// Return the _lastExpressionVariable value as a FluidValue
        /// </summary>
        /// <returns></returns>
        private string GetLocalValue()
        {
            var value = _lastVariable.Name;

            if (!_lastVariable.IsModel)
            {
                return value;
            }

            return _lastVariable.Type switch
            {
                FluidValues.Boolean => $"{value} ? BooleanValue.True : BooleanValue.False",
                FluidValues.String => $"StringValue.Create({value})",
                FluidValues.Number => $"NumberValue.Create({value})",
                _ => $"FluidValue.Create({_lastVariable.Name}, context.Options)"
            };
        }

        private string GetLocalObject(FluidValues typeHint)
        {
            var value = _lastVariable.Name;

            if (_lastVariable.IsModel)
            {
                if (_lastVariable.Type == typeHint)
                {
                    return value;
                }
                else
                {
                    value = $"FluidValue.Create({_lastVariable.Name}, context.Options)";
                }
            }

            return typeHint switch
            {
                FluidValues.Boolean => $"{value}.ToBooleanValue()",
                FluidValues.String => $"{value}.ToStringValue()",
                FluidValues.Number => $"{value}.ToNumberValue()",
                FluidValues.Array => $"await {value}.Enumerate(context)",
                _ => throw new NotSupportedException("Invalid type conversion for local value")
            };
        }

        /// <summary>
        /// Defines a new local FluidValue variable
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="isModel"></param>
        /// <returns></returns>
        private Variable DeclareLocalValue(string value, FluidValues type = FluidValues.Nil, bool isModel = false)
        {
            _locals++;
            var name = $"local_{_locals}";
            WriteLine($@"var {name} = {value};");
            _lastVariable = new(name, isModel, type);
            return _lastVariable;
        }

        private void IncreaseIndent()
        {
            _indentLevel++;
            _indent = new string(' ', 4 * _indentLevel);
        }

        private void DecreaseIndent()
        {
            _indentLevel--;
            _indent = new string(' ', 4 * _indentLevel);
        }

        private void BackupIndentLevel()
        {
            _indentLevelsStack.Push(_indentLevel);
            _indentLevel = 0;
        }

        private void RestoreIndentLevel()
        {
            _indentLevel = _indentLevelsStack.Pop();
            _indent = new string(' ', 4 * _indentLevel);
        }

        private void EnterIsolatedBuilder(StringBuilder sb)
        {
            _stringBuildersStack.Push(_currentBuilder);
            _currentBuilder = sb;
            BackupIndentLevel();
        }

        private void LeaveIsolatedBuilder()
        {
            _currentBuilder = _stringBuildersStack.Pop();
            RestoreIndentLevel();
        }

        private void WriteLine(string value)
        {
            _currentBuilder.Append(_indent).AppendLine(value.Replace(Environment.NewLine, Environment.NewLine + _indent));
        }

        private void Write(string value)
        {
            _currentBuilder.Append(_indent).Append(value.Replace(Environment.NewLine, Environment.NewLine + _indent));
        }

        internal sealed record Variable(string Name = null, bool IsModel = false, FluidValues Type = FluidValues.Nil);
    }
}
