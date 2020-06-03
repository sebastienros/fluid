using System;
using System.Collections.Generic;
using System.Text;

namespace Fluid.Tests.Domain.WithInterfaces
{
    public class Dog : Animal, IDog
    {
        public string Name { get; set; }
    }
}
