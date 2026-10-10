using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AstroArchive;

internal static class LinuxCodecs
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!OperatingSystem.IsLinux()) return;
        NativeLibrary.SetDllImportResolver(typeof(LinuxCodecs).Assembly, Resolve);
    }
    internal static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? search)
    {
            string[] names = name switch {
                "cfitsio.dll" => ["libcfitsio.so.10", "libcfitsio.so.9"],
                "libzstd.dll" => ["libzstd.so.1"],
                _ => []
            };
            if (names.Length == 0) return IntPtr.Zero;
            // Only distribution library directories, never a writable source or CWD.
            foreach (string directory in new[] { "/usr/lib/x86_64-linux-gnu", "/lib/x86_64-linux-gnu", "/usr/lib64", "/usr/lib" })
                foreach (string library in names)
                    if (NativeLibrary.TryLoad(Path.Combine(directory, library), out var handle)) return handle;
            throw new DllNotFoundException("Install the distribution's " + names[0] + " codec to preview this compression.");
    }
}
