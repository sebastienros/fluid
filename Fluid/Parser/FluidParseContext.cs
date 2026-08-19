using Fluid.Ast;
using Parlot;
using Parlot.Fluent;

namespace Fluid.Parser
{
    public class FluidParseContext : ParseContext
    {
        private const int InlineIdentifierCapacity = 8;

        private string _identifier0, _identifier1, _identifier2, _identifier3;
        private string _identifier4, _identifier5, _identifier6, _identifier7;
        private int _identifierCount;
        private Dictionary<string, string> _identifiers;

        public FluidParseContext(string text) : base(new Scanner(text))
        {
        }

        // Keep identifier references local to this parse so repeated scope names can use reference equality.
        internal string CanonicalizeIdentifier(string identifier)
        {
            if (_identifiers is not null)
            {
                if (_identifiers.TryGetValue(identifier, out var canonical))
                {
                    return canonical;
                }

                _identifiers.Add(identifier, identifier);
                return identifier;
            }

            for (var i = 0; i < _identifierCount; i++)
            {
                var canonical = GetIdentifier(i);

                if (string.Equals(identifier, canonical, StringComparison.Ordinal))
                {
                    return canonical;
                }
            }

            if (_identifierCount < InlineIdentifierCapacity)
            {
                SetIdentifier(_identifierCount++, identifier);
                return identifier;
            }

            _identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < InlineIdentifierCapacity; i++)
            {
                var canonical = GetIdentifier(i);
                _identifiers.Add(canonical, canonical);
            }

            _identifiers.Add(identifier, identifier);
            return identifier;
        }

        private string GetIdentifier(int index) => index switch
        {
            0 => _identifier0,
            1 => _identifier1,
            2 => _identifier2,
            3 => _identifier3,
            4 => _identifier4,
            5 => _identifier5,
            6 => _identifier6,
            _ => _identifier7,
        };

        private void SetIdentifier(int index, string identifier)
        {
            switch (index)
            {
                case 0: _identifier0 = identifier; break;
                case 1: _identifier1 = identifier; break;
                case 2: _identifier2 = identifier; break;
                case 3: _identifier3 = identifier; break;
                case 4: _identifier4 = identifier; break;
                case 5: _identifier5 = identifier; break;
                case 6: _identifier6 = identifier; break;
                default: _identifier7 = identifier; break;
            }
        }

        public string PreviousRenderTag { get; set; }
        public TextSpanStatement PreviousTextSpanStatement { get; set; }
        public bool StripNextTextSpanStatement { get; set; }
        public bool PreviousIsTag { get; set; }
        public bool PreviousIsOutput { get; set; }
        public int LiquidTagDepth { get; set; } // Used in the {% liquid %} tag to ensure a new line corresponds to '%}'
    }
}
