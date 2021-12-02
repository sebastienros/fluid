#if NETCOREAPP3_1 || NETSTANDARD
using System.ComponentModel;

// Fix for: error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}
#endif
