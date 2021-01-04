using Parlot;
using Parlot.Fluent;
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

        public TextSpanStatement(TextSpan text)
        {
            Text = text;
        }

        public TextSpanStatement(string text)
        {
            Text = new TextSpan(text);
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

        public TextSpan Text { get; private set; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Prevent this instance from mutating concurrently

            if (!_isStripped)
            {
                lock (_synLock)
                {
                    if (!_isStripped)
                    {
                        var span = Text.Buffer;
                        var start = 0;
                        var end = Text.Length - 1;

                        if (StrippedLeft)
                        {
                            for (var i = start; i <= end; i++)
                            {
                                var c = span[Text.Offset + i];

                                if (Character.IsWhiteSpaceOrNewLine(c))
                                {
                                    start++;

                                    // Read the first CR/LF or LF and stop skipping
                                    if (c == '\r')
                                    {
                                        if (i + 1 <= end && span[Text.Offset + i + 1] == '\n')
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
                                var c = span[Text.Offset + i];

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

                        if (end - start + 1 == 0)
                        {
                            _isEmpty = true;
                        }
                        else if (start != 0 || end != Text.Length - 1)
                        {
                            var offset = Text.Offset;
                            var buffer = Text.Buffer;

                            Text = new TextSpan(buffer, offset + start, end - start + 1);
                        }
                    }
                }

                _isStripped = true;
            }

            if (_isEmpty)
            {
                return Normal;
            }

            context.IncrementSteps();

            // The Text fragments are not encoded, but kept as-is
            writer.Write(Text.Span);
            writer.Flush();

            return Normal;
        }
    }
}
