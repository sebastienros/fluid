using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fluid.Compilation
{
    public class AstCompiler : AstVisitor
    {
        public AstCompiler(TemplateOptions templateOptions) 
        {
            _templateOptions = templateOptions;
        }

        public void RenderTemplate(Type modelType, string templateName, FluidTemplate template, StringBuilder builder)
        {
            _modelType = modelType;

            IncreaseIndent();
            builder.AppendLine(@$"{_indent}public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)");
            builder.AppendLine(@$"{_indent}{{");
            IncreaseIndent();

            _defaultIndent = _indent;

            builder.AppendLine(@$"{_indent}var model = context.Model?.ToObjectValue() as {modelType.FullName};");

            //builder.AppendLine($@"{_indent}TextEncoder encoder = NullEncoder.Default;");

            VisitTemplate(template);

            builder.Append(_localsb);
            builder.Append(_sb);

            builder.AppendLine(@$"{_indent}await writer.FlushAsync();");

            DecreaseIndent();
            builder.AppendLine(@$"{_indent}}}");
            DecreaseIndent();
        }

        private readonly StringBuilder _sb = new();
        private readonly StringBuilder _localsb = new();
        private readonly TemplateOptions _templateOptions;
        private readonly HashSet<string> _localVariables = new();
        private readonly HashSet<string> _modelVariables = new();
        private string _indent = new string(' ', 4);
        private string _defaultIndent = "";
        private int _indentLevel = 0;
        private Type _modelType;
        private int _locals = 0;

        protected internal override Statement VisitForStatement(ForStatement forStatement)
        {
            Visit(forStatement.Source);
            var source = _lastExpressionVariable;

            if (_lastExpressionIsModel)
            {
                _modelVariables.Add(forStatement.Identifier);
                _sb.AppendLine($@"{_indent}foreach (var {forStatement.Identifier} in {source})");
                _sb.AppendLine($@"{_indent}{{");
                IncreaseIndent();
                foreach (var statement in forStatement.Statements)
                {
                    Visit(statement);
                }
                DecreaseIndent();
                _sb.AppendLine($@"{_indent}}}");
                _modelVariables.Remove(forStatement.Identifier);
            }
            else
            {
                _localVariables.Add(forStatement.Identifier);
                _sb.AppendLine($@"{_indent}context.EnterForLoopScope();");
                _sb.AppendLine($@"{_indent}foreach (var {forStatement.Identifier} in {source}.Enumerate(context))");
                _sb.AppendLine($@"{_indent}{{");
                IncreaseIndent();
                _sb.AppendLine($@"{_indent}context.SetValue(""{forStatement.Identifier}"", {forStatement.Identifier});");

                foreach (var statement in forStatement.Statements)
                {
                    Visit(statement);
                }
                DecreaseIndent();
                _sb.AppendLine($@"{_indent}}}");
                _sb.AppendLine($@"{_indent}context.ReleaseScope();");
                _localVariables.Remove(forStatement.Identifier);
            }
            
            return forStatement;
        }

        protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            var identifierSegment = memberExpression.Segments.FirstOrDefault() as IdentifierSegment;
            var identifier = identifierSegment.Identifier;
            _lastExpressionIsModel = false;

            var property = _modelType.GetProperty(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            var field = _modelType.GetField(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

            if (_modelVariables.Contains(identifier))
            {
                // TODO: Resolve each sub-property from the previous type
                // if the MethodInfo's case doesn't match the identifier, create a local variable that will be assigned and behave as an alias
                // e.g., fortune.id => var local = fortune.Id

                var accessor = identifier;
                foreach (var segment in memberExpression.Segments.Skip(1))
                {
                    accessor += "." + (segment as IdentifierSegment).Identifier;
                }

                _lastExpressionVariable = accessor;
                _lastExpressionIsModel = true;

                _modelVariables.Add(accessor);
            }
            else if (_localVariables.Contains(identifier))
            {
                _lastExpressionVariable = identifier;
                _lastExpressionIsModel = true;

                var accessor = identifier;
                foreach (var segment in memberExpression.Segments.Skip(1).Reverse())
                {
                    accessor = $@"await ({accessor}).GetValueAsync(""{(segment as IdentifierSegment).Identifier}"", context)";
                    _lastExpressionVariable = accessor;
                }
            }
            else if (property != null)
            {
                DeclareLocalValue($@"model.{property.Name}");
                _lastExpressionIsModel = true;
                _modelVariables.Add(_lastExpressionVariable);
            }
            else if (field != null)
            {
                DeclareLocalValue($@"model.{field.Name}");
                _lastExpressionIsModel = true;
                _modelVariables.Add(_lastExpressionVariable);
            }
            else
            {
                var accessor = $@"context.GetValue(""{identifier}"")";
                foreach (var segment in memberExpression.Segments.Skip(1).Reverse())
                {
                    accessor = $@"await ({accessor}).GetValueAsync(""{(segment as IdentifierSegment).Identifier}"", context)";
                }

                DeclareLocalValue(accessor);
            }

            return memberExpression;
        }

        protected internal override Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            Visit(outputStatement.Expression);

            if (_lastExpressionIsModel)
            {
                // Dispatching a FluidValue.WriteTo call is expensive
                // Use this to wrap dotnet objects in FluidValue.
                // var wrapped = $"FluidValue.Create({_lastExpressionVariable}, context.Options)";
                // _sb.AppendLine($"{_indent}{wrapped}.WriteTo(writer, encoder, context.CultureInfo);");

                // Invokes CompiledTemplateBase.Write overloads to prevent dynamic dispatch
                _sb.AppendLine($"{_indent}await WriteAsync({_lastExpressionVariable}, writer, encoder, context);");
            }
            else
            {
                _sb.AppendLine($"{_indent}{_lastExpressionVariable}.WriteTo(writer, encoder, context.CultureInfo);");
            }

            return outputStatement;
        }

        protected string _lastExpressionVariable = "";
        protected bool _lastExpressionIsModel = false;

        protected internal override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            // Declares the a literal value pushes the resulting variable on the stack

            switch (literalExpression.Value.Type)
            {
                case Values.FluidValues.Number:
                    var number = DeclareLocalValue($@"NumberValue.Create({literalExpression.Value.ToNumberValue().ToString(CultureInfo.InvariantCulture)})");
                    _lastExpressionVariable = number;
                    break;
                case Values.FluidValues.String: 
                    var s = DeclareLocalValue($@"StringValue.Create(""{literalExpression.Value.ToStringValue().Replace("\"", "\"\"")}"")");
                    _lastExpressionVariable = s;
                    break;
                case Values.FluidValues.Boolean:
                    var b = DeclareLocalValue(literalExpression.Value.ToBooleanValue() ? "BooleanValue.True" : "BooleanValue.False");
                    _lastExpressionVariable = b;
                    break;
                case Values.FluidValues.Blank:
                    var blank = DeclareLocalValue("BlankValue.Instance");
                    _lastExpressionVariable = blank;
                    break;
                case Values.FluidValues.Empty:
                    var empty = DeclareLocalValue("EmptyValue.Instance");
                    _lastExpressionVariable = empty;
                    break;
            }

            return literalExpression;
        }

        protected internal override Expression VisitRangeExpression(RangeExpression rangeExpression)
        {
            Visit(rangeExpression.From);
            var from = _lastExpressionVariable;

            Visit(rangeExpression.To);
            var to = _lastExpressionVariable;

            DeclareLocalValue($@"BuildRangeArray((int){from}.ToNumberValue(), (int){to}.ToNumberValue())");

            return rangeExpression;
        }

        protected internal override Statement VisitTextSpanStatement(TextSpanStatement textSpanStatement)
        {
            // Apply all trim options to the string

            textSpanStatement.PrepareBuffer(_templateOptions);

            // Split the string into separate lines such that each generated
            // string constant fits on a single line in C#

            using (var sr = new StringReader(textSpanStatement.Buffer))
            {
                string line = null;

                if (line != null)
                {
                    _sb.AppendLine($@"{_indent}await writer.WriteLineAsync();");
                }

                while (null != (line = sr.ReadLine()))
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        _sb.AppendLine($@"{_indent}await writer.WriteAsync(""{line.Replace("\"", "\"\"").Replace("\\", "\\\\")}"");");
                    }
                }
            }
            return textSpanStatement;
        }

        protected internal override Expression VisitEqualBinaryExpression(EqualBinaryExpression equalBinaryExpression)
        {
            Visit(equalBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(equalBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"{left}.Equals({right}) ? BooleanValue.True : BooleanValue.False");
            
            _lastExpressionIsModel = false;

            return equalBinaryExpression;
        }

        protected internal override Expression VisitNotEqualBinaryExpression(NotEqualBinaryExpression notEqualBinaryExpression)
        {
            Visit(notEqualBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(notEqualBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"!{left}.Equals({right})? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return notEqualBinaryExpression;
        }

        protected internal override Expression VisitAndBinaryExpression(AndBinaryExpression andBinaryExpression)
        {
            Visit(andBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(andBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"({left}.ToBooleanValue() && {right}.ToBooleanValue()) ? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return andBinaryExpression;
        }

        protected internal override Expression VisitOrBinaryExpression(OrBinaryExpression orBinaryExpression)
        {
            Visit(orBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(orBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"({left}.ToBooleanValue() || {right}.ToBooleanValue()) ? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return orBinaryExpression;
        }

        protected internal override Expression VisitContainsBinaryExpression(ContainsBinaryExpression containsBinaryExpression)
        {
            Visit(containsBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(containsBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"{left}.Contains({right}) ? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return containsBinaryExpression;
        }

        protected internal override Expression VisitLowerThanBinaryExpression(LowerThanBinaryExpression lowerThanExpression)
        {
            Visit(lowerThanExpression.Left);
            var left = _lastExpressionVariable;

            Visit(lowerThanExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"LowerThanBinaryExpression.IsLower({left}, {right}, {lowerThanExpression.Strict.ToString().ToLowerInvariant()})");
            
            _lastExpressionIsModel = false;

            return lowerThanExpression;
        }

        protected internal override Expression VisitGreaterThanBinaryExpression(GreaterThanBinaryExpression greaterThanExpression)
        {
            Visit(greaterThanExpression.Left);
            var left = _lastExpressionVariable;

            Visit(greaterThanExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"GreaterThanBinaryExpression.IsGreater({left}, {right}, {greaterThanExpression.Strict.ToString().ToLowerInvariant()})");

            _lastExpressionIsModel = false;

            return greaterThanExpression;
        }

        protected internal override Expression VisitStartsWithBinaryExpression(StartsWithBinaryExpression startsWithBinaryExpression)
        {
            Visit(startsWithBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(startsWithBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"await StartsWithBinaryExpression.StartsWithAsync({left}, {right}, context) ? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return startsWithBinaryExpression;
        }

        protected internal override Expression VisitEndsWithBinaryExpression(EndsWithBinaryExpression endsWithBinaryExpression)
        {
            Visit(endsWithBinaryExpression.Left);
            var left = _lastExpressionVariable;

            Visit(endsWithBinaryExpression.Right);
            var right = _lastExpressionVariable;

            DeclareLocalValue($@"await EndsWithBinaryExpression.EndsWithAsync({left}, {right}, context) ? BooleanValue.True : BooleanValue.False");

            _lastExpressionIsModel = false;

            return endsWithBinaryExpression;
        }

        protected internal override Statement VisitIfStatement(IfStatement ifStatement)
        {
            Visit(ifStatement.Condition);
            var condition = _lastExpressionVariable;

            // TODO: handle completions of each statement

            if (_lastExpressionIsModel)
            {
                _sb.AppendLine($@"{_indent}if ({condition})");
            }
            else
            {
                _sb.AppendLine($@"{_indent}if ({condition}.ToBooleanValue())");
            }

            WriteBlock(ifStatement.Statements);
            
            foreach (var elseIf in ifStatement.ElseIfs)
            {
                Visit(elseIf.Condition);
                condition = _lastExpressionVariable;

                if (_lastExpressionIsModel)
                {
                    _sb.AppendLine($@"{_indent}elseif ({condition})");
                }
                else
                {
                    _sb.AppendLine($@"{_indent}elseif ({condition}.ToBooleanValue())");
                }

                WriteBlock(elseIf.Statements);
            }

            if (ifStatement.Else != null)
            {
                _sb.AppendLine($@"{_indent}else");

                WriteBlock(ifStatement.Else.Statements);
            }

            return ifStatement;
        }

        private void WriteBlock(IEnumerable<Statement> statements)
        {
            _sb.AppendLine($@"{_indent}{{");
            IncreaseIndent();
            foreach (var statement in statements)
            {
                Visit(statement);
            }
            DecreaseIndent();
            _sb.AppendLine($@"{_indent}}}");
        }

        private string DeclareGlobalVariable(string value)
        {
            _locals++;
            var name = $"local_{_locals}";
            _localsb.AppendLine($@"{_defaultIndent}var {name} = {value};");
            _lastExpressionVariable = name;
            return name;
        }

        /// <summary>
        /// Defines a new local FluidValue variable
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private string DeclareLocalValue(string value)
        {
            _locals++;
            var name = $"local_{_locals}";
            _sb.AppendLine($@"{_indent}var {name} = {value};");
            _lastExpressionVariable = name;
            _lastExpressionIsModel = false;
            return name;
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
    }
}
