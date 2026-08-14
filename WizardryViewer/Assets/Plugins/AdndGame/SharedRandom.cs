// Unity-only plumbing: Random.Shared is a .NET 6+ static property that Unity's older Mono/
// IL2CPP corelib doesn't ship (CS0117). It can't be polyfilled as an extension method since
// it's static, not instance, so the sync script text-substitutes "Random.Shared" -> the fully
// qualified name below at every call site instead. Single shared instance, not locked - this
// codebase runs its game logic single-threaded (WinForms UI thread today, Unity main thread
// here), matching how the rest of Adnd.Core/Adnd.Data isn't thread-safety-hardened either.
// Not synced from either repo.
namespace Adnd.Unity.Compat
{
    internal static class SharedRandom
    {
        internal static readonly System.Random Instance = new System.Random();
    }
}
