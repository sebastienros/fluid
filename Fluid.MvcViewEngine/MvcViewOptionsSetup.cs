using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    /// <summary>
    /// This class is registered automatically to register the Fluid View Engine into ASP.NET MVC
    /// </summary>
    internal class MvcViewOptionsSetup : IConfigureOptions<MvcViewOptions>
    {
        private readonly IFluidViewEngine _fluidViewEngine;

        /// <summary>
        /// Initializes a new instance of <see cref="MvcViewOptionsSetup"/>.
        /// </summary>
        /// <param name="fluidViewEngine">The <see cref="IFluidViewEngine"/>.</param>
        public MvcViewOptionsSetup(IFluidViewEngine fluidViewEngine)
        {
            ArgumentNullException.ThrowIfNull(fluidViewEngine);
            
            _fluidViewEngine = fluidViewEngine;
        }

        /// <summary>
        /// Configures <paramref name="options"/> to use <see cref="FluidViewEngine"/>.
        /// </summary>
        /// <param name="options">The <see cref="MvcViewOptions"/> to configure.</param>
        public void Configure(MvcViewOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.ViewEngines.Add(_fluidViewEngine);
        }

    }
}
