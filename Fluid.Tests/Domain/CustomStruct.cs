namespace Fluid.Tests.Domain
{
    public struct CustomStruct
    {
        public int X1 { get; set; }

        private int _x2;

        public int X2
        {
            get { return _x2; }
            set { _x2 = value; }
        }

        private int x3;

        public int X3
        {
            get { return x3; }
            set { x3 = value; }
        }
    }
}
