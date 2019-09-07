using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Fluid.Filters;
using Fluid.Values;

namespace Fluid
{
    public class TemplateContext
    {
        internal int _recursion = 0;
        internal int _steps = 0;

        public static int DefaultMaxSteps = 0;
        public static int DefaultMaxRecursion = 100;

        /// <summary>
        /// Gets or sets the maximum number of steps a script can execute. Leave to 0 for unlimited.
        /// </summary>
        public int MaxSteps { get; set; } = DefaultMaxSteps;

        /// <summary>
        /// Gets or sets the maximum depth of recursions a script can execute.
        /// </summary>
        public int MaxRecursion { get; set; } = DefaultMaxRecursion;

        internal void IncrementSteps()
        {
            if (MaxSteps != 0 && _steps++ > MaxSteps)
            {
                throw new InvalidOperationException("The maximum number of statements has been reached. Your script took too long to run.");
            }
        }

        /// <summary>
        /// The <see cref="IFluidParserFactory"/> instance to use with this context
        /// </summary>
        public IFluidParserFactory ParserFactory { get; set; }

        /// <summary>
        /// The <see cref="IFluidTemplate"/> instance to use with this context
        /// </summary>
        public Func<IFluidTemplate> TemplateFactory { get; set; }

        // Scopes
        public static Scope GlobalScope = new Scope();

        public Scope LocalScope { get; private set; }
        
        // Filters
        public FilterCollection Filters { get; } = new FilterCollection();

        public static FilterCollection GlobalFilters { get; } = new FilterCollection();

        /// <summary>
        /// Used to define custom object on this instance to be used in filters and statements
        /// but which are not available from the template.
        /// </summary>
        public Dictionary<string, object> AmbientValues = new Dictionary<string, object>();

        // Members

        /// <summary>
        /// Represent a global list of object members than can be accessed in any template.
        /// </summary>
        /// <remarks>
        /// This property should only be set by static constructores to prevent concurrency issues.
        /// </remarks>
        public static IMemberAccessStrategy GlobalMemberAccessStrategy = new ConcurrentMemberAccessStrategy();

        public static IFileProvider GlobalFileProvider { get; set; } = new NullFileProvider();

        /// <summary>
        /// Represent a local list of object members than can be accessed with this context.
        /// </summary>
        public IMemberAccessStrategy MemberAccessStrategy = new MemberAccessStrategy(GlobalMemberAccessStrategy);

        public IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
        /// </summary>
        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Gets or sets the way to return the current date and time for the template.
        /// </summary>
        public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;

        /// <summary>
        /// Gets or sets a model object that is used to resolve properties in a template. This object is used if local and 
        /// global scopes are unsuccessfull.
        /// </summary>
        public object Model { get; set; }

        static TemplateContext()
        {
            // Global properties
            GlobalScope.SetValue("empty", NilValue.Empty);
            GlobalScope.SetValue("blank", new StringValue(""));

            // Initialize Global Filters
            GlobalFilters
                .WithArrayFilters()
                .WithStringFilters()
                .WithNumberFilters()
                .WithMiscFilters()
                .WithMoneyFilters();
        }

        public TemplateContext()
        {
            LocalScope = new Scope(GlobalScope);
        }

        /// <summary>
        /// Creates a new isolated scope. After than any value added to this content object will be released once
        /// <see cref="ReleaseScope" /> is called. The previous scope is linked such that its values are still available.
        /// </summary>
        public void EnterChildScope()
        {
            if (MaxRecursion > 0 && _recursion++ > MaxRecursion)
            {
                throw new InvalidOperationException("The maximum level of recursion has been reached. Your script must have a cyclic include statement.");
            }

            LocalScope = LocalScope.EnterChildScope();
        }

        /// <summary>
        /// Exits the current scope that has been created by <see cref="EnterChildScope" />
        /// </summary>
        public void ReleaseScope()
        {
            if (_recursion > 0)
            {
                _recursion--;
            }

            LocalScope = LocalScope.ReleaseScope();

            if (LocalScope == null)
            {
                throw new InvalidOperationException();
            }
        }

        public FluidValue GetValue(string name)
        {
            return LocalScope.GetValue(name);
        }

        public TemplateContext SetValue(string name, FluidValue value)
        {
            LocalScope.SetValue(name, value);
            return this;
        }

        public TemplateContext SetValue(string name, int value)
        {
            return SetValue(name, NumberValue.Create(value));
        }

        public TemplateContext SetValue(string name, string value)
        {
            return SetValue(name, new StringValue(value));
        }

        public TemplateContext SetValue(string name, bool value)
        {
            return SetValue(name, BooleanValue.Create(value));
        }

        public TemplateContext SetValue(string name, object value)
        {
            return SetValue(name, FluidValue.Create(value));
        }

        public TemplateContext SetValue<T>(string name, Func<T> factory)
        {
            return SetValue(name, new FactoryValue<T>(factory));
        }
    }
}
