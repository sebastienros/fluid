using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Fluid.Ast;
using Fluid.Guards;
using Fluid.Values;

namespace Fluid
{
    public class FilterCollection
    {
        private readonly Dictionary<string, AsyncFilterDelegate> _delegates =
            new Dictionary<string, AsyncFilterDelegate>();

        private readonly Dictionary<string, RequiresArgumentAttribute> _filterRequiresArgument =
            new Dictionary<string, RequiresArgumentAttribute>();

        public void AddFilter(string name, FilterDelegate d)
        {
            _delegates[name] = (input, arguments, context) => new ValueTask<FluidValue>(d(input, arguments, context));
            if (d.GetMethodInfo().GetCustomAttribute(typeof(RequiresArgumentAttribute), false) is
                RequiresArgumentAttribute attr)
                _filterRequiresArgument[name] = attr;
        }

        public void AddAsyncFilter(string name, AsyncFilterDelegate d)
        {
            _delegates[name] = d;
        }

        public bool TryGetValue(string name, out AsyncFilterDelegate filter)
        {
            return _delegates.TryGetValue(name, out filter);
        }

        public void GuardIfArgumentMandatory(string name, IEnumerable<FilterArgument> arguments)
        {
            _filterRequiresArgument.TryGetValue(name, out var requiresAttribute);
            if (requiresAttribute != null && !arguments.Any())
            {
                if (!string.IsNullOrEmpty(requiresAttribute.ErrorMessage))
                {
                    throw new ParseException(requiresAttribute.ErrorMessage);
                }

                throw new ParseException($"Filter {name} requires a mandatory argument which was not supplied.");
            }
        }
    }
}