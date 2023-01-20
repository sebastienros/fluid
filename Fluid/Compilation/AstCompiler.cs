using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;
using Fluid.Values;
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
                    accessor = segment switch
                    {
                        IdentifierSegment i => $@"await ({accessor}).GetValueAsync(""{i.Identifier}"", context)",
                        FunctionCallSegment f => $@"await ({accessor}).InvokeAsync(FunctionArguments.Empty, context)",
                        _ => throw new NotSupportedException("Invalid segment type")
                    };
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
        protected FluidValues _lastExpressionType = FluidValues.Nil;

        protected internal override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            // Declares the a literal value pushes the resulting variable on the stack

            switch (literalExpression.Value.Type)
            {
                case FluidValues.Number:
                    var number = DeclareLocalValue($@"NumberValue.Create({literalExpression.Value.ToNumberValue().ToString(CultureInfo.InvariantCulture)})");
                    _lastExpressionVariable = number;
                    break;
                case FluidValues.String: 
                    var s = DeclareLocalValue($@"StringValue.Create(""{literalExpression.Value.ToStringValue().Replace("\"", "\"\"")}"")");
                    _lastExpressionVariable = s;
                    break;
                case FluidValues.Boolean:
                    var b = DeclareLocalValue(literalExpression.Value.ToBooleanValue() ? "BooleanValue.True" : "BooleanValue.False");
                    _lastExpressionVariable = b;
                    break;
                case FluidValues.Blank:
                    var blank = DeclareLocalValue("BlankValue.Instance");
                    _lastExpressionVariable = blank;
                    break;
                case FluidValues.Empty:
                    var empty = DeclareLocalValue("EmptyValue.Instance");
                    _lastExpressionVariable = empty;
                    break;
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
                        _sb.AppendLine($@"{_indent}await writer.WriteAsync(""{line.Replace("\\", "\\\\").Replace("\"", "\\\"")}"");");
                    }
                }
            }
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

            DeclareLocalValue($@"({left} && {right})", FluidValues.Boolean, true);

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

        protected internal override Expression VisitLowerThanBinaryExpression(LowerThanBinaryExpression lowerThanExpression)
        {
            Visit(lowerThanExpression.Left);
            var left = GetLocalValue();

            Visit(lowerThanExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"LowerThanBinaryExpression.IsLower({left}, {right}, {lowerThanExpression.Strict.ToString().ToLowerInvariant()})", FluidValues.Boolean, true);

            return lowerThanExpression;
        }

        protected internal override Expression VisitGreaterThanBinaryExpression(GreaterThanBinaryExpression greaterThanExpression)
        {
            Visit(greaterThanExpression.Left);
            var left = GetLocalValue();

            Visit(greaterThanExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"GreaterThanBinaryExpression.IsGreater({left}, {right}, {greaterThanExpression.Strict.ToString().ToLowerInvariant()})", FluidValues.Boolean, true);

            return greaterThanExpression;
        }

        protected internal override Expression VisitStartsWithBinaryExpression(StartsWithBinaryExpression startsWithBinaryExpression)
        {
            Visit(startsWithBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(startsWithBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"await StartsWithBinaryExpression.StartsWithAsync({left}, {right}, context)", FluidValues.Boolean, true);

            return startsWithBinaryExpression;
        }

        protected internal override Expression VisitEndsWithBinaryExpression(EndsWithBinaryExpression endsWithBinaryExpression)
        {
            Visit(endsWithBinaryExpression.Left);
            var left = GetLocalValue();

            Visit(endsWithBinaryExpression.Right);
            var right = GetLocalValue();

            DeclareLocalValue($@"await EndsWithBinaryExpression.EndsWithAsync({left}, {right}, context)", FluidValues.Boolean, true);

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

        private string DeclareGlobalVariable(string value, bool isModel = false)
        {
            _locals++;
            var name = $"local_{_locals}";
            _localsb.AppendLine($@"{_defaultIndent}var {name} = {value};");
            _lastExpressionVariable = name;
            _lastExpressionIsModel = isModel;
            return name;
        }

        /// <summary>
        /// Return the _lastExpressionVariable value as a FluidValue
        /// </summary>
        /// <returns></returns>
        private string GetLocalValue()
        {
            var value = _lastExpressionVariable;

            if (!_lastExpressionIsModel)
            {
                return value;
            }

            return _lastExpressionType switch
            {
                FluidValues.Boolean => $"{value} ? BooleanValue.True : BooleanValue.False",
                FluidValues.String => $"StringValue.Create({value})",
                FluidValues.Number => $"NumberValue.Create({value})",
                _ => $"FluidValue.Create({_lastExpressionVariable}, context.Options)"
            };
        }

        private string GetLocalObject(FluidValues typeHint)
        {
            var value = _lastExpressionVariable;

            if (_lastExpressionIsModel)
            {
                if (_lastExpressionType == typeHint)
                {
                    return value;
                }
                else
                {
                    value = $"FluidValue.Create({_lastExpressionVariable}, context.Options)";
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
        private string DeclareLocalValue(string value, FluidValues type = FluidValues.Nil, bool isModel = false)
        {
            _locals++;
            var name = $"local_{_locals}";
            _sb.AppendLine($@"{_indent}var {name} = {value};");
            _lastExpressionVariable = name;
            _lastExpressionIsModel = isModel;
            _lastExpressionType = type;
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
