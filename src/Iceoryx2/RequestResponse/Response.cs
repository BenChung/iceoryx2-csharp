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
using static Iceoryx2.Native.Iox2NativeMethods;

namespace Iceoryx2.RequestResponse;

/// <summary>
/// Represents a received response from a server.
/// </summary>
/// <typeparam name="TResponse">The type of the response payload.</typeparam>
public sealed class Response<TResponse> : IDisposable
    where TResponse : unmanaged
{
    private IntPtr _handle;
    private bool _disposed;

    internal Response(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the number of elements in the received response payload.
    /// </summary>
    public int Length
    {
        get
        {
            ThrowIfDisposed();

            iox2_response_payload(ref _handle, out _, out var numberOfElements);
            return (int)numberOfElements;
        }
    }

    /// <summary>
    /// Gets the response payload data.
    /// For single-element responses only (Length == 1); slice responses use <see cref="PayloadAsReadOnlySpan"/>.
    /// </summary>
    public unsafe TResponse Payload
    {
        get
        {
            ThrowIfDisposed();

            iox2_response_payload(ref _handle, out var payloadPtr, out _);

            if (payloadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get response payload");
            }

            return *(TResponse*)payloadPtr;
        }
    }

    /// <summary>
    /// Gets the payload as a read-only Span over shared memory, spanning all
    /// <see cref="Length"/> elements the server sent.
    /// </summary>
    public unsafe ReadOnlySpan<TResponse> PayloadAsReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();

            iox2_response_payload(ref _handle, out var payloadPtr, out var numberOfElements);

            if (payloadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get response payload");
            }

            return new ReadOnlySpan<TResponse>(payloadPtr.ToPointer(), (int)numberOfElements);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Response<TResponse>));
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Response{TResponse}"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                iox2_response_drop(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}