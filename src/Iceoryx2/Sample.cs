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

using Iceoryx2.SafeHandles;
using System;
using System.Runtime.InteropServices;

using Iceoryx2.ErrorHandling;

namespace Iceoryx2;

/// <summary>
/// Represents a data sample that can be sent or received.
/// </summary>
public sealed class Sample<T> : IDisposable where T : unmanaged
{
    private SafeSampleHandle _handle;
    private bool _disposed;
    private readonly int _numberOfElements;

    internal Sample(SafeSampleHandle handle, int numberOfElements = 1)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        _numberOfElements = numberOfElements;
    }

    /// <summary>
    /// Gets the number of elements in this sample.
    /// For single-element samples (loaned via Loan()), this is 1.
    /// For slice samples (loaned via LoanSlice()), this is the number of elements in the slice.
    /// </summary>
    public int Length => _numberOfElements;

    /// <summary>
    /// Gets or sets the payload data.
    /// For single-element samples only (Length == 1).
    /// For slice samples, use PayloadAsSpan instead.
    /// </summary>
    public unsafe T Payload
    {
        get
        {
            ThrowIfDisposed();
            var sampleHandle = _handle.DangerousGetHandle();
            IntPtr payloadPtr;

            if (_handle.IsMutable)
            {
                Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
                    ref sampleHandle,
                    out payloadPtr,
                    IntPtr.Zero);
            }
            else
            {
                Native.Iox2NativeMethods.iox2_sample_payload(
                    ref sampleHandle,
                    out payloadPtr,
                    out _);
            }

            if (payloadPtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get sample payload");

            return Marshal.PtrToStructure<T>(payloadPtr);
        }
        set
        {
            ThrowIfDisposed();

            var sampleHandle = _handle.DangerousGetHandle();
            IntPtr payloadPtr;
            unsafe
            {
                // WORKAROUND: Pass NULL for number_of_elements because native code has a bug
                // where it accesses .local union variant even when service_type is IPC
                Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
                    ref sampleHandle,  // _ref type needs ref to pass pointer-to-pointer
                    out payloadPtr,
                    IntPtr.Zero);  // NULL - don't query element count due to native bug
            }

            if (payloadPtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get sample payload");

            // Write the unmanaged representation the service registered and the span
            // accessors read. Marshalled layout can differ (bool is 1 byte here, 4 when
            // marshalled) and would disagree with the registered payload size.
            *(T*)payloadPtr = value;
        }
    }

    /// <summary>
    /// Gets a mutable reference to the payload data in shared memory.
    /// This allows zero-copy access to the data.
    /// Throws InvalidOperationException if the sample is read-only (received from a subscriber).
    /// </summary>
    public unsafe ref T GetPayloadRef()
    {
        ThrowIfDisposed();

        if (!_handle.IsMutable)
            throw new InvalidOperationException("Cannot get mutable reference to read-only sample. Use GetPayloadRefReadOnly() instead.");

        var sampleHandle = _handle.DangerousGetHandle();
        IntPtr payloadPtr;

        Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
            ref sampleHandle,
            out payloadPtr,
            IntPtr.Zero);

        if (payloadPtr == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get sample payload");

        return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(payloadPtr.ToPointer());
    }

    /// <summary>
    /// Gets a read-only reference to the payload data in shared memory.
    /// This allows zero-copy access to the data.
    /// Works for both loaned samples (mutable) and received samples (read-only).
    /// </summary>
    public unsafe ref readonly T GetPayloadRefReadOnly()
    {
        ThrowIfDisposed();

        var sampleHandle = _handle.DangerousGetHandle();
        IntPtr payloadPtr;

        if (_handle.IsMutable)
        {
            Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
                ref sampleHandle,
                out payloadPtr,
                IntPtr.Zero);
        }
        else
        {
            Native.Iox2NativeMethods.iox2_sample_payload(
                ref sampleHandle,
                out payloadPtr,
                out _);
        }

        if (payloadPtr == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get sample payload");

        return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(payloadPtr.ToPointer());
    }

    /// <summary>
    /// Gets the payload as a Span for zero-copy access to slice data.
    /// This works for both single-element samples (Length == 1) and slice samples (Length > 1).
    /// For mutable samples (loaned from publisher), the span is writable.
    /// For read-only samples (received by subscriber), use PayloadAsReadOnlySpan instead.
    /// </summary>
    public unsafe Span<T> PayloadAsSpan
    {
        get
        {
            ThrowIfDisposed();

            if (!_handle.IsMutable)
                throw new InvalidOperationException("Cannot get mutable span for read-only sample. Use PayloadAsReadOnlySpan instead.");

            var sampleHandle = _handle.DangerousGetHandle();
            IntPtr payloadPtr;

            Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
                ref sampleHandle,
                out payloadPtr,
                IntPtr.Zero);

            if (payloadPtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get sample payload");

            return new Span<T>(payloadPtr.ToPointer(), _numberOfElements);
        }
    }

    /// <summary>
    /// Gets the payload as a read-only Span for zero-copy access to slice data.
    /// This works for both single-element samples (Length == 1) and slice samples (Length > 1).
    /// Works for both loaned samples (mutable) and received samples (read-only).
    /// </summary>
    public unsafe ReadOnlySpan<T> PayloadAsReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();

            var sampleHandle = _handle.DangerousGetHandle();
            IntPtr payloadPtr;

            if (_handle.IsMutable)
            {
                Native.Iox2NativeMethods.iox2_sample_mut_payload_mut_ptr(
                    ref sampleHandle,
                    out payloadPtr,
                    IntPtr.Zero);
            }
            else
            {
                Native.Iox2NativeMethods.iox2_sample_payload(
                    ref sampleHandle,
                    out payloadPtr,
                    out _);
            }

            if (payloadPtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get sample payload");

            return new ReadOnlySpan<T>(payloadPtr.ToPointer(), _numberOfElements);
        }
    }

    /// <summary>
    /// Sends the sample to all connected subscribers.
    /// </summary>
    public Result<Unit, Iox2Error> Send()
    {
        ThrowIfDisposed();

        try
        {
            var sampleHandle = _handle.DangerousGetHandle();

            // Native frees the sample struct before it attempts delivery, so the handle is
            // spent on the failure path too. Relinquish it before inspecting the result.
            _handle.SetHandleAsInvalid();
            _disposed = true;

            var result = Native.Iox2NativeMethods.iox2_sample_mut_send(
                sampleHandle,
                IntPtr.Zero);

            if (result != Native.Iox2NativeMethods.IOX2_OK)
                return Result<Unit, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.SendFailed, result, Native.Iox2NativeMethods.iox2_send_error_string));

            return Result<Unit, Iox2Error>.Ok(Unit.Value);
        }
        catch (Exception e)
        {
            return Result<Unit, Iox2Error>.Err(Iox2Error.FromException(Iox2ErrorKind.SendFailed, e));
        }
    }

    /// <summary>
    /// Releases the resources associated with the current instance of the Sample class.
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
            throw new ObjectDisposedException(nameof(Sample<T>));
    }
}