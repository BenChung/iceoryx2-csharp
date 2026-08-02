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

using Iceoryx2.Native;
using Interop = Iceoryx2.Native.Interop;
using Iceoryx2.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Iceoryx2;

/// <summary>
/// Represents the result of a node wait operation.
/// </summary>
public enum NodeWaitResult
{
    /// <summary>
    /// The wait completed successfully after the specified cycle time.
    /// </summary>
    Ok,

    /// <summary>
    /// The wait was interrupted by a signal.
    /// </summary>
    Interrupt,

    /// <summary>
    /// A termination request was received.
    /// </summary>
    TerminationRequest
}

/// <summary>
/// Represents a node in the Iceoryx2 system.
/// The node serves as a central entry point and is linked to a specific process within the Iceoryx2 ecosystem.
/// It provides capabilities for creating or opening services and managing node-specific resources.
/// </summary>
public sealed class Node : IDisposable
{
    internal SafeNodeHandle _handle;
    internal Iox2NativeMethods.iox2_service_type_e _serviceType;
    private bool _disposed;

    internal Node(SafeNodeHandle handle, Iox2NativeMethods.iox2_service_type_e serviceType = Iox2NativeMethods.iox2_service_type_e.IPC)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _serviceType = serviceType;
    }

    private static readonly int s_idOffset = OffsetOf(nameof(Interop.iox2_static_config_t.id));
    private static readonly int s_nameOffset = OffsetOf(nameof(Interop.iox2_static_config_t.name));
    private static readonly int s_messagingPatternOffset =
        OffsetOf(nameof(Interop.iox2_static_config_t.messaging_pattern));

    private static int OffsetOf(string field) =>
        Marshal.OffsetOf<Interop.iox2_static_config_t>(field).ToInt32();

    /// <summary>
    /// Collects the discovery callback's results plus any failure it hit. An exception
    /// must not unwind into the Rust caller, so the callback records it here and
    /// <see cref="List"/> reports it rather than returning a silently truncated list.
    /// </summary>
    private sealed class ServiceListContext
    {
        public readonly List<ServiceStaticConfig> Services = new();
        public Exception? Failure;
    }

    private static Iox2NativeMethods.iox2_callback_progression_e ServiceListCallback(IntPtr configPtr, IntPtr context)
    {
        ServiceListContext? ctx = null;
        try
        {
            ctx = CallbackContext.Peek<ServiceListContext>(context);
            if (ctx is null || configPtr == IntPtr.Zero)
            {
                return Iox2NativeMethods.iox2_callback_progression_e.STOP;
            }

            // Field offsets come from the generated interop struct, so padding between
            // fields is the CLR's problem rather than hand arithmetic against the header.
            byte[] id = new byte[Interop.Iox2Constants.IOX2_SERVICE_HASH_LENGTH];
            byte[] name = new byte[Interop.Iox2Constants.IOX2_SERVICE_NAME_LENGTH];

            Marshal.Copy(IntPtr.Add(configPtr, s_idOffset),
                id, 0, Interop.Iox2Constants.IOX2_SERVICE_HASH_LENGTH);

            Marshal.Copy(IntPtr.Add(configPtr, s_nameOffset),
                name, 0, Interop.Iox2Constants.IOX2_SERVICE_NAME_LENGTH);

            var messagingPattern = (Iox2NativeMethods.iox2_messaging_pattern_e)Marshal.ReadInt32(
                IntPtr.Add(configPtr, s_messagingPatternOffset));

            ctx.Services.Add(new ServiceStaticConfig(id, name, messagingPattern));
            return Iox2NativeMethods.iox2_callback_progression_e.CONTINUE;
        }
        catch (Exception ex)
        {
            // Never let this unwind into the Rust caller. Record it so List() can fail
            // loudly; a swallowed failure here reads as "no services exist".
            if (ctx != null)
                ctx.Failure ??= ex;
            return Iox2NativeMethods.iox2_callback_progression_e.STOP;
        }
    }

    /// <summary>
    /// Gets the name of the node.
    /// </summary>
    public string Name
    {
        get
        {
            ThrowIfDisposed();
            // TODO: Implement proper node name retrieval
            return "node"; // Placeholder
        }
    }

    /// <summary>
    /// Gets the unique ID of the node.
    /// </summary>
    public Guid Id
    {
        get
        {
            ThrowIfDisposed();
            // TODO: Implement proper node ID retrieval
            return Guid.NewGuid(); // Placeholder
        }
    }

    /// <summary>
    /// Creates a builder for creating or opening a service.
    /// </summary>
    public ServiceBuilder ServiceBuilder()
    {
        ThrowIfDisposed();
        return new ServiceBuilder(this);
    }

    /// <summary>
    /// Waits for the specified duration while handling system signals properly.
    /// This is the recommended way to wait in a loop instead of Thread.Sleep.
    /// </summary>
    /// <param name="cycleTime">The duration to wait.</param>
    /// <returns>A result indicating whether the wait completed successfully or was interrupted.</returns>
    public NodeWaitResult Wait(TimeSpan cycleTime)
    {
        ThrowIfDisposed();

        var seconds = (ulong)cycleTime.TotalSeconds;
        var nanoseconds = (uint)((cycleTime.TotalSeconds - seconds) * 1_000_000_000);

        var nodeHandle = _handle.DangerousGetHandle();
        var result = Iox2NativeMethods.iox2_node_wait(ref nodeHandle, seconds, nanoseconds);

        if (result == Iox2NativeMethods.IOX2_OK)
        {
            return NodeWaitResult.Ok;
        }

        var errorCode = (Iox2NativeMethods.iox2_node_wait_failure_e)result;
        return errorCode switch
        {
            Iox2NativeMethods.iox2_node_wait_failure_e.INTERRUPT => NodeWaitResult.Interrupt,
            Iox2NativeMethods.iox2_node_wait_failure_e.TERMINATION_REQUEST => NodeWaitResult.TerminationRequest,
            _ => NodeWaitResult.Interrupt
        };
    }

    /// <summary>
    /// Lists all available services in the system.
    /// </summary>
    /// <returns>A result containing a list of service static configurations or an error.</returns>
    public Result<List<ServiceStaticConfig>, ServiceListError> List()
    {
        ThrowIfDisposed();

        var listContext = new ServiceListContext();
        var contextPtr = CallbackContext.Pin(listContext);

        try
        {
            var configPtr = Iox2NativeMethods.iox2_config_global_config();

            var result = Iox2NativeMethods.iox2_service_list(
                _serviceType,
                configPtr,
                ServiceListCallback,
                contextPtr);

            if (result != Iox2NativeMethods.IOX2_OK)
            {
                var errorCode = (Iox2NativeMethods.iox2_service_list_error_e)result;
                var error = errorCode switch
                {
                    Iox2NativeMethods.iox2_service_list_error_e.INSUFFICIENT_PERMISSIONS => ServiceListError.InsufficientPermissions,
                    Iox2NativeMethods.iox2_service_list_error_e.INTERNAL_ERROR => ServiceListError.InternalError,
                    Iox2NativeMethods.iox2_service_list_error_e.INTERRUPT => ServiceListError.Interrupt,
                    _ => ServiceListError.InternalError
                };
                return Result<List<ServiceStaticConfig>, ServiceListError>.Err(error);
            }

            // The native call succeeded, but the callback stops the iteration when it
            // fails, so an unreported failure here would masquerade as an empty system.
            if (listContext.Failure != null)
                return Result<List<ServiceStaticConfig>, ServiceListError>.Err(ServiceListError.InternalError);

            return Result<List<ServiceStaticConfig>, ServiceListError>.Ok(listContext.Services);
        }
        finally
        {
            CallbackContext.Unpin<ServiceListContext>(contextPtr);
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the Node and optionally releases the managed resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _handle?.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Node));
    }
}