using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Fluid
{
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
        /// Initializes a new instance of <see cref="TemplateContext"/>.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="options">The template options.</param>
        /// <param name="allowModelMembers">Whether the members of the model can be accessed by default.</param>
        public TemplateContext(object model, TemplateOptions options, bool allowModelMembers = true) : this(options)
        {
            if (model == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(model));
            }

            if (model is FluidValue fluidValue)
            {
                Model = fluidValue;
            }
            else
            {
                Model = FluidValue.Create(model, options);
                AllowModelMembers = allowModelMembers;
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateContext"/> with the specified <see cref="TemplateOptions"/>.
        /// </summary>
        /// <param name="options">The template options.</param>
        public TemplateContext(TemplateOptions options)
        {
            Options = options;
            LocalScope = new Scope(options.Scope);
            RootScope = LocalScope;
            CultureInfo = options.CultureInfo;
            TimeZone = options.TimeZone;
            Captured = options.Captured;
            Now = options.Now;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateContext"/> wih a model and option regiter its properties.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="allowModelMembers">Whether the members of the model can be accessed by default.</param>
        public TemplateContext(object model, bool allowModelMembers = true) : this()
        {
            if (model == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(model));
            }

            if (model is FluidValue fluidValue)
            {
                Model = fluidValue;
            }
            else
            {
                Model = FluidValue.Create(model, TemplateOptions.Default);
                AllowModelMembers = allowModelMembers;
            }
        }

        /// <summary>
        /// Gets the <see cref="TemplateOptions"/>.
        /// </summary>
        public TemplateOptions Options { get; protected set; }

        /// <summary>
        /// Gets or sets the <see cref="CultureInfo"/> instance used to render locale values like dates and numbers.
        /// </summary>
        public CultureInfo CultureInfo { get; set; } = TemplateOptions.Default.CultureInfo;

        /// <summary>
        /// Gets or sets the value to returned by the "now" keyword.
        /// </summary>
        public Func<DateTimeOffset> Now { get; set; } = TemplateOptions.Default.Now;

        /// <summary>
        /// Gets or sets the local time zone used when parsing or creating dates without specific ones.
        /// </summary>
        public TimeZoneInfo TimeZone { get; set; } = TemplateOptions.Default.TimeZone;

        /// <summary>
        /// Increments the number of statements the current template is processing.
        /// </summary>
        public void IncrementSteps()
        {
            var maxSteps = Options.MaxSteps;
            if (maxSteps > 0 && _steps++ > maxSteps)
            {
                ExceptionHelper.ThrowMaximumRecursionException();
            }
        }

        /// <summary>
        /// Gets or sets the current scope.
        /// </summary>
        internal Scope LocalScope { get; set; }

        /// <summary>
        /// Gets or sets the root scope.
        /// </summary>
        internal Scope RootScope { get; set; }

        private Dictionary<string, object> _ambientValues;

        /// <summary>
        /// Used to define custom object on this instance to be used in filters and statements
        /// but which are not available from the template.
        /// </summary>
        public Dictionary<string, object> AmbientValues => _ambientValues ??= new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets a model object that is used to resolve properties in a template. This object is used if local and
        /// global scopes are unsuccessfull.
        /// </summary>
        public FluidValue Model { get; }

        /// <summary>
        /// Whether the direct properties of the Model can be accessed without being registered. Default is <code>true</code>.
        /// </summary>
        public bool AllowModelMembers { get; set; } = true;

        /// <summary>
        /// Gets or sets the delegate to execute when a Capture tag has been evaluated.
        /// </summary>
        public Func<string, string, ValueTask<string>> Captured { get; set; }

        /// <summary>
        /// Creates a new isolated child scope. After than any value added to this content object will be released once
        /// <see cref="ReleaseScope" /> is called. The previous scope is linked such that its values are still available.
        /// </summary>
        public void EnterChildScope()
        {
            if (Options.MaxRecursion > 0 && _recursion++ > Options.MaxRecursion)
            {
                ExceptionHelper.ThrowMaximumRecursionException();
                return;
            }

            LocalScope = new Scope(LocalScope);
        }

        /// <summary>
        /// Creates a new for loop scope. After than any value added to this content object will be released once
        /// <see cref="ReleaseScope" /> is called. The previous scope is linked such that its values are still available.
        /// </summary>
        public void EnterForLoopScope()
        {
            if (Options.MaxRecursion > 0 && _recursion++ > Options.MaxRecursion)
            {
                ExceptionHelper.ThrowMaximumRecursionException();
                return;
            }

            LocalScope = new Scope(LocalScope, forLoopScope: true);
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

            LocalScope = LocalScope.Parent;

            if (LocalScope == null)
            {
                ExceptionHelper.ThrowInvalidOperationException("ReleaseScope invoked without corresponding EnterChildScope");
                return;
            }
        }

        /// <summary>
        /// Gets the names of the values.
        /// </summary>
        public IEnumerable<string> ValueNames => LocalScope.Properties;

        /// <summary>
        /// Gets a value from the context.
        /// </summary>
        /// <param name="name">The name of the value.</param>
        public FluidValue GetValue(string name)
        {
            return LocalScope.GetValue(name);
        }

        /// <summary>
        /// Sets a value on the context.
        /// </summary>
        /// <param name="name">The name of the value.</param>
        /// <param name="value">Teh value to set.</param>
        /// <returns></returns>
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
            if (value == null)
            {
                return context.SetValue(name, NilValue.Instance);
            }

            return context.SetValue(name, FluidValue.Create(value, context.Options));
        }

        public static TemplateContext SetValue(this TemplateContext context, string name, Func<FluidValue> factory)
        {
            return context.SetValue(name, new FactoryValue(factory));
        }
    }
}
