using System;
using System.IO;
using System.Reflection;

namespace Fluid.SourceGenerator
{
    internal static class EmbeddedDependencyResolver
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            // Roslyn loads analyzers into a dedicated AssemblyLoadContext.
            // netstandard2.0 can't reference System.Runtime.Loader directly, so hook it via reflection.
            TryHookAssemblyLoadContextResolving();

            // Fallback for environments that still honor it.
            AppDomain.CurrentDomain.AssemblyResolve += static (_, args) => ResolveFromName(args.Name);
        }

        private static void TryHookAssemblyLoadContextResolving()
        {
            try
            {
                var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
                if (alcType == null)
                {
                    return;
                }

                var getLoadContext = alcType.GetMethod("GetLoadContext", BindingFlags.Public | BindingFlags.Static);
                if (getLoadContext == null)
                {
                    return;
                }

                var alc = getLoadContext.Invoke(null, new object[] { typeof(EmbeddedDependencyResolver).Assembly });
                if (alc == null)
                {
                    return;
                }

                var resolvingEvent = alcType.GetEvent("Resolving", BindingFlags.Public | BindingFlags.Instance);
                if (resolvingEvent == null)
                {
                    return;
                }

                var handlerType = resolvingEvent.EventHandlerType;
                if (handlerType == null)
                {
                    return;
                }

                var handlerMethod = typeof(EmbeddedDependencyResolver).GetMethod(
                    nameof(OnResolving),
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (handlerMethod == null)
                {
                    return;
                }

                var handler = Delegate.CreateDelegate(handlerType, handlerMethod);
                resolvingEvent.AddEventHandler(alc, handler);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static Assembly? OnResolving(object context, AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!TryReadEmbeddedDependency(name, out var bytes))
            {
                return null;
            }

            // Prefer loading into the provided AssemblyLoadContext (via reflection).
            try
            {
                var loadFromStream = context?.GetType().GetMethod("LoadFromStream", new[] { typeof(Stream) });
                if (loadFromStream != null)
                {
                    using var ms = new MemoryStream(bytes, writable: false);
                    var loaded = loadFromStream.Invoke(context, new object[] { ms }) as Assembly;
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to default load.
            }

            return Assembly.Load(bytes);
        }

        private static Assembly? ResolveFromName(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return null;
            }

            var name = new AssemblyName(assemblyName).Name;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!TryReadEmbeddedDependency(name, out var bytes))
            {
                return null;
            }

            return Assembly.Load(bytes);
        }

        private static bool TryReadEmbeddedDependency(string name, out byte[] bytes)
        {
            // Dependencies are embedded as: Fluid.SourceGenerator.Dependencies.<Name>.dll
            var resourceName = "Fluid.SourceGenerator.Dependencies." + name + ".dll";

            var thisAssembly = typeof(EmbeddedDependencyResolver).Assembly;
            using var stream = thisAssembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            bytes = ms.ToArray();
            return bytes.Length != 0;
        }
    }
}
