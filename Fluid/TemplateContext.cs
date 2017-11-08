using System;
using System.Collections.Generic;
using System.Globalization;
using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.FileProviders;

namespace Fluid
{
    public class TemplateContext
    {
        protected Scope _scope;
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
        public static IMemberAccessStrategy GlobalMemberAccessStrategy = new MemberAccessStrategy();

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
                .WithMiscFilters();
        }

        public TemplateContext()
        {
            _scope = new Scope(GlobalScope);
            LocalScope = _scope;
        }

        public Scope EnterChildScope()
        {
            return LocalScope = LocalScope.EnterChildScope();
        }

        public void ReleaseScope()
        {
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

        public void SetValue(string name, FluidValue value)
        {
            LocalScope.SetValue(name, value);
        }

        public void SetValue(string name, int value)
        {
            SetValue(name, new NumberValue(value));
        }

        public void SetValue(string name, string value)
        {
            SetValue(name, new StringValue(value));
        }

        public void SetValue(string name, bool value)
        {
            SetValue(name, new BooleanValue(value));
        }

        public void SetValue(string name, object value)
        {
            SetValue(name, FluidValue.Create(value));
        }
    }
}
