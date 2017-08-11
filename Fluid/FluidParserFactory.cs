using System.Collections.Generic;
using Irony.Parsing;

namespace Fluid
{
    public class FluidParserFactory : IFluidParserFactory
    {
        private readonly FluidGrammar _grammar = new FluidGrammar();
        private readonly Dictionary<string, ITag> _tags = new Dictionary<string, ITag>();
        private readonly Dictionary<string, ITag> _blocks = new Dictionary<string, ITag>();

        private LanguageData _language;

        public IFluidParser CreateParser()
        {
            if (_language == null)
            {
                lock (_grammar)
                {
                    if (_language == null)
                    {
                        _language = new LanguageData(_grammar);
                    }
                }
            }

            return new DefaultFluidParser(_language, _tags, _blocks);
        }

        public void RegisterTag<T>(string name) where T : ITag, new()
        {
            lock (_grammar)
            {
                var tag = new T();

                _language = null;
                _tags[name] = tag;

                // Configure the grammar to add support for the custom syntax

                var terminal = new NonTerminal(name);

                terminal.Rule = _grammar.ToTerm(name) + tag.GetSyntax(_grammar);
                _grammar.KnownTags.Rule |= terminal;

                // Prevent the text from being added in the parsed tree.
                _grammar.MarkPunctuation(name);
            }
        }

        public void RegisterBlock<T>(string name) where T : ITag, new()
        {
            lock (_grammar)
            {
                var tag = new T();

                _language = null;
                _blocks[name] = tag;

                // Configure the grammar to add support for the custom syntax

                var terminal = new NonTerminal(name);
                var endTerminal = _grammar.ToTerm("end" + name);

                terminal.Rule = _grammar.ToTerm(name) + tag.GetSyntax(_grammar);
                _grammar.KnownTags.Rule |= terminal | endTerminal;

                // Prevent the text from being added in the parsed tree.
                _grammar.MarkPunctuation(name);
            }
        }
    }
}


