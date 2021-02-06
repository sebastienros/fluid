using Fluid.Values;
using System;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Tests.Domain.WithInterfaces
{
    public class PetValue : FluidValue
    {
        private readonly IPet pet;

        public override FluidValues Type { get; } = FluidValues.Object;

        public PetValue(IPet pet)
        {
            this.pet = pet;
        }

        public override bool Equals(FluidValue other)
        {
            throw new NotImplementedException();
        }

        public override bool ToBooleanValue()
        {
            throw new NotImplementedException();
        }

        public override decimal ToNumberValue()
        {
            throw new NotImplementedException();
        }

        public override object ToObjectValue()
        {
            throw new NotImplementedException();
        }

        public override string ToStringValue()
        {
            throw new NotImplementedException();
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            throw new NotImplementedException();
        }

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            if (name == "Name")
            {
                return Create(pet.Name, context.Options);
            }

            return NilValue.Instance;
        }
    }
}
