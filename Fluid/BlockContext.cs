using System;
using System.Collections.Generic;
using System.Linq;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid
{
    public class BlockContext
    {
        // Some types of sub-block can be repeated (when, case)
        // This value is initialized the first time a sub-block is found
        private Dictionary<string, IList<Statement>> _blocks;

        // Statements that are added while parsing a sub-block (else, elsif, when)
        private IList<Statement> transientStatements;

        public BlockContext(ParseTreeNode tag)
        {
            Tag = tag;
            Statements = new List<Statement>();
            transientStatements = Statements;
        }

        public ParseTreeNode Tag { get; }
        public IList<Statement> Statements { get; private set; }

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
            transientStatements = statement.Statements;
        }

        public void AddStatement(Statement statement)
        {
            transientStatements.Add(statement);
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


