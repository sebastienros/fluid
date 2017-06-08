using Irony;
using Irony.Parsing;

namespace Fluid
{
    public class ContentTerminal : Terminal
    {
        public ContentTerminal(string name) : base(name, TokenCategory.Content)
        {
            Priority = TerminalPriority.Low;
        }

        #region overrides
        public override void Init(GrammarData grammarData)
        {
            base.Init(grammarData);

            SetFlag(TermFlags.IsMultiline);

            if (this.EditorInfo == null)
            {
                TokenType ttype = TokenType.Comment;
                this.EditorInfo = new TokenEditorInfo(ttype, TokenColor.Comment, TokenTriggers.None);
            }
        }

        public override Token TryMatch(ParsingContext context, ISourceStream source)
        {
            Token result;
            if (context.VsLineScanState.Value != 0)
            {
                // we are continuing in line mode - restore internal env (none in this case)
                context.VsLineScanState.Value = 0;
            }
            else
            {
                //we are starting from scratch
                if (!BeginMatch(context, source)) return null;
            }
            result = CompleteMatch(context, source);
            if (result != null) return result;
            if (context.Mode == ParseMode.VsLineScan)
                return CreateIncompleteToken(context, source);
            return context.CreateErrorToken(Resources.ErrUnclosedComment);
        }

        private Token CreateIncompleteToken(ParsingContext context, ISourceStream source)
        {
            source.PreviewPosition = source.Text.Length;
            Token result = source.CreateToken(this.OutputTerminal);
            result.Flags |= TokenFlags.IsIncomplete;
            context.VsLineScanState.TerminalIndex = this.MultilineIndex;
            return result;
        }

        private bool BeginMatch(ParsingContext context, ISourceStream source)
        {
            //Check starting symbol
            if (source.MatchSymbol("{{") || source.MatchSymbol("{%"))
            {
                return false;
            }

            source.PreviewPosition += 1;
            return true;
        }

        private Token CompleteMatch(ParsingContext context, ISourceStream source)
        {
            //Find end symbol
            while (!source.EOF())
            {
                var firstCharPos = source.Text.IndexOf('{', source.PreviewPosition);
                if (firstCharPos < 0)
                {
                    source.PreviewPosition = source.Text.Length;
                    return source.CreateToken(this.OutputTerminal);
                }
                //We found a character that might start an end symbol; let's see if it is true.
                source.PreviewPosition = firstCharPos;
                if (source.MatchSymbol("{{") || source.MatchSymbol("{%"))
                {
                    return source.CreateToken(this.OutputTerminal);
                }
                source.PreviewPosition++; //move to the next char and try again    
            }

            return source.CreateToken(this.OutputTerminal);
        }

        #endregion
    }
}
