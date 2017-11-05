using System.Collections.Generic;
using Fluid.Tags;
using Irony.Parsing;

namespace Fluid
{
    public class FluidParserFactory : IFluidParserFactory
    {
        private readonly FluidGrammar _grammar = new FluidGrammar();
        private readonly Dictionary<string, ITag> _tags = new Dictionary<string, ITag>();
        private readonly Dictionary<string, ITag> _blocks = new Dictionary<string, ITag>();

        private LanguageData _languageData;

        public IFluidParser CreateParser()
        {
            if (_languageData == null)
            {
                lock (_grammar)
                {
                    if (_languageData == null)
                    {
                        _languageData = new LanguageData(_grammar);
                    }
                }
            }

            return new DefaultFluidParser(_languageData, _tags, _blocks);
        }

        public void RegisterTag<T>(string name) where T : ITag, new()
        {
            lock (_grammar)
            {
                var tag = new T();

                _languageData = null;
                _tags[name] = tag;

                // Configure the grammar to add support for the custom syntax

                var terminal = new NonTerminal(name)
                {
                    Rule = _grammar.ToTerm(name) + tag.GetSyntax(_grammar)
                };

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

                _languageData = null;
                _blocks[name] = tag;

                // Configure the grammar to add support for the custom syntax

                var terminal = new NonTerminal(name)
                {
                    Rule = _grammar.ToTerm(name) + tag.GetSyntax(_grammar)
                };
                var endName = "end" + name;

                _grammar.KnownTags.Rule |= terminal;
                _grammar.KnownTags.Rule |= _grammar.ToTerm(endName);

                // Prevent the text from being added in the parsed tree.
                _grammar.MarkPunctuation(name);
            }
        }
    }
}


