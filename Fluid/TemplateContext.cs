using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Fluid.Filters;
using Fluid.Values;

namespace Fluid
{
    public class TemplateOptions
    {
        public static readonly TemplateOptions Default = new TemplateOptions();

        /// <summary>
        /// Gets ot sets the members than can be accessed in a template.
        /// </summary>
        public MemberAccessStrategy MemberAccessStrategy { get; set; } = new DefaultMemberAccessStrategy();

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider"/> used to access files.
        /// </summary>
        public IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of steps a script can execute. Leave to 0 for unlimited.
        /// </summary>
        public int MaxSteps { get; set; } = 0;

        /// <summary>
        /// Gets or sets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
        /// </summary>
        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Gets or sets the way to return the current date and time for the template.
        /// </summary>
        public Func<DateTimeOffset> Now { get; set; } = static () => DateTimeOffset.Now;

        /// <summary>
        /// Gets or sets the maximum depth of recursions a script can execute. 100 by default.
        /// </summary>
        public int MaxRecursion { get; set; } = 100;

        /// <summary>
        /// Gets the collection of filters available in the templates.
        /// </summary>
        public FilterCollection Filters { get; } = new FilterCollection();

        public TemplateOptions()
        {
            Filters.WithArrayFilters()
                .WithStringFilters()
                .WithNumberFilters()
                .WithMiscFilters();
        }
    }

    public class TemplateContext
    {
        protected int _recursion = 0;
        protected int _steps = 0;

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateContext"/>.
        /// </summary>
        public TemplateContext() : this(TemplateOptions.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateContext"/> with the specified <see cref="TemplateOptions"/>.
        /// </summary>
        /// <param name="options"></param>
        public TemplateContext(TemplateOptions options)
        {
            Options = options;

            LocalScope = new Scope();

            LocalScope.SetValue("empty", NilValue.Empty);
            LocalScope.SetValue("blank", StringValue.Empty);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateContext"/> wih a model and option regiter its properties.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="registerModelProperties">Whether to register the model properties or not.</param>
        public TemplateContext(object model, bool registerModelProperties = true) : this()
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>
        /// Gets the <see cref="TemplateOptions"/>.
        /// </summary>
        public TemplateOptions Options { get; protected set; }

        internal void IncrementSteps()
        {
            if (Options.MaxSteps != 0 && _steps++ > Options.MaxSteps)
            {
                throw new InvalidOperationException("The maximum number of statements has been reached. Your script took too long to run.");
            }
        }

        public Scope LocalScope { get; protected set; }

        /// <summary>
        /// Used to define custom object on this instance to be used in filters and statements
        /// but which are not available from the template.
        /// </summary>
        public Dictionary<string, object> AmbientValues { get; protected set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets a model object that is used to resolve properties in a template. This object is used if local and
        /// global scopes are unsuccessfull.
        /// </summary>
        public object Model { get; set; }

        /// <summary>
        /// Creates a new isolated scope. After than any value added to this content object will be released once
        /// <see cref="ReleaseScope" /> is called. The previous scope is linked such that its values are still available.
        /// </summary>
        public void EnterChildScope()
        {
            if (Options.MaxRecursion > 0 && _recursion++ > Options.MaxRecursion)
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
    }

    public static class TemplateContextExtensions
    {
        public static TemplateContext SetValue(this TemplateContext context, string name, int value)
        {
            return context.SetValue(name, NumberValue.Create(value));
        }

        public static TemplateContext SetValue(this TemplateContext context, string name, string value)
        {
            return context.SetValue(name, new StringValue(value));
        }

        public static TemplateContext SetValue(this TemplateContext context, string name, char value)
        {
            return context.SetValue(name, StringValue.Create(value));
        }

        public static TemplateContext SetValue(this TemplateContext context, string name, bool value)
        {
            return context.SetValue(name, BooleanValue.Create(value));
        }

        public static TemplateContext SetValue(this TemplateContext context, string name, object value)
        {
            return context.SetValue(name, FluidValue.Create(value));
        }

        public static TemplateContext SetValue<T>(this TemplateContext context, string name, Func<T> factory)
        {
            return context.SetValue(name, new FactoryValue<T>(factory));
        }
    }
}
