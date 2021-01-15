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
        private readonly object _synLock = new object();
        private TextSpan _text;

        public TextSpanStatement(in TextSpan text)
        {
            _text = text;
        }

        public TextSpanStatement(string text)
        {
            _text = new TextSpan(text);
        }

        public bool StrippedLeft { get; set; }
        public bool StrippedRight { get; set; }

        public void StripRight()
        {
            StrippedRight = true;
        }

        public void StripLeft()
        {
            StrippedLeft = true;
        }

        public ref readonly TextSpan Text => ref _text;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (!_isStripped)
            {
                var span = _text.Buffer;
                var start = 0;
                var end = _text.Length - 1;

                if (StrippedLeft)
                {
                    for (var i = start; i <= end; i++)
                    {
                        var c = span[_text.Offset + i];

                        if (Character.IsWhiteSpaceOrNewLine(c))
                        {
                            start++;

                            // Read the first CR/LF or LF and stop skipping
                            if (c == '\r')
                            {
                                if (i + 1 <= end && span[_text.Offset + i + 1] == '\n')
                                {
                                    start++;
                                    break;
                                }
                            }
                            else
                            {
                                if (c == '\n')
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (StrippedRight)
                {
                    for (var i = end; i >= start; i--)
                    {
                        var c = span[_text.Offset + i];

                        if (Character.IsWhiteSpace(c))
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
                return Normal;
            }

            context.IncrementSteps();

            // The Text fragments are not encoded, but kept as-is
            writer.Write(_text.Span);

            return Normal;
        }
    }
}
