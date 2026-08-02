// Copyright (c) 2025 Contributors to the Eclipse Foundation
//
// See the NOTICE file(s) distributed with this work for additional
// information regarding copyright ownership.
//
// This program and the accompanying materials are made available under the
// terms of the Apache Software License 2.0 which is available at
// https://www.apache.org/licenses/LICENSE-2.0, or the MIT license
// which is available at https://opensource.org/licenses/MIT.
//
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Iceoryx2;

/// <summary>
/// Process-wide runtime services of the native library.
/// </summary>
/// <remarks>
/// The native library catches internal panics at every FFI entry point and
/// reports them here instead of aborting the process. After a
/// <see cref="NativePanic"/>, the library's internal state may be
/// inconsistent: dispose all iceoryx2 objects and restart in a fresh domain
/// (see <see cref="Config.WipeDomain"/>).
/// </remarks>
public static class Iox2Runtime
{
    // Rooted so the native side never calls a collected delegate.
    private static Native.Iox2NativeMethods.iox2_panic_callback? _panicCallback;
    private static int _installed;

    /// <summary>
    /// Raised with the panic message whenever the native library catches an
    /// internal panic, on the thread that panicked. Handlers must not call
    /// back into iceoryx2 or throw.
    /// </summary>
    public static event Action<string>? NativePanic;

    /// <summary>
    /// Installs the native panic reporting hook. Idempotent; called
    /// automatically by <see cref="NodeBuilder.Create"/>.
    /// </summary>
    public static void EnablePanicReporting()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
            return;

        _panicCallback = OnNativePanic;
        Native.Iox2NativeMethods.iox2_guarded_set_panic_callback(_panicCallback);
    }

    /// <summary>
    /// Returns and clears the calling thread's last caught native panic
    /// message. A failing call preceded by a panic on the same thread failed
    /// because of that panic.
    /// </summary>
    public static string? TakeLastPanic()
    {
        var buffer = new byte[4096];
        if (!Native.Iox2NativeMethods.iox2_guarded_take_last_panic(buffer, (UIntPtr)buffer.Length))
            return null;

        var length = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
    }

    private static void OnNativePanic(IntPtr message)
    {
        var text = Marshal.PtrToStringUTF8(message) ?? "native panic";
        try
        {
            NativePanic?.Invoke(text);
        }
        catch
        {
            // Never propagate exceptions into the panicking native thread.
        }
    }
}
