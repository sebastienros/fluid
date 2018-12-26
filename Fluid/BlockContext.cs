using System.Collections.Generic;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid
{
    public class BlockContext
    {
        // Some types of sub-block can be repeated (when, case)
        // This value is initialized the first time a sub-block is found
        private Dictionary<string, List<Statement>> _blocks;

        // Statements that are added while parsing a sub-block (else, elsif, when)
        private List<Statement> _transientStatements;

        public BlockContext(ParseTreeNode tag)
        {
            Tag = tag;
            Statements = new List<Statement>();
            _transientStatements = Statements;
        }

        public ParseTreeNode Tag { get; }

        public List<Statement> Statements { get; }

        public void EnterBlock(string name, TagStatement statement)
        {
            if (_blocks == null)
            {
                _blocks = new Dictionary<string, List<Statement>>();
            }

            if (!_blocks.TryGetValue(name, out var blockStatements))
            {
                _blocks.Add(name, blockStatements = new List<Statement>());
            }

            blockStatements.Add(statement);
            _transientStatements = statement.Statements;
        }

        public void AddStatement(Statement statement)
        {
            _transientStatements.Add(statement);
        }

        public List<TStatement> GetBlockStatements<TStatement>(string name) where TStatement : Statement
        {
            if (_blocks != null && _blocks.TryGetValue(name, out var statements))
            {
                var result = new List<TStatement>(statements.Count);
                for (int i = 0; i < statements.Count; ++i)
                {
                    result.Add((TStatement) statements[i]);
                }
                return result;
            }

            return StatementList<TStatement>.Empty;
        }

        private static class StatementList<T>
        {
            internal static readonly List<T> Empty = new List<T>();
        }
    }
}