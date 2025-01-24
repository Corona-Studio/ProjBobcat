using System.Runtime.InteropServices;

namespace ProjBobcat.Class.Model;

public record JavaRuntimeInfo(
    string JavaPath,
    OSPlatform JavaPlatform,
    Architecture JavaArch,
    bool UseSystemGlfwOnLinux,
    bool UseSystemOpenAlOnLinux);