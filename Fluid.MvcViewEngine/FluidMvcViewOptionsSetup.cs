﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    public class FluidMvcViewOptionsSetup : IConfigureOptions<MvcViewOptions>
    {
        private readonly IFluidViewEngine _fluidViewEngine;

        /// <summary>
        /// Initializes a new instance of <see cref="FluidMvcViewOptionsSetup"/>.
        /// </summary>
        /// <param name="fluidViewEngine">The <see cref="IFluidViewEngine"/>.</param>
        public FluidMvcViewOptionsSetup(IFluidViewEngine fluidViewEngine)
        {
            if (fluidViewEngine is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(fluidViewEngine));
            }
            _fluidViewEngine = fluidViewEngine;
        }

        /// <summary>
        /// Configures <paramref name="options"/> to use <see cref="FluidViewEngine"/>.
        /// </summary>
        /// <param name="options">The <see cref="MvcViewOptions"/> to configure.</param>
        public void Configure(MvcViewOptions options)
        {
            if (options is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(options));
            }

            options.ViewEngines.Add(_fluidViewEngine);
        }

    }
}
