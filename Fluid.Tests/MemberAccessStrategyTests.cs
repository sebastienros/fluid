using Fluid.Accessors;
using Fluid.Tests.Domain;
using Fluid.Values;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class MemberAccessStrategyTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void RegisterByTypeAddPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
        }

        [Fact]
        public void RegisterByTypeAddPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeUsesAotSafePropertyAccessorWhenDynamicCodeIsUnavailable()
        {
            var strategy = new DefaultMemberAccessStrategy();
            strategy.Register<Class1>();
            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1));

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                Assert.IsType<PropertyInfoAccessor>(accessor, exactMatch: false);
            }
            else
            {
                Assert.Equal("ReflectionPropertyInfoAccessor", accessor.GetType().Name);
            }
        }

        [Fact]
        public void RegisterByTypeUsesAotSafeFieldAccessorWhenDynamicCodeIsUnavailable()
        {
            var strategy = new DefaultMemberAccessStrategy();
            strategy.Register<Class1>();
            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1));

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                Assert.IsType<DelegateAccessor>(accessor, exactMatch: false);
            }
            else
            {
                Assert.Equal("ReflectionFieldInfoAccessor", accessor.GetType().Name);
            }
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Field3));
            Assert.NotNull(accessor);
            Assert.IsAssignableFrom<AsyncDelegateAccessor>(accessor);
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Property3));
            Assert.NotNull(accessor);
            Assert.IsAssignableFrom<AsyncDelegateAccessor>(accessor);
        }

        [Fact]
        public void RegisterByTypeIgnoresStaticMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticProperty)));
        }

        [Fact]
        public void RegisterByTypeIgnoresPrivateMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeAndName()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>(nameof(Class1.Field1), nameof(Class1.Property1));

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
        }

        [Fact]
        public void RegisterByTypeAndExpression()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>(x => x.Field1, x => x.Property1);

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
        }

        [Fact]
        public async Task ShouldResolvePropertiesWithDots()
        {
            var obj = new JObject(
                new JProperty("a", "1"),
                new JProperty("a.b", "2")
            );

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var objectValue = FluidValue.Create(obj, options);

            Assert.Equal("1", (await objectValue.GetValueAsync("a", context)).ToObjectValue());
            Assert.Equal("2", (await objectValue.GetValueAsync("a.b", context)).ToObjectValue());
            Assert.Null((await objectValue.GetValueAsync("a.c", context)).ToObjectValue());
        }

        [Fact]
        public async Task ShouldNotBreakJObjectCustomizations()
        {
            var options = new TemplateOptions();

            // When a property of a JObject value is accessed, try to look into its properties
            options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);

            // Convert JToken to FluidValue
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue v ? v.Value : null);

            var model = JObject.Parse("{\"Name\": \"Bill\",\"Company\":{\"Name\":\"Microsoft\"}}");

            _parser.TryParse("His name is {{ Name }}, Company : {{ Company.Name }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill, Company : Microsoft", await template.RenderAsync(context));
        }

        [Fact]
        public async Task ShouldRenderReadmeSample()
        {
            var options = new TemplateOptions();

            options.MemberAccessStrategy.Register<Person, object>((p, name) => p.Firstname);
            var model = new Person { Firstname = "Bill" };

            _parser.TryParse("His name is {{ Something }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill", await template.RenderAsync(context));
        }

        [Fact]
        public async Task ShouldAccessJObject()
        {
            var options = new TemplateOptions();

            var model = JObject.Parse("{\"Name\": \"Bill\",\"Company\":{\"Name\":\"Microsoft\"}}");

            _parser.TryParse("His name is {{ Name }}, Company : {{ Company | json }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill, Company : {\"Name\":\"Microsoft\"}", await template.RenderAsync(context));
        }

        [Fact]
        public void SubPropertyShouldNotBeAccessible()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = _parser.Parse("{{Firstname}};{{Lastname}};{{Address.City}};{{Address.State}}");
            Assert.Equal("John;;;", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void SiblingPropertyShouldNotBeAccessible()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);
            // Address is not registered
            options.MemberAccessStrategy.Register<Address>(x => x.State);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = _parser.Parse("{{Firstname}};{{Lastname}};{{Address.City}};{{Address.State}}");
            Assert.Equal("John;;;", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void ShouldResolveModelProperty()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = _parser.Parse("{{Firstname}}{{Lastname}}");
            Assert.Equal("John", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void ShouldSkipWriteOnlyProperty()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.WriteOnlyProperty)));
        }

        [Fact]
        public void ShouldUseDictionaryAsModel()
        {
            var options = new TemplateOptions();

            var model = new Dictionary<string, object>();
            model.Add("Firstname", "Bill");
            model.Add("Lastname", "Gates");

            var template = _parser.Parse("{{Firstname}} {{Lastname}}");
            
            Assert.Equal("Bill Gates", template.Render(new TemplateContext(model, options)));
        }

        [Fact]
        public void ShouldResolveEnums()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>();

            var john = new Person { Firstname = "John", EyesColor = Colors.Yellow };

            var template = _parser.Parse("{{Firstname}} {{EyesColor}}");
            Assert.Equal("John Yellow", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void ShouldResolveStructs()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Shape>();
            options.MemberAccessStrategy.Register<Point>();

            var circle = new Shape
            {
                Coordinates = new Point(1, 2)
            };

            var template = _parser.Parse("{{Coordinates.X}} {{Coordinates.Y}}");
            Assert.Equal("1 2", template.Render(new TemplateContext(circle, options, false)));
        }

        [Fact]
        public void ShouldFindBackingFields()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<CustomStruct>();

            var s = new CustomStruct
            {
                X1 = 1,
                X2 = 2,
                X3 = 3
            };

            var template = _parser.Parse("{{X1}} {{X2}} {{X3}}");
            Assert.Equal("1 2 3", template.Render(new TemplateContext(s, options, false)));
        }
        [Fact]
        public void ShouldApplyGeneratedMemberAccessorsFromTemplateOptionsSubclass()
        {
            var options = new GeneratedTemplateOptions();
            var model = new GeneratedModel();

            var template = _parser.Parse("{{Generated}}");

            Assert.Equal("generated", template.Render(new TemplateContext(model, options)));
        }

        [Fact]
        public void ShouldReapplyGeneratedMemberAccessorsWhenStrategyIsReplaced()
        {
            var options = new GeneratedTemplateOptions
            {
                MemberAccessStrategy = new DefaultMemberAccessStrategy()
            };
            var model = new GeneratedModel();

            var template = _parser.Parse("{{Generated}}");

            Assert.Equal("generated", template.Render(new TemplateContext(model, options)));
        }

        [Fact]
        public void ShouldAllowRuntimeRegistrationsToOverrideGeneratedMemberAccessors()
        {
            var options = new GeneratedTemplateOptions();
            options.MemberAccessStrategy.Register(
                typeof(GeneratedModel),
                [new KeyValuePair<string, IMemberAccessor>(nameof(GeneratedModel.Generated), new FixedMemberAccessor("runtime"))]);
            var model = new GeneratedModel();

            var template = _parser.Parse("{{Generated}}");

            Assert.Equal("runtime", template.Render(new TemplateContext(model, options)));
        }

        [Fact]
        public void ShouldInferDirectAccessorsWithoutAllowListingTheModel()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(new InferredContextModel(), options);

            Assert.Null(options.MemberAccessStrategy.GetAccessor(typeof(InferredContextModel), "Value"));
            Assert.Equal("Fluid.SourceGenerated", GetModelAccessor(options.MemberAccessStrategy,
                typeof(InferredContextModel), "Value").GetType().Namespace);
            Assert.Equal("value;42;async;", _parser.Parse("{{Value}};{{Count}};{{Loaded}};{{Method}}").Render(context));
            context.AllowModelMembers = false;
            Assert.Equal("", _parser.Parse("{{Value}}").Render(context));
        }

        [Fact]
        public void InferredAccessorsShouldPreserveAllowListsAndNestedMembers()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<InferredContextModel>("Count");
            var context = new TemplateContext(new InferredContextModel(), options, false);
            Assert.Equal(";42;", _parser.Parse("{{Value}};{{Count}};{{Child.Value}}").Render(context));
            context.AllowModelMembers = true;
            Assert.Equal("value;42;", _parser.Parse("{{Value}};{{Count}};{{Child.Value}}").Render(context));
        }

        [Fact]
        public void InferredAccessorsShouldPreserveExplicitAndWildcardPrecedence()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(new InferredContextModel(), options);
            options.MemberAccessStrategy.Register<InferredContextModel, object>("Value", (_, _) => "explicit");
            Assert.Equal("explicit", _parser.Parse("{{Value}}").Render(context));
            options.MemberAccessStrategy.Register<InferredContextModel, object>((_, _) => "wildcard");
            Assert.Equal("explicit;wildcard", _parser.Parse("{{Value}};{{Count}}").Render(context));

            var inheritedOptions = new TemplateOptions();
            inheritedOptions.MemberAccessStrategy.Register<InferredContextBase, object>((_, _) => "base");
            Assert.Equal("base", _parser.Parse("{{Value}}").Render(new TemplateContext(new InferredContextModel(), inheritedOptions)));
        }

        [Fact]
        public void InferredAccessorsShouldPreserveNamingAndCasing()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(new InferredContextModel(), options);
            Assert.Equal("value;", _parser.Parse("{{Value}};{{value}}").Render(context));
            options.MemberAccessStrategy.IgnoreCasing = true;
            Assert.Equal("value;", _parser.Parse("{{Value}};{{value}}").Render(context));
            options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            Assert.Equal(";value", _parser.Parse("{{Value}};{{value}}").Render(context));
            options.MemberAccessStrategy.MemberNameStrategy = member => "custom_" + member.Name;
            Assert.Equal("value", _parser.Parse("{{custom_Value}}").Render(context));
        }

        [Fact]
        public void InferredAccessorsShouldPreserveConvertersAndAsyncFallback()
        {
            var options = new TemplateOptions();
            options.ValueConverters.Add(value => value is int ? "converted" : null);
            var context = new TemplateContext(new InferredContextModel(), options);
            Assert.Equal("converted", _parser.Parse("{{Count}}").Render(context));
            Assert.Equal("Fluid.Accessors", GetModelAccessor(options.MemberAccessStrategy,
                typeof(InferredContextModel), "PlainTask").GetType().Namespace);
            Assert.Equal("Fluid.Accessors", GetModelAccessor(options.MemberAccessStrategy,
                typeof(InferredContextModel), "ValueTask").GetType().Namespace);
        }

        [Fact]
        public void ShouldNotActivateInferredAccessorsOnDefaultOrCustomStrategies()
        {
            var defaultContext = new TemplateContext(new InferredContextModel(), TemplateOptions.Default);
            Assert.Equal("Fluid.Accessors", GetModelAccessor(TemplateOptions.Default.MemberAccessStrategy,
                typeof(InferredContextModel), "Value").GetType().Namespace);
            Assert.Equal("value", _parser.Parse("{{Value}}").Render(defaultContext));

            var options = new TemplateOptions { MemberAccessStrategy = new CustomContextStrategy() };
            Assert.Equal("custom", _parser.Parse("{{Value}}").Render(new TemplateContext(new InferredContextModel(), options)));
            var derivedOptions = new TemplateOptions { MemberAccessStrategy = new DerivedContextStrategy() };
            _ = new TemplateContext(new InferredContextModel(), derivedOptions);
            Assert.Equal("Fluid.Accessors", GetModelAccessor(derivedOptions.MemberAccessStrategy,
                typeof(InferredContextModel), "Value").GetType().Namespace);
        }

        [Fact]
        public void ShouldRequireMatchingRuntimeTypeAndKeepOptionInstancesIndependent()
        {
            InferredContextBase model = new UninferredContextModel();
            var options = new TemplateOptions();
            _ = new TemplateContext(model, options);
            Assert.Equal("Fluid.Accessors", GetModelAccessor(options.MemberAccessStrategy,
                typeof(UninferredContextModel), "Value").GetType().Namespace);
            var unrelatedOptions = new TemplateOptions();
            Assert.Equal("Fluid.Accessors", GetModelAccessor(unrelatedOptions.MemberAccessStrategy,
                typeof(InferredContextModel), "Value").GetType().Namespace);
        }

        [Fact]
        public void InferredAccessorsShouldPreserveInterfaceRegistrationFallback()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<IInferredContextModel, object>((_, _) => "interface");
            Assert.Equal("interface", _parser.Parse("{{Value}}").Render(new TemplateContext(new InferredContextModel(), options)));
        }

        [Fact]
        public void InferredAccessorsShouldMatchReflectedInheritedMemberSelection()
        {
            var model = new InferredInheritedModel();
            var generated = new TemplateContext(model, new TemplateOptions());
            var reflected = new TemplateContext(model, new TemplateOptions { MemberAccessStrategy = new DerivedContextStrategy() });
            var template = _parser.Parse("{{Value}};{{Hidden}};{{Different}};{{Field}}");
            Assert.Equal("field;;base;base", template.Render(reflected));
            Assert.Equal(template.Render(reflected), template.Render(generated));
        }

        [Fact]
        public void ShouldActivateLateGeneratedRegistrationsWithoutChangingAllowLists()
        {
            var options = new TemplateOptions();
            object model = new LateRegisteredContextModel();
            var firstContext = new TemplateContext(model, options);
            Assert.Equal("model", _parser.Parse("{{Value}}").Render(firstContext));

            var names = new[] { "Value" };
            DefaultMemberAccessStrategy.RegisterSourceGeneratedAccessor(
                typeof(LateRegisteredContextModel), new FixedMemberAccessor("late"), names);
            names[0] = "Other";
            var secondContext = new TemplateContext(model, options);
            Assert.Equal("late", _parser.Parse("{{Value}}").Render(secondContext));
            Assert.Equal("late", _parser.Parse("{{Value}}").Render(firstContext));
            Assert.Null(options.MemberAccessStrategy.GetAccessor(typeof(LateRegisteredContextModel), "Value"));
            secondContext.AllowModelMembers = false;
            Assert.Equal("", _parser.Parse("{{Value}}").Render(secondContext));
        }

        [Fact]
        public void ShouldPreserveGeneratedProfilesAndOptionsRegistrations()
        {
            var template = _parser.Parse("{{Value}};{{Loaded}};{{Method}}");
            var options = new TemplateOptions();
            ExplicitContextProfile.Apply(options);
            Assert.Equal("profile;loaded;method", template.Render(new TemplateContext(new ProfileContextModel(), options, false)));
            Assert.Equal("profile;loaded;method", template.Render(new TemplateContext(new ProfileContextModel(), new ProfileContextOptions(), false)));
            var unrelatedOptions = new TemplateOptions();
            Assert.Equal(";;", template.Render(new TemplateContext(new ProfileContextModel(), unrelatedOptions, false)));
        }

        private static IMemberAccessor GetModelAccessor(MemberAccessStrategy strategy, Type type, string name)
            => (IMemberAccessor)typeof(MemberAccessStrategy)
                .GetMethod("GetModelAccessor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(strategy, [type, name]);
    }

    public class InferredContextBase
    {
    }

    public interface IInferredContextModel
    {
        string Value { get; }
    }

    public sealed class InferredContextModel : InferredContextBase, IInferredContextModel
    {
        public string Value => "value";
        public int Count = 42;
        public Task<string> Loaded => Task.FromResult("async");
        public Task PlainTask => Task.CompletedTask;
        public ValueTask<int> ValueTask => new(42);
        public InferredContextChild Child => new();
        public string Method() => "method";
    }

    public class InferredInheritedBase
    {
        public Task<string> Value => Task.FromResult("property");
        public string Hidden => "base";
        public object Different => "base";
        public string Field = "base";
    }

    public sealed class InferredInheritedModel : InferredInheritedBase
    {
        public new string Value = "field";
        private new string Hidden => "private";
        public new string Different => "derived";
        public new string Field = "derived";
    }

    public sealed class UninferredContextModel : InferredContextBase
    {
        public string Value => "uninferred";
    }

    public sealed class LateRegisteredContextModel
    {
        public string Value => "model";
    }

    public sealed class ProfileContextModel
    {
        public string Value => "profile";
        public Task<string> Loaded => Task.FromResult("loaded");
        public string Method() => "method";
    }

    public static partial class ExplicitContextProfile
    {
        [FluidRegister(typeof(ProfileContextModel))]
        public static partial void Apply(TemplateOptions options);
    }

    [FluidRegister(typeof(ProfileContextModel))]
    public partial class ProfileContextOptions : TemplateOptions
    {
    }

    public sealed class InferredContextChild
    {
        public string Value => "child";
    }

    public sealed class DerivedContextStrategy : DefaultMemberAccessStrategy
    {
    }

    public sealed class CustomContextStrategy : MemberAccessStrategy
    {
        public override IMemberAccessor GetAccessor(Type type, string name) => new FixedMemberAccessor("custom");
        public override void Register(Type type, IEnumerable<KeyValuePair<string, IMemberAccessor>> accessors)
        {
        }
    }

    public class Class1
    {
        public static string StaticField;
        public static string StaticProperty { get; set; }
        internal string PrivateField = null;
        internal string PrivateProperty { get; set; }
        public string Field1;
        public int Field2;
        public Task<string> Field3;
        public string Property1 { get; set; }
        public int Property2 { get; set; }
        public Task<string> Property3 { get; set; }
        public string WriteOnlyProperty { private get; set; }
    }

    public sealed class GeneratedModel
    {
        public string Generated { get; set; } = "model";
    }

    public sealed class GeneratedTemplateOptions : TemplateOptions, ITemplateOptionsMemberAccessorRegistrar
    {
        void ITemplateOptionsMemberAccessorRegistrar.RegisterMemberAccessors(TemplateOptions options)
        {
            options.MemberAccessStrategy.Register(
                typeof(GeneratedModel),
                [new KeyValuePair<string, IMemberAccessor>("*", new FixedMemberAccessor("generated"))]);
        }
    }

    public sealed class FixedMemberAccessor : IMemberAccessor
    {
        private readonly string _value;

        public FixedMemberAccessor(string value)
        {
            _value = value;
        }

        public object Get(object obj, string name, TemplateContext ctx) => _value;
    }
}
