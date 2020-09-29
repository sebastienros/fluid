using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Fluid.Json
{
    internal class FluidJsonNamingPolicy : JsonNamingPolicy
    {
        private readonly TemplateContext _templateContext;
        public FluidJsonNamingPolicy(TemplateContext templateContext)
        {
            _templateContext = templateContext;
        }


        public override string ConvertName(string name)
        {
            var memberInfo = FakeMemberInfo.Get(name);
            return _templateContext.MemberAccessStrategy.MemberNameStrategy(memberInfo);
        }

        private class FakeMemberInfo : MemberInfo
        {
            private static readonly ConcurrentDictionary<string, MemberInfo> Cache =
                new ConcurrentDictionary<string, MemberInfo>();

            private FakeMemberInfo(string name)
            {
                Name = name;
            }

            public override Type DeclaringType => throw new NotImplementedException();
            public override MemberTypes MemberType => throw new NotImplementedException();
            public override string Name { get; }
            public override Type ReflectedType => throw new NotImplementedException();

            public static MemberInfo Get(string name)
            {
                return Cache.GetOrAdd(name, n => new FakeMemberInfo(n));
            }

            public override object[] GetCustomAttributes(bool inherit)
            {
                throw new NotImplementedException();
            }

            public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }

            public override bool IsDefined(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }
        }
    }
}