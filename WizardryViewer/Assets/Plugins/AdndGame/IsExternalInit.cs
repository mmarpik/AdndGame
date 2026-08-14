// Unity-only plumbing: C# 9 records/init-only setters compile down to a reference to
// System.Runtime.CompilerServices.IsExternalInit, which .NET 5+ ships in corelib but Unity's
// Mono/IL2CPP runtime does not. This is the standard polyfill for that gap. Not synced from
// either repo - it has no upstream counterpart to drift from.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
