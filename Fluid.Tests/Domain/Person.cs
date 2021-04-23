namespace Fluid.Tests.Domain
{
    public class Person
    {
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public Address Address { get; set; }
    }

    public class Address
    {
        public string City { get; set; }
        public string State { get; set; }
    }
}
