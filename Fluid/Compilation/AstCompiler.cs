using Fluid.Ast;
using Fluid.Parser;
using Fluid.Values;
using System.Globalization;
using System.Linq;
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

            builder.AppendLine(@$"{_indent}writer.Flush();");
            builder.AppendLine(@$"{_indent}await new ValueTask<int>(0);");

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


        // TODO: 
        // in forstatement, enumerate the dotnet property directly, and the iterator is a dotnet property too.
        // Only wrap the properties in a FluidValue when an operation needs to be done on it, like WriteTo, or a filter.

        protected internal override Statement VisitForStatement(ForStatement forStatement)
        {
            Visit(forStatement.Source);
            var source = _lastExpressionVariable;

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
            return forStatement;
        }

        protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            var identifierSegment = memberExpression.Segments.FirstOrDefault() as IdentifierSegment;
            var identifier = identifierSegment.Identifier;

            var property = _modelType.GetProperty(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            var field = _modelType.GetField(identifier, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

            if (_modelVariables.Contains(identifier))
            {
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
            }
            else if (property != null)
            {
                DeclareLocalVariable($@"model.{property.Name}");
                _lastExpressionIsModel = true;
                _modelVariables.Add(_lastExpressionVariable);
            }
            else if (field != null)
            {
                DeclareLocalVariable($@"model.{field.Name}");
                _lastExpressionIsModel = true;
                _modelVariables.Add(_lastExpressionVariable);
            }
            else
            {
                DeclareLocalVariable($@"context.GetValue(""{identifier}"")");
            }

            return memberExpression;
        }

        protected internal override Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            Visit(outputStatement.Expression);

            var target = _lastExpressionVariable;

            if (_modelVariables.Contains(target))
            {
                target = $"FluidValue.Create({target}, context.Options)";
            }

            _sb.AppendLine($"{_indent}{target}.WriteTo(writer, encoder, context.CultureInfo);");

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
                    var number = DeclareGlobalVariable($@"NumberValue.Create({literalExpression.Value.ToNumberValue().ToString(CultureInfo.InvariantCulture)})");
                    _lastExpressionVariable = number;
                    break;
                case Values.FluidValues.String: 
                    var s = DeclareGlobalVariable($@"StringValue.Create(""{literalExpression.Value.ToStringValue().Replace("\"", "\"\"")}"");");
                    _lastExpressionVariable = s;
                    break;
                case Values.FluidValues.Boolean:
                    var b = DeclareGlobalVariable(literalExpression.Value.ToBooleanValue() ? "BooleanValue.True" : "BooleanValue.False");
                    _lastExpressionVariable = b;
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

            DeclareLocalVariable($@"BuildArray((int){from}.ToNumberValue(), (int){to}.ToNumberValue())");

            return rangeExpression;
        }

        protected internal override Statement VisitTextSpanStatement(TextSpanStatement textSpanStatement)
        {
            textSpanStatement.PrepareBuffer(_templateOptions);
            _sb.AppendLine($@"{_indent}writer.Write(""{textSpanStatement.Buffer.Replace("\"", "\"\"").Replace("\\", "\\\\")}"");");

            return textSpanStatement;
        }

        private string DeclareGlobalVariable(string value)
        {
            var name = $"local_{_locals++}";
            _localsb.AppendLine($@"{_defaultIndent}var {name} = {value};");
            _lastExpressionVariable = name;
            return name;
        }

        private string DeclareLocalVariable(string value)
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
