using Parlot;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class TextSpanStatement : Statement
    {
        private bool _isStripped = false;
        private bool _isEmpty = false;
        private readonly object _synLock = new ();
        private TextSpan _text;

        public TextSpanStatement(in TextSpan text)
        {
            _text = text;
        }

        public TextSpanStatement(string text)
        {
            _text = new TextSpan(text);
        }

        public bool StripLeft { get; set; }
        public bool StripRight { get; set; }

        public bool NextIsTag { get; set; }
        public bool NextIsOutput { get; set; }
        public bool PreviousIsTag { get; set; }
        public bool PreviousIsOutput { get; set; }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (!_isStripped)
            {
                StripLeft |= 
                    (PreviousIsTag && context.Options.Trimming.HasFlag(TrimmingFlags.TagRight)) ||
                    (PreviousIsOutput && context.Options.Trimming.HasFlag(TrimmingFlags.OutputRight))
                    ;

                StripRight |=
                    (NextIsTag && context.Options.Trimming.HasFlag(TrimmingFlags.TagLeft)) ||
                    (NextIsOutput && context.Options.Trimming.HasFlag(TrimmingFlags.OutputLeft))
                    ;

                var span = _text.Buffer;
                var start = 0;
                var end = _text.Length - 1;

                if (StripLeft)
                {
                    for (var i = start; i <= end; i++)
                    {
                        var c = span[_text.Offset + i];

                        if (Character.IsWhiteSpaceOrNewLine(c))
                        {
                            start++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (StripRight)
                {
                    for (var i = end; i >= start; i--)
                    {
                        var c = span[_text.Offset + i];

                        if (Character.IsWhiteSpaceOrNewLine(c))
                        {
                            end--;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // update the current statement with tread-safely since this statements
                // is shared
                lock (_synLock)
                {
                    // it might have been stripped by another thread while locked
                    if (!_isStripped)
                    {
                        if (end - start + 1 == 0)
                        {
                            _isEmpty = true;
                        }
                        else if (start != 0 || end != _text.Length - 1)
                        {
                            var offset = _text.Offset;
                            var buffer = _text.Buffer;

                            _text = new TextSpan(buffer, offset + start, end - start + 1);
                        }
                    }

                    _isStripped = true;
                }                
            }

            if (_isEmpty)
            {
                return Normal();
            }

            context.IncrementSteps();

            // The Text fragments are not encoded, but kept as-is
#if NETSTANDARD2_0
            writer.Write(_text.ToString());
#else
            writer.Write(_text.Span);
#endif
            return Normal();
        }
    }
}
