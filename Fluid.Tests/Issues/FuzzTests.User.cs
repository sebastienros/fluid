using System.Collections.Generic;

namespace Fluid.Tests.Issues
{
    public partial class FuzzTests
    {
        public class User
        {
            public string String { get; set; }
            public int Integer { get; set; }
            public List<double> Doubles { get; set; }
        }
    }
}