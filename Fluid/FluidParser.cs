using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Ast;
using Fluid.Ast.Values;
using Irony.Parsing;
using Microsoft.Extensions.Primitives;

namespace Fluid
{
    /// <summary>
    /// A Parser implementation based on Irony.
    /// </summary>
    public class FluidParser
    {
        private static LanguageData _language = new LanguageData(new FluidGrammar());

        // Used to store statements as we process tag bodies. 
        // Unstacked when we exit a tag.
        private Stack<(ParseTreeNode tag, List<Statement> statements)> _accumulators;
        private List<Statement> _accumulator;

        public bool TryParse(StringSegment template, out List<Statement> result, out IEnumerable<string> errors)
        {
            result = new List<Statement>();
            errors = Array.Empty<string>();
            
            Parser parser = null;
            _accumulators = new Stack<(ParseTreeNode tag, List<Statement> statements)>();

            try
            {
                int previous = 0, index = 0;
                Statement s;

                while (true)
                {
                    previous = index;
                    
                    if (!MatchTag(template, index, out var start, out var end))                    
                    {
                        index = template.Length;

                        if (index != previous)
                        {
                            // Consume last Text statement
                            (_accumulator ?? result).Add(new TextStatement(template.Substring(previous, index - previous)));
                        }

                        break;
                    }
                    else
                    {
                        if (parser == null) 
                        {
                            parser = new Parser(_language);
                        } 
                
                        if (start != previous)
                        {
                            // Consume current Text statement
                            (_accumulator ?? result).Add(new TextStatement(template.Substring(previous, start - previous)));
                        }

                        var tag = template.Substring(start, end - start + 1);
                        var tree = parser.Parse(tag);

                        if (tree.HasErrors())
                        {
                            errors = tree
                                .ParserMessages
                                .Select(x => $"{x.Message} at line:{x.Location.Line}, col:{x.Location.Column}")
                                .ToArray();

                            return false;
                        }

                        switch (tree.Root.Term.Name)
                        {
                            case "output":
                                s = BuildOutputStatement(tree.Root);
                                break;
                            case "tag":
                                s = BuildTagStatement(tree.Root);
                                break;
                            default:
                                s = null;
                                break;
                        }

                        if (s != null)
                        {
                            (_accumulator ?? result).Add(s);
                        }

                        index = end + 1;
                    }
                }

                return true;
            }
            catch (ParseException e)
            {
                errors = new [] { e.Message };
            }

            return false;
        }


        private bool MatchTag(StringSegment template, int startIndex, out int start, out int end)
        {
            start = -1;
            end = -1;

            while (startIndex < template.Length)
            {
                start = template.IndexOf('{', startIndex);
                
                // No match
                if (start == -1)
                {
                    end = -1;
                    return false;
                }

                if (start < template.Length - 1)
                {
                    var c = template.Value[start + 1];
                    if (c == '{' || c == '%')
                    {
                        // Start tag found
                        var endTag = c == '{' ? "}}" : "%}";

                        end = template.Value.IndexOf(endTag, start + 2);

                        if (end == -1)
                        {
                            // No end tag
                            return false;
                        }
                        else
                        {
                            // Found a match
                            end = end + 1;
                            return true;
                        }
                    }
                    else
                    {
                        // Was not a start tag, look further
                        startIndex = start + 1;
                    }
                }
                else
                {
                    return false;
                }                
            }

            return false;
        }

        public OutputStatement BuildOutputStatement(ParseTreeNode node)
        {
            var expressionNode = node.ChildNodes[0];
            var filterListNode = node.ChildNodes[1];

            var expression = BuildExpression(expressionNode);

            var filters = filterListNode.ChildNodes.Select(BuildFilterExpression).ToArray();

            return new OutputStatement(expression, filters);
        }

        public Statement BuildTagStatement(ParseTreeNode node)
        {
            var tag = node.ChildNodes[0];

            switch (tag.Term.Name)
            {
                case "for":
                    _accumulators.Push((tag, _accumulator = new List<Statement>()));
                    break;
                case "endFor":
                    (var t, var statements) = _accumulators.Pop();
                    _accumulator = _accumulators.Any() ? _accumulators.Peek().statements : null;
                    return BuildForStatement(t, statements);

                default:
                    throw new ParseException("Unknown expression type: " + node.Term.Name);
            }

            return null;
        }

        public Statement BuildForStatement(ParseTreeNode node, List<Statement> statements)
        {
            var identifier = node.ChildNodes[0].Token.Text;
            var source = node.ChildNodes[1];
            
            switch (source.Term.Name)
            {
                case "memberAccess":
                    return new ForStatement(statements, identifier, BuildMemberExpression(source));
                case "range":
                    return new ForStatement(statements, identifier, BuildRangeExpression(source));
                default:
                    throw new ParseException("Unknown for source type: " + node.Term.Name);
            }

        }

        public RangeExpression BuildRangeExpression(ParseTreeNode node)
        {
            throw new NotSupportedException();
        }

        public Expression BuildExpression(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            switch (child.Term.Name)
            {
                case "memberAccess":
                    return BuildMemberExpression(child);
                case "literal":
                    return BuildLiteralExpression(child);
                case "unaryExpression":
                    throw new NotSupportedException();
                case "binaryExpression":
                    throw new NotSupportedException();
                default:
                    throw new ParseException("Unknown expression type: " + node.Term.Name);
            }
        }

        public MemberExpression BuildMemberExpression(ParseTreeNode node)
        {
            var identifierNode = node.ChildNodes[0];
            var segmentNodes = node.ChildNodes[1].ChildNodes;

            var segments = new MemberSegmentExpression[segmentNodes.Count + 1];
            segments[0] = new IdentifierSegmentIdentiferExpression(identifierNode.Token.Text);

            for(var i=0; i<segmentNodes.Count; i++)
            {
                var segmentNode = segmentNodes[i];
                segments[i + 1] = BuildMemberSegmentExpression(segmentNode);
            }

            return new MemberExpression(segments);
        }

        public MemberSegmentExpression BuildMemberSegmentExpression(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            switch (child.Term.Name)
            {
                case "memberAccessSegmentIdentifier":
                    return new IdentifierSegmentIdentiferExpression(child.ChildNodes[0].Token.Text);
                case "memberAccessSegmentIndexer":
                    return new IndexerSegmentIdentiferExpression(BuildExpression(child.ChildNodes[0]));
                default:
                    throw new ParseException("Unknown expression type: " + node.Term.Name);
            }
        }

        public LiteralExpression BuildLiteralExpression(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            string raw = child.Token.Text;

            switch (child.Term.Name)
            {
                case "stringLiteral":
                    return new LiteralExpression(new StringValue(raw));
                case "number":
                    if(!double.TryParse(raw, out var number))
                    {
                        throw new ParseException("Invalid number: " + raw);
                    }

                    return new LiteralExpression(new NumberValue(number));
                case "boolean":
                    if (!bool.TryParse(raw, out var boolean))
                    {
                        throw new ParseException("Invalid boolean: " + raw);
                    }
                    return new LiteralExpression(new BooleanValue(boolean));
                default:
                    throw new ParseException("Unknown literal expression: " + node.Term.Name);
            }
        } 

        public FilterExpression BuildFilterExpression(ParseTreeNode node)
        {
            var identifier = node.ChildNodes[0].Token.Text;

            return new FilterExpression(identifier, Array.Empty<Expression>());
        }
    }
}
