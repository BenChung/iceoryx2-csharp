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

using Iceoryx2.ErrorHandling;
using Iceoryx2.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Iceoryx2;

/// <summary>
/// An iceoryx2 configuration. Apply it to a node with
/// <see cref="NodeBuilder.WithConfig"/>; every service created through that
/// node inherits it.
/// </summary>
/// <remarks>
/// <see cref="RootPath"/> and <see cref="Prefix"/> together form the IPC
/// namespace: processes discover each other's services only when both values
/// match.
/// </remarks>
public sealed class Config : IDisposable
{
    // Mirrors platform/iceoryx2_settings.rs: the platform layer keeps
    // shared-memory state under these compile-time paths independent of the
    // configured root path, and does not create them.
    private const string WindowsPlatformTempDirectory = @"C:\ProgramData\iceoryx2\tmp\";
    private const string WindowsPlatformShmDirectory = @"C:\ProgramData\iceoryx2\shm\";
    private const string PosixPlatformTempDirectory = "/tmp/";
    private const string PosixPlatformShmDirectory = "/dev/shm/";

    private readonly SafeConfigHandle _handle;

    private Config(SafeConfigHandle handle)
    {
        _handle = handle;
    }

    internal SafeConfigHandle Handle => _handle;

    /// <summary>
    /// Creates a config with the library default values, independent of any
    /// loaded <c>iceoryx2.toml</c>.
    /// </summary>
    public static Result<Config, Iox2Error> Default()
    {
        var result = Native.Iox2NativeMethods.iox2_config_default(IntPtr.Zero, out var handle);
        if (result != Native.Iox2NativeMethods.IOX2_OK || handle == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed, $"error code {result}"));

