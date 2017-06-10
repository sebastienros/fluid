using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
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
        private Stack<BlockContext> _contexts;
        private BlockContext _currentContext;
        private bool _isComment; // true when the current block is a comment
        private bool _isRaw; // true when the current block is raw

        public bool TryParse(StringSegment template, out IList<Statement> result, out IEnumerable<string> errors)
        {
            errors = Array.Empty<string>();
            
            Parser parser = null;
            _currentContext = new BlockContext(null);
            _contexts = new Stack<BlockContext>();
            result = _currentContext.Statements;

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
                            ConsumeTextStatement(template, previous, index);
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
                            ConsumeTextStatement(template, previous, start);
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
                            _currentContext.Statements.Add(s);
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

        private void ConsumeTextStatement(StringSegment template, int start, int end)
        {
            var textSatement = CreateTextStatement(template, start, end);

            if (textSatement != null)
            {
                if (_isComment)
                {
                    _currentContext.AddStatement(new CommentStatement(textSatement.Text));
                }
                else
                {
                    _currentContext.AddStatement(textSatement);
                }

            }
        }
        /// <summary>
        /// Returns a <see cref="TextStatement"/> where the extra whitespace is stripped 
        /// for a Tag that is the only content on a line
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        private TextStatement CreateTextStatement(StringSegment segment, int start, int end)
        {
            int index;

            if (end < segment.Length - 1 && segment.Value[end + 1] == '%')
            {
                index = end - 1;

                // There is a tag after, we can try to strip the end of the section
                while (true)
                {
                    // Reach beginning of section?
                    if (index == start)
                    {
                        end = start;
                        break;
                    }

                    var c = segment.Value[index];

                    if (c == '\n')
                    {
                        // Beginning of line, we can strip
                        end = index + 1;
                        break;
                    }

                    if (!Char.IsWhiteSpace(c))
                    {
                        // This is not just whitespace
                        break;
                    }

                    index--;
                }
            }

            if (start > 2 && segment.Value[start - 2] == '%')
            {
                index = start;

                // There is a tag before, we can try to strip the beginning of the section
                while (true)
                {
                    // Reach end of section?
                    if (index == end)
                    {
                        start = end;
                        break;
                    }

                    var c = segment.Value[index];

                    if (c == '\n' && index + 1 <= end)
                    {
                        // End of line, we can strip
                        start = index + 1;
                        break;
                    }

                    if (c == '\r' && index + 2 < end && segment.Value[index + 1] == '\n')
                    {
                        start = index + 2;
                        break;
                    }

                    if (!Char.IsWhiteSpace(c))
                    {
                        // This is not just whitespace
                        break;
                    }

                    index++;
                }
            }

            // Did the text get completely removed?
            if (end == start)
            {
                return null;
            }

            return new TextStatement(segment.Substring(start, end - start));
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

                    if ((c == '{' && !(_isComment || _isRaw)) || c == '%')
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

        public Statement BuildTagStatement(ParseTreeNode node)
        {
            var tag = node.ChildNodes[0];

            switch (tag.Term.Name)
            {
                case "for":
                    EnterBlock(tag);
                    break;

                case "endfor":
                    return BuildForStatement();

                case "case":
                    EnterBlock(tag);
                    break;

                case "when":
                    BuildWhenStatement(tag);
                    break;

                case "endcase":
                    return BuildCaseStatement();

                case "if":
                    EnterBlock(tag);
                    break;

                case "unless":
                    EnterBlock(tag);
                    break;

                case "endif":
                    return BuildIfStatement();

                case "endunless":
                    return BuildUnlessStatement();

                case "else":
                    _currentContext.EnterBlock("else", new ElseStatement(new List<Statement>()));
                    break;

                case "elsif":
                    _currentContext.EnterBlock("elsif", new ElseIfStatement(BuildExpression(tag.ChildNodes[0]), new List<Statement>()));
                    break;

                case "break":
                    return new BreakStatement();

                case "continue":
                    return new ContinueStatement();

                case "comment":
                    _isComment = true;
                    break;

                case "endcomment":
                    _isComment = false;
                    break;

                case "raw":
                    _isRaw = true;
                    break;

                case "endraw":
                    _isRaw = false;
                    break;

                case "cycle":
                    return BuildCycleStatement(tag);

                case "assign":
                    return BuildAssignStatement(tag);

                case "capture":
                    EnterBlock(tag);
                    break;

                case "endcapture":
                    return BuildCaptureStatement();

                default:
                    throw new ParseException("Unknown tag type: " + node.Term.Name);
            }

            return null;
        }

        private CaptureStatement BuildCaptureStatement()
        {
            if (_currentContext.Tag.Term.Name != "capture")
            {
                throw new ParseException($"Unexpected tag: endcapture not matching {_currentContext.Tag.Term.Name} tag.");
            }

            var identifier = _currentContext.Tag.ChildNodes[0].Token.ValueString;
            
            var captureStatement = new CaptureStatement(identifier, _currentContext.Statements);

            ExitBlock();

            return captureStatement;
        }

        private AssignStatement BuildAssignStatement(ParseTreeNode tag)
        {
            var identifier = tag.ChildNodes[0].Token.ValueString;
            var value = BuildExpression(tag.ChildNodes[1]);

            return new AssignStatement(identifier, value);
        }

        private CycleStatement BuildCycleStatement(ParseTreeNode tag)
        {
            Expression group = null;
            IList<Expression> values;

            if (tag.ChildNodes[0].Term.Name == "filterArguments")
            {
                // No group name
                values = tag.ChildNodes[0].ChildNodes.Select(BuildFilter).ToArray();
            }
            else
            {
                group = BuildFilter(tag.ChildNodes[0]);
                values = tag.ChildNodes[1].ChildNodes.Select(BuildFilter).ToArray();
            }

            return new CycleStatement(group, values);
        }

        private void BuildWhenStatement(ParseTreeNode tag)
        {
            var options = tag.ChildNodes[0].ChildNodes[0].ChildNodes.Select(BuildLiteral).ToList();
            _currentContext.EnterBlock("when", new WhenStatement(options, new List<Statement>()));
        }

        /// <summary>
        /// Invoked when a block is entered to assign subsequent
        /// statements to it.
        /// </summary>
        private void EnterBlock(ParseTreeNode tag)
        {
            _contexts.Push(_currentContext);
            _currentContext = new BlockContext(tag);
        }

        private void ExitBlock()
        {
            _currentContext = _contexts.Pop();
        }

        #region Build methods

        public OutputStatement BuildOutputStatement(ParseTreeNode node)
        {
            var expressionNode = node.ChildNodes[0];
            var filterListNode = node.ChildNodes[1];

            var expression = BuildExpression(expressionNode);

            var filters = filterListNode.ChildNodes.Select(BuildFilterExpression).ToArray();

            return new OutputStatement(expression, filters);
        }

        private IfStatement BuildIfStatement()
        {
            if (_currentContext.Tag.Term.Name != "if")
            {
                throw new ParseException($"Unexpected tag: endif not matching {_currentContext.Tag.Term.Name} tag.");
            }

            var elseStatements = _currentContext.GetBlockStatements<ElseStatement>("else");
            var elseIfStatements = _currentContext.GetBlockStatements<ElseIfStatement>("elsif");

            var ifStatement = new IfStatement(
                BuildExpression(_currentContext.Tag.ChildNodes[0]),
                _currentContext.Statements,
                elseStatements.FirstOrDefault(),
                elseIfStatements
                );

            ExitBlock();

            return ifStatement;
        }

        private CaseStatement BuildCaseStatement()
        {
            if (_currentContext.Tag.Term.Name != "case")
            {
                throw new ParseException($"Unexpected tag: endcase not matching {_currentContext.Tag.Term.Name} tag.");
            }

            if (_currentContext.Statements.Any())
            {
                throw new ParseException($"Unexpected content in 'case' tag. Only 'when' and 'else' are allowed.");
            }

            var elseStatements = _currentContext.GetBlockStatements<ElseStatement>("else");
            var whenStatements = _currentContext.GetBlockStatements<WhenStatement>("when");

            var caseStatement = new CaseStatement(
                BuildExpression(_currentContext.Tag.ChildNodes[0]),
                elseStatements.FirstOrDefault(),
                whenStatements
                );

            ExitBlock();

            return caseStatement;
        }

        private UnlessStatement BuildUnlessStatement()
        {
            if (_currentContext.Tag.Term.Name != "unless")
            {
                throw new ParseException($"Unexpected tag: endunless not matching {_currentContext.Tag.Term.Name} tag.");
            }

            var elseStatements = _currentContext.GetBlockStatements<ElseStatement>("else");
            var elseIfStatements = _currentContext.GetBlockStatements<ElseIfStatement>("elsif");

            if (elseStatements.Count > 0)
            {
                throw new ParseException($"Unexpected tag 'else' in 'unless'.");
            }

            if (elseIfStatements.Count > 0)
            {
                throw new ParseException($"Unexpected tag 'elsif' in 'unless'.");
            }

            var unlessStatement = new UnlessStatement(
                BuildExpression(_currentContext.Tag.ChildNodes[0]),
                _currentContext.Statements
                );

            ExitBlock();

            return unlessStatement;
        }

        private Statement BuildForStatement()
        {
            if (_currentContext.Tag.Term.Name != "for")
            {
                throw new ParseException($"Unexpected tag: endfor not matching {_currentContext.Tag.Term.Name} tag.");
            }

            var identifier = _currentContext.Tag.ChildNodes[0].Token.Text;
            var source = _currentContext.Tag.ChildNodes[1];

            LiteralExpression limit = null;
            LiteralExpression offset = null;
            var reversed = false;

            // Options?
            if (_currentContext.Tag.ChildNodes.Count > 2)
            {
                foreach(var option in _currentContext.Tag.ChildNodes[2].ChildNodes)
                {
                    switch (option.Term.Name)
                    {
                        case "limit":
                            limit = BuildLiteral(option.ChildNodes[0]);
                            break;
                        case "offset":
                            offset = BuildLiteral(option.ChildNodes[0]);
                            break;
                        case "reversed":
                            reversed = true;
                            break;
                    }
                }
            }

            ForStatement forStatement;

            switch (source.Term.Name)
            {
                case "memberAccess":
                    forStatement = new ForStatement(
                        _currentContext.Statements, 
                        identifier, 
                        BuildMemberExpression(source),
                        limit,
                        offset,
                        reversed);
                    break;

                case "range":
                    forStatement =  new ForStatement(
                        _currentContext.Statements, 
                        identifier, 
                        BuildRangeExpression(source),
                        limit,
                        offset,
                        reversed);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            ExitBlock();

            return forStatement;
        }

        private RangeExpression BuildRangeExpression(ParseTreeNode node)
        {
            Expression from = null, to = null;

            var fromNode = node.ChildNodes[0];

            from = fromNode.Term.Name == "number"
                ? (Expression)BuildLiteral(fromNode)
                : BuildMemberExpression(fromNode)
                ;

            var toNode = node.ChildNodes[1];

            to = toNode.Term.Name == "number"
                ? (Expression)BuildLiteral(toNode)
                : BuildMemberExpression(toNode)
                ;

            return new RangeExpression(from, to);
        }

        private Expression BuildExpression(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            switch (child.Term.Name)
            {
                case "memberAccess":
                    return BuildMemberExpression(child);
                case "literal":
                    return BuildLiteralExpression(child);
                case "binaryExpression":
                    return BuildBinaryExpression(child);
                default:
                    throw new ParseException("Unknown expression type: " + node.Term.Name);
            }
        }

        private Expression BuildBinaryExpression(ParseTreeNode node)
        {
            var left = BuildExpression(node.ChildNodes[0]);
            var op = node.ChildNodes[1].Term.Name;
            var right = BuildExpression(node.ChildNodes[2]);

            switch (op)
            {
                case "==": return new EqualBinaryExpression(left, right);
                case "<>": 
                case "!=": return new NotEqualBinaryExpression(left, right);
                case "+": return new AddBinaryExpression(left, right);
                case "-": return new SubstractBinaryExpression(left, right);
                case "*": return new MultiplyBinaryExpression(left, right);
                case "/": return new DivideBinaryExpression(left, right);
                case "%": return new ModuloBinaryExpression(left, right);
                case ">": return new GreaterThanBinaryExpression(left, right, true);
                case "<": return new LowerThanExpression(left, right, true);
                case ">=": return new GreaterThanBinaryExpression(left, right, false);
                case "<=": return new LowerThanExpression(left, right, false);
                case "contains": return new ContainsBinaryExpression(left, right);
                case "and": return new AndBinaryExpression(left, right);
                case "or": return new OrBinaryExpression(left, right);
            }

            throw new ParseException($"Unknown binary expression: {op}");
        }

        private MemberExpression BuildMemberExpression(ParseTreeNode node)
        {
            var identifierNode = node.ChildNodes[0];
            var segmentNodes = node.ChildNodes[1].ChildNodes;

            var segments = new MemberSegment[segmentNodes.Count + 1];
            segments[0] = new IdentifierSegment(identifierNode.Token.Text);

            for (var i = 0; i < segmentNodes.Count; i++)
            {
                var segmentNode = segmentNodes[i];
                segments[i + 1] = BuildMemberSegment(segmentNode);
            }

            return new MemberExpression(segments);
        }

        private MemberSegment BuildMemberSegment(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            switch (child.Term.Name)
            {
                case "memberAccessSegmentIdentifier":
                    return new IdentifierSegment(child.ChildNodes[0].Token.Text);
                case "memberAccessSegmentIndexer":
                    return new IndexerSegment(BuildExpression(child.ChildNodes[0]));
                default:
                    throw new ParseException("Unknown expression type: " + node.Term.Name);
            }
        }

        private LiteralExpression BuildLiteralExpression(ParseTreeNode node)
        {
            var child = node.ChildNodes[0];

            return BuildLiteral(child);
        }

        private LiteralExpression BuildLiteral(ParseTreeNode node)
        {
            switch (node.Term.Name)
            {
                case "string1":
                case "string2":
                    return new LiteralExpression(new StringValue(node.Token.ValueString));

                case "number":
                    return new LiteralExpression(new NumberValue(Convert.ToDouble(node.Token.Value)));

                case "boolean":
                    if (!bool.TryParse(node.ChildNodes[0].Token.Text, out var boolean))
                    {
                        throw new ParseException("Invalid boolean: " + node.Token.Text);
                    }
                    return new LiteralExpression(new BooleanValue(boolean));

                default:
                    throw new ParseException("Unknown literal expression: " + node.Term.Name);
            }
        }

        private FilterExpression BuildFilterExpression(ParseTreeNode node)
        {
            var identifier = node.ChildNodes[0].Token.Text;

            var arguments = node.ChildNodes.Count > 1
                ? node.ChildNodes[1].ChildNodes.Select(BuildFilter).ToArray()
                : Array.Empty<Expression>()
                ;

            return new FilterExpression(identifier, arguments);
        }

        public Expression BuildFilter(ParseTreeNode node)
        {
            if (node.Term.Name == "memberAccess")
            {
                return BuildMemberExpression(node);
            }
            else
            {
                return BuildLiteral(node);
            }
        }
        #endregion

        private class BlockContext
        {

            // Some types of sub-block can be repeated (when, case)
            // This value is initialized the first time a sub-block is found
            private Dictionary<string, IList<Statement>> _blocks;

            private IList<Statement> _statements;

            public BlockContext(ParseTreeNode tag)
            {
                Tag = tag;
                Statements = new List<Statement>();
                _statements = Statements;
            }

            public ParseTreeNode Tag { get; }
            public IList<Statement> Statements { get; }
            
            public void EnterBlock(string name, TagStatement statement)
            {
                if (_blocks == null)
                {
                    _blocks = new Dictionary<string, IList<Statement>>();
                }

                if (!_blocks.TryGetValue(name, out var blockStatements))
                {
                    _blocks.Add(name, blockStatements = new List<Statement>());
                }

                blockStatements.Add(statement);
                _statements = statement.Statements;
            }

            public void AddStatement(Statement statement)
            {
                _statements.Add(statement);
            }

            public IList<TStatement> GetBlockStatements<TStatement>(string name)
            {
                if (_blocks != null && _blocks.TryGetValue(name, out var statements))
                {
                    return statements.Cast<TStatement>().ToList();
                }

                return Array.Empty<TStatement>();
            }
        }
    }
}


