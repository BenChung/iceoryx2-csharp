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

using Iceoryx2.ErrorHandling;

namespace Iceoryx2.RequestResponse;

/// <summary>
/// Represents a mutable response message that can be written to and sent back to a client.
/// </summary>
/// <typeparam name="TResponse">The type of the response payload.</typeparam>
public sealed class ResponseMut<TResponse> : IDisposable
    where TResponse : unmanaged
{
    private IntPtr _handle;
    private bool _disposed;
    private readonly int _numberOfElements;

    internal ResponseMut(IntPtr handle, int numberOfElements = 1)
    {
        _handle = handle;
        _numberOfElements = numberOfElements;
    }

    /// <summary>
    /// Gets the number of elements in this response, as passed to the loan that produced it.
    /// </summary>
    public int Length => _numberOfElements;

    /// <summary>
    /// Gets or sets the response payload data.
    /// For single-element responses only (Length == 1); slice responses use <see cref="PayloadAsSpan"/>.
    /// </summary>
    public unsafe TResponse Payload
    {
        get
        {
            ThrowIfDisposed();

            return *(TResponse*)PayloadPtr();
        }
        set
        {
            ThrowIfDisposed();

            *(TResponse*)PayloadPtr() = value;
        }
    }

    /// <summary>
    /// Gets the payload as a writable Span over shared memory, spanning all
    /// <see cref="Length"/> elements.
    /// </summary>
    public unsafe Span<TResponse> PayloadAsSpan
    {
        get
        {
            ThrowIfDisposed();

            return new Span<TResponse>(PayloadPtr().ToPointer(), _numberOfElements);
        }
    }

    /// <summary>
    /// Gets the payload as a read-only Span over shared memory, spanning all
    /// <see cref="Length"/> elements.
    /// </summary>
    public unsafe ReadOnlySpan<TResponse> PayloadAsReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();

            return new ReadOnlySpan<TResponse>(PayloadPtr().ToPointer(), _numberOfElements);
        }
    }

    private IntPtr PayloadPtr()
    {
        iox2_response_mut_payload_mut(ref _handle, out var payloadPtr, out _);

        if (payloadPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get response payload");
        }

        return payloadPtr;
    }

    /// <summary>
    /// Sends the response back to the client.
    /// After calling this method, the ResponseMut is consumed and should not be used again.
    /// </summary>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, Iox2Error> Send()
    {
        ThrowIfDisposed();

        var result = iox2_response_mut_send(_handle);

        if (result != IOX2_OK)
        {
            return Result<Unit, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.ResponseSendFailed, result, iox2_send_error_string));
        }

        // Mark as disposed since the handle is consumed by send
        _disposed = true;
        _handle = IntPtr.Zero;

        return Result<Unit, Iox2Error>.Ok(new Unit());
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ResponseMut<TResponse>));
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="ResponseMut{TResponse}"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                iox2_response_mut_drop(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}