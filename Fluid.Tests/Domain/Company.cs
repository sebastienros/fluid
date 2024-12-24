using System.Collections.Generic;

namespace Fluid.Tests.Domain
{
    public class Company 
    {
        public Employee Director { get; set; }

        public List<Employee> Employees { get; set; } = [];
    }
}