        return Result<Config, Iox2Error>.Ok(new Config(new SafeConfigHandle(handle)));
    }

    /// <summary>
    /// Creates a snapshot of the process-global config. The first access to
    /// the global config loads <c>iceoryx2.toml</c> from the default search
    /// locations when one is present.
    /// </summary>
    public static Result<Config, Iox2Error> FromGlobal()
    {
        var globalPtr = Native.Iox2NativeMethods.iox2_config_global_config();
        if (globalPtr == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed, "global config unavailable"));

        Native.Iox2NativeMethods.iox2_config_from_ptr(globalPtr, IntPtr.Zero, out var handle);
        if (handle == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed, "failed to clone global config"));

        return Result<Config, Iox2Error>.Ok(new Config(new SafeConfigHandle(handle)));
    }

    /// <summary>
    /// Creates a config populated from the given TOML file.
    /// </summary>
    public static Result<Config, Iox2Error> FromFile(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var result = Native.Iox2NativeMethods.iox2_config_from_file(IntPtr.Zero, out var handle, path);
        if (result != Native.Iox2NativeMethods.IOX2_OK || handle == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed,
                    $"failed to load config file '{path}' (error code {result})"));

        return Result<Config, Iox2Error>.Ok(new Config(new SafeConfigHandle(handle)));
    }

    /// <summary>
    /// Loads the given TOML file into the process-global config and returns a
    /// snapshot of it. Call this before any other iceoryx2 API; once the
    /// global config is initialized it keeps its values.
    /// </summary>
    public static Result<Config, Iox2Error> SetupGlobalFromFile(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        var result = Native.Iox2NativeMethods.iox2_config_setup_global_config_from_file(out var globalPtr, path);
        if (result != Native.Iox2NativeMethods.IOX2_OK || globalPtr == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed,
                    $"failed to load global config file '{path}' (error code {result})"));

        Native.Iox2NativeMethods.iox2_config_from_ptr(globalPtr, IntPtr.Zero, out var handle);
        if (handle == IntPtr.Zero)
            return Result<Config, Iox2Error>.Err(
                Iox2Error.FromKind(Iox2ErrorKind.ConfigCreationFailed, "failed to clone global config"));

        return Result<Config, Iox2Error>.Ok(new Config(new SafeConfigHandle(handle)));
    }

    /// <summary>
    /// Creates the config for one IPC domain: default values with
    /// <see cref="RootPath"/> set to <c>&lt;ipcRoot&gt;/&lt;domain&gt;</c> and
    /// <see cref="Prefix"/> set to <c>iox2_&lt;domain&gt;_</c>. Processes in
    /// any language that derive their config with the same formula share the
    /// domain. Creates the root directory and, on Windows, the platform
    /// shared-memory state directories, since iceoryx2 creates neither.
    /// </summary>
    /// <param name="ipcRoot">Directory under which all domains live. Every
    /// participant must use the same value.</param>
    /// <param name="domain">Domain name; isolates this deployment's services
    /// from others on the machine.</param>
    public static Result<Config, Iox2Error> ForDomain(string ipcRoot, string domain)
    {
        if (string.IsNullOrEmpty(ipcRoot))
            throw new ArgumentException("ipcRoot must be a non-empty path", nameof(ipcRoot));
        if (!System.IO.Path.IsPathRooted(ipcRoot))
            throw new ArgumentException("ipcRoot must be an absolute path", nameof(ipcRoot));
        if (string.IsNullOrEmpty(domain))
            throw new ArgumentException("domain must be a non-empty name", nameof(domain));

        var root = System.IO.Path.Combine(ipcRoot, domain);
        Directory.CreateDirectory(root);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Directory.CreateDirectory(WindowsPlatformTempDirectory);
            Directory.CreateDirectory(WindowsPlatformShmDirectory);
        }

        var result = Default();
        if (!result.IsOk)
            return result;

        var config = result.Unwrap();
        try
        {
            config.RootPath = root;
            config.Prefix = $"iox2_{domain}_";
        }
        catch
        {
            config.Dispose();
            throw;
        }
        return Result<Config, Iox2Error>.Ok(config);
    }

    /// <summary>
    /// Removes every on-disk trace of a domain: the domain directory under
    /// <paramref name="ipcRoot"/> and the domain-prefixed shared-memory marker
    /// files in the platform state directories. Call it only after all domain
    /// participants have exited or been disposed; a file still held open by a
    /// live process is skipped.
    /// </summary>
    /// <returns><c>true</c> when nothing of the domain remains on disk.</returns>
    public static bool WipeDomain(string ipcRoot, string domain)
    {
        if (string.IsNullOrEmpty(ipcRoot))
            throw new ArgumentException("ipcRoot must be a non-empty path", nameof(ipcRoot));
        if (!System.IO.Path.IsPathRooted(ipcRoot))
            throw new ArgumentException("ipcRoot must be an absolute path", nameof(ipcRoot));
        if (string.IsNullOrEmpty(domain))
            throw new ArgumentException("domain must be a non-empty name", nameof(domain));

        var clean = true;
        var root = System.IO.Path.Combine(ipcRoot, domain);
        if (Directory.Exists(root))
        {
            // Delete file by file so one locked file doesn't abort the sweep.
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { clean = false; }
            }
            try { Directory.Delete(root, recursive: true); } catch { clean = false; }
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var platformDirs = isWindows
            ? new[] { WindowsPlatformTempDirectory, WindowsPlatformShmDirectory }
            : new[] { PosixPlatformTempDirectory, PosixPlatformShmDirectory };
        var prefix = $"iox2_{domain}_";
        foreach (var dir in platformDirs)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, prefix + "*"))
            {
                try { File.Delete(file); } catch { clean = false; }
            }
        }
        return clean;
    }

    /// <summary>
    /// The path under which iceoryx2 stores all bookkeeping files (nodes,
    /// services, connection state).
    /// </summary>
    /// <exception cref="ArgumentException">The value violates iceoryx2's path
    /// rules (for example, it exceeds the maximum path length).</exception>
    public string RootPath
    {
        get
        {
            var handle = DangerousHandle();
            var ptr = Native.Iox2NativeMethods.iox2_config_global_root_path(ref handle);
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var handle = DangerousHandle();
            var result = Native.Iox2NativeMethods.iox2_config_global_set_root_path(ref handle, value);
            if (result != Native.Iox2NativeMethods.IOX2_OK)
                throw new ArgumentException(
                    $"iceoryx2 rejected the root path (semantic string error {result}): {value}", nameof(value));
        }
    }

    /// <summary>
    /// The prefix applied to every file and shared-memory name created at
    /// runtime. Shared-memory names never include <see cref="RootPath"/>, so
    /// the prefix is what keeps two deployments' segments apart.
    /// </summary>
    /// <exception cref="ArgumentException">The value violates iceoryx2's file
    /// name rules (for example, it contains a path separator).</exception>
    public string Prefix
    {
        get
        {
            var handle = DangerousHandle();
            var ptr = Native.Iox2NativeMethods.iox2_config_global_prefix(ref handle);
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var handle = DangerousHandle();
            var result = Native.Iox2NativeMethods.iox2_config_global_set_prefix(ref handle, value);
            if (result != Native.Iox2NativeMethods.IOX2_OK)
                throw new ArgumentException(
                    $"iceoryx2 rejected the prefix (semantic string error {result}): {value}", nameof(value));
        }
    }

    /// <summary>
    /// Creates an independent copy of this config.
    /// </summary>
    public Config Clone()
    {
        var handle = DangerousHandle();
        Native.Iox2NativeMethods.iox2_config_clone(ref handle, IntPtr.Zero, out var cloned);
        if (cloned == IntPtr.Zero)
            throw new InvalidOperationException("Failed to clone config.");
        return new Config(new SafeConfigHandle(cloned));
    }

    private IntPtr DangerousHandle()
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException(nameof(Config));
        return _handle.DangerousGetHandle();
    }

    /// <summary>
    /// Releases the native config. Nodes built from this config keep their
    /// own copy and stay valid.
    /// </summary>
    public void Dispose()
    {
        _handle.Dispose();
    }
}
