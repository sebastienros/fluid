using Fluid.Accessors;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Fluid
{
    public static class MemberAccessStrategyExtensions
    {
#if NET5_0_OR_GREATER
        private const DynamicallyAccessedMemberTypes RegisteredMemberTypes = DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicMethods;
#endif

        /// <summary>
        /// Registers a named property when accessing a type using a <see cref="IMemberAccessor"/>
        /// to retrieve the value. The name of the property doesn't have to exist on the object.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property to intercept.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T>(this MemberAccessStrategy strategy, string name, IMemberAccessor getter)
#else
        public static void Register<T>(this MemberAccessStrategy strategy, string name, IMemberAccessor getter)
#endif
        {
            strategy.Register(typeof(T), name, getter);
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T>(this MemberAccessStrategy strategy, IMemberAccessor getter)
#else
        public static void Register<T>(this MemberAccessStrategy strategy, IMemberAccessor getter)
#endif
        {
            strategy.Register<T>("*", getter);
        }

        /// <summary>
        /// Registers a type using a <see cref="IMemberAccessor"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="type">The type to register.</param>
        /// <param name="getter">The <see cref="IMemberAccessor"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register(this MemberAccessStrategy strategy, [DynamicallyAccessedMembers(RegisteredMemberTypes)] Type type, IMemberAccessor getter)
#else
        public static void Register(this MemberAccessStrategy strategy, Type type, IMemberAccessor getter)
#endif
        {
            strategy.Register(type, "*", getter);
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <typeparam name="TResult">The type to return.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/> to register.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TResult}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TResult> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TResult> accessor)
#endif
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TemplateContext, TResult}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <typeparam name="TResult">The type to return.</typeparam>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, TResult> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, TResult> accessor)
#endif
        {
            strategy.Register(typeof(T), "*", new DelegateAccessor<T, TResult>(accessor));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, Task{Object}}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, Func<T, string, Task<TResult>> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, Task<TResult>> accessor)
#endif
        {
            Register<T, TResult>(strategy, (obj, name, ctx) => accessor(obj, name));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, string, TemplateContext, Task{TResult}}"/> to retrieve any of
        /// its property values.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="accessor">The <see cref="T:Func{T, string, TemplateContext, Task{TResult}}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, Task<TResult>> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, Func<T, string, TemplateContext, Task<TResult>> accessor)
#endif
        {
            strategy.Register(typeof(T), "*", new AsyncDelegateAccessor<T, TResult>(accessor));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, Task{TResult}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="T:Func{T, Task{TResult}}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, Task<TResult>> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, Task<TResult>> accessor)
#endif
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="T:Func{T, TemplateContext, Task{Object}}"/> to retrieve the given property's value.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="T:Func{T, TemplateContext, Task{Object}}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, Task<TResult>> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, Task<TResult>> accessor)
#endif
        {
            strategy.Register(typeof(T), name, new AsyncDelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx)));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TResult}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TResult}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TResult> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TResult> accessor)
#endif
        {
            Register<T, TResult>(strategy, name, (obj, ctx) => accessor(obj));
        }

        /// <summary>
        /// Registers a type with a <see cref="Func{T, TemplateContext, TResult}"/> to retrieve the property specified.
        /// </summary>
        /// <param name="strategy">The <see cref="MemberAccessStrategy"/>.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="accessor">The <see cref="Func{T, TemplateContext, TResult}"/> instance used to retrieve the value.</param>
#if NET5_0_OR_GREATER
        public static void Register<[DynamicallyAccessedMembers(RegisteredMemberTypes)] T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, TResult> accessor)
#else
        public static void Register<T, TResult>(this MemberAccessStrategy strategy, string name, Func<T, TemplateContext, TResult> accessor)
#endif
        {
            strategy.Register(typeof(T), name, new DelegateAccessor<T, TResult>((obj, propertyName, ctx) => accessor(obj, ctx)));
        }
    }
}
