using Fluid.Filters;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.Globalization;

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
        public IFileProvider FileProvider { get; set; } = new NullFileProvider();

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

        /// <summary>
        /// Gets a scope that is available in all the templates.
        /// </summary>
        public Scope Scope { get; } = new Scope();

        /// <summary>
        /// Gets the list of value converters.
        /// </summary>
        public List<Func<object, object>> ValueConverters { get; } = new List<Func<object, object>>();

        public TemplateOptions()
        {
            Filters.WithArrayFilters()
                .WithStringFilters()
                .WithNumberFilters()
                .WithMiscFilters();
        }
    }

}
