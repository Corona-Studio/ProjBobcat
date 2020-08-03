using System;
using System.Runtime.InteropServices;
using System.Security;

[SecurityCritical]
[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
    [DllImport("User32.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowText(IntPtr winHandle, string title);
}