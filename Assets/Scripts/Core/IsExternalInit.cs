// Polyfill so C# 9 'init' accessors and records compile under Unity's .NET Standard 2.1 / .NET 4.8 profiles.
// Unity does not ship System.Runtime.CompilerServices.IsExternalInit; the compiler only needs the type to exist.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
