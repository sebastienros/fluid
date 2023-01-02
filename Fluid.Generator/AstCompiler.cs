using Fluid.Ast;
using Fluid.Parser;
using Fluid.Values;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fluid.Compilation
{
    public class AstCompiler : AstVisitor
    {
        public AstCompiler() 
        { 
        }

        public void RenderTemplate(ITypeSymbol modelType, string templateName, FluidTemplate template, StringBuilder builder)
        {
            builder.AppendLine(@$"{_indent}public static void Render{templateName}({modelType} model, TextWriter writer, TemplateContext context)");
            builder.AppendLine(@$"{_indent}{{");
            IncreaseIndent();
            builder.AppendLine($@"{_indent}var _locals = new List<FluidValue>();");
            builder.AppendLine($@"{_indent}TextEncoder encoder = NullEncoder.Default;");

            VisitTemplate(template);
            
            builder.AppendLine(@$"{_indent}writer.Flush();");

            DecreaseIndent();
            builder.AppendLine(@$"{_indent}}}");
        }

        private readonly StringBuilder _sb = new();
        private readonly StringBuilder _localsb = new();
        private string _indent = "";
        private int _indentLevel = 0;

        private int _locals = 0;

        protected override Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            Visit(outputStatement.Expression);
            var name = _lastExpressionVariable;
            _sb.Append(_indent).Append(name).Append(" = ");

            _sb.AppendLine(";");
            
            _sb.AppendLine($"{_lastExpressionVariable}.WriteTo(writer, encoder, context.CultureInfo);");

            return outputStatement;
        }

        protected string _lastExpressionVariable = "";

        protected override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            // Declares the a literal value pushes the resulting variable on the stack

            switch (literalExpression.Value.Type)
            {
                case Values.FluidValues.Number:
                    var number = DeclareGlobal($@"NumberValue.Create({literalExpression.Value.ToNumberValue().ToString(CultureInfo.InvariantCulture)})");
                    _lastExpressionVariable = number;
                    break;
                case Values.FluidValues.String: 
                    var s = DeclareGlobal($@"StringValue.Create(""{literalExpression.Value.ToStringValue().Replace("\"", "\"\"")}"");");
                    _lastExpressionVariable = s;
                    break;
                case Values.FluidValues.Boolean:
                    var b = DeclareGlobal(literalExpression.Value.ToBooleanValue() ? "BooleanValue.True" : "BooleanValue.False");
                    _lastExpressionVariable = b;
                    break;
            }

            return literalExpression;
        }

        // Declares a global constant FluidValue that can be reused
        private string DeclareGlobal(string value)
        {
            _locals++;
            var name = $"_local_{_locals}";
            _localsb.AppendLine($@"var {name} = {value};");
            return name;
        }

        private void IncreaseIndent()
        {
            _indentLevel++;
            _indent = new string(' ', 4 * _indentLevel);
        }

        private void DecreaseIndent()
        {
            _indentLevel++;
            _indent = new string(' ', 4 * _indentLevel);
        }
    }
}
