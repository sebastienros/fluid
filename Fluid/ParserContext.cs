using System.Collections.Generic;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid
{
    public class ParserContext
    {
        private Stack<BlockContext> _blocks { get; } = new Stack<BlockContext>();

        public BlockContext CurrentBlock { get; private set; } = new BlockContext(null);

        /// <summary>
        /// Invoked when a block is entered to create a new statements context
        /// which will received all subsequent statements.
        /// </summary>
        public void EnterBlock(ParseTreeNode tag)
        {
            _blocks.Push(CurrentBlock);
            CurrentBlock = new BlockContext(tag);
        }

        /// <summary>
        /// Invoked when a section is entered to create a new statements context
        /// which will received all subsequent statements.
        /// </summary>
        public void EnterBlockSection(string name, TagStatement statement)
        {
            CurrentBlock.EnterBlock(name, statement);
        }

        /// <summary>
        /// Invoked when the end of a block has been reached.
        /// It resets the current statements context to the outer block.
        /// </summary>
        public void ExitBlock()
        {
            CurrentBlock = _blocks.Pop();
        }
        /// <summary>
        /// Invoked at the end of parsing to ensure we closed all blocks
        /// </summary>
        public bool AreInBlock()
        {
            return(_blocks.Count > 0);
        }
    }
}