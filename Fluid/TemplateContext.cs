using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;
using Fluid.Values;
using Fluid.Filters;

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
        /// Represent a global list of members than can be accessed in any template.
        /// </summary>
        /// <remarks>
        /// This property should only be set by static constructores to prevent concurrency issues.
        /// </remarks>
        public static IMemberAccessStrategy GlobalMemberAccessStrategy = new MemberAccessStrategy();

        public static IFileProvider GlobalFileProvider { get; set; }

        public IMemberAccessStrategy MemberAccessStrategy = new MemberAccessStrategy(GlobalMemberAccessStrategy);

        public IFileProvider FileProvider { get; set; }

        static TemplateContext()
        {
            // Global properties
            GlobalScope.SetValue("empty", EmptyValue.Instance);

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
            return _scope.GetValue(name);
        }

        public void SetValue(string name, FluidValue value)
        {
            _scope.SetValue(name, value);
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
