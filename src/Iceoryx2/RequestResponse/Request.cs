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
using static Iceoryx2.Native.Iox2NativeMethods;

using Iceoryx2.ErrorHandling;

namespace Iceoryx2.RequestResponse;

/// <summary>
/// Represents a received request from a client in a request-response communication pattern.
/// Provides access to the request payload and methods to send responses back to the client.
/// </summary>
/// <typeparam name="TRequest">The type of the request payload.</typeparam>
/// <typeparam name="TResponse">The type of the response payload.</typeparam>
public sealed class Request<TRequest, TResponse> : IDisposable
    where TRequest : unmanaged
    where TResponse : unmanaged
{
    private IntPtr _handle;
    private bool _disposed;

    internal Request(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the number of elements in the received request payload.
    /// </summary>
    public int Length
    {
        get
        {
            ThrowIfDisposed();

            iox2_active_request_payload(ref _handle, out _, out var numberOfElements);
            return (int)numberOfElements;
        }
    }

    /// <summary>
    /// Gets the request payload data.
    /// For single-element requests only (Length == 1); slice requests use <see cref="PayloadAsReadOnlySpan"/>.
    /// </summary>
    public unsafe TRequest Payload
    {
        get
        {
            ThrowIfDisposed();

            iox2_active_request_payload(ref _handle, out var payloadPtr, out _);

            if (payloadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get request payload");
            }

            return *(TRequest*)payloadPtr;
        }
    }

    /// <summary>
    /// Gets the payload as a read-only Span over shared memory, spanning all
    /// <see cref="Length"/> elements the client sent.
    /// </summary>
    public unsafe ReadOnlySpan<TRequest> PayloadAsReadOnlySpan
    {
        get
        {
            ThrowIfDisposed();

            iox2_active_request_payload(ref _handle, out var payloadPtr, out var numberOfElements);

            if (payloadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get request payload");
            }

            return new ReadOnlySpan<TRequest>(payloadPtr.ToPointer(), (int)numberOfElements);
        }
    }

    /// <summary>
    /// Loans a response message to send back to the client.
    /// </summary>
    /// <returns>A Result containing the response message or an error.</returns>
    public Result<ResponseMut<TResponse>, Iox2Error> LoanResponse()
    {
        return LoanResponseSlice(1);
    }

    /// <summary>
    /// Loans a response slice of <paramref name="numberOfElements"/> elements to send back
    /// to the client.
    /// Requires the service to be opened with <c>EnableDynamicPayloads()</c> or
    /// <c>EnableDynamicResponsePayloads()</c>, and the element count to stay within the
    /// service's <c>InitialMaxResponseSliceLen</c>.
    /// </summary>
    /// <param name="numberOfElements">The number of elements to allocate in the slice.</param>
    /// <returns>A Result containing the response message or an error.</returns>
    public Result<ResponseMut<TResponse>, Iox2Error> LoanResponseSlice(ulong numberOfElements)
    {
        ThrowIfDisposed();

        if (numberOfElements == 0)
            throw new ArgumentException("Number of elements must be greater than 0", nameof(numberOfElements));

        var result = iox2_active_request_loan_slice_uninit(
            ref _handle,
            IntPtr.Zero,
            out var responseHandle,
            new UIntPtr(numberOfElements));

        if (result != IOX2_OK)
        {
            return Result<ResponseMut<TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.ResponseLoanFailed, result, iox2_loan_error_string));
        }

        return Result<ResponseMut<TResponse>, Iox2Error>.Ok(
            new ResponseMut<TResponse>(responseHandle, (int)numberOfElements));
    }

    /// <summary>
    /// Sends a response by copying the provided data.
    /// This is a convenience method that loans, writes, and sends in one operation.
    /// </summary>
    /// <param name="response">The response data to send.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public unsafe Result<Unit, Iox2Error> SendCopyResponse(TResponse response)
    {
        ThrowIfDisposed();

        return SendCopyResponseElements(&response, 1);
    }

    /// <summary>
    /// Sends a response slice by copying the provided elements.
    /// This is a convenience method that loans, writes, and sends in one operation, with
    /// the same service and slice-length requirements as <see cref="LoanResponseSlice"/>.
    /// </summary>
    /// <param name="response">The response elements to send.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public unsafe Result<Unit, Iox2Error> SendCopyResponse(ReadOnlySpan<TResponse> response)
    {
        ThrowIfDisposed();

        if (response.IsEmpty)
            throw new ArgumentException("Response slice must contain at least one element", nameof(response));

        fixed (TResponse* dataPtr = response)
        {
            return SendCopyResponseElements(dataPtr, response.Length);
        }
    }

    private unsafe Result<Unit, Iox2Error> SendCopyResponseElements(TResponse* dataPtr, int numberOfElements)
    {
        // sizeof, not Marshal.SizeOf: the service registered the unmanaged size and the
        // client reads that layout back.
        var result = iox2_active_request_send_copy(
            ref _handle,
            new IntPtr(dataPtr),
            new UIntPtr((uint)sizeof(TResponse)),
            new UIntPtr((uint)numberOfElements));

        if (result != IOX2_OK)
        {
            return Result<Unit, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.ResponseSendFailed, result, iox2_send_error_string));
        }

        return Result<Unit, Iox2Error>.Ok(new Unit());
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Request<TRequest, TResponse>));
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Request{TRequest, TResponse}"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                iox2_active_request_drop(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}