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
/// Delegate for open or create operations.
/// </summary>
internal delegate int OpenOrCreateDelegate(IntPtr handle, IntPtr structPtr, out IntPtr outHandle);

/// <summary>
/// Builder for creating or opening request-response services.
/// </summary>
/// <typeparam name="TRequest">The type of the request payload.</typeparam>
/// <typeparam name="TResponse">The type of the response payload.</typeparam>
public sealed class RequestResponseServiceBuilder<TRequest, TResponse>
    where TRequest : unmanaged
    where TResponse : unmanaged
{
    private readonly Node _node;
    private bool _dynamicRequestPayloads;
    private bool _dynamicResponsePayloads;
    private ulong? _initialMaxRequestSliceLen;
    private ulong? _initialMaxResponseSliceLen;

    internal RequestResponseServiceBuilder(Node node)
    {
        _node = node;
    }

    /// <summary>
    /// Enables dynamic-sized payloads (slices/arrays) for both requests and responses.
    /// Clients can then use <c>LoanSlice()</c> and servers <c>LoanResponseSlice()</c>.
    /// Every participant in the service must declare the same variant, and the
    /// corresponding <c>InitialMaxSliceLen</c> bounds how many elements a port may loan.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> EnableDynamicPayloads()
    {
        _dynamicRequestPayloads = true;
        _dynamicResponsePayloads = true;
        return this;
    }

    /// <summary>
    /// Enables dynamic-sized request payloads, leaving responses fixed-size.
    /// Use for RPC where the request is variable-length and the reply is a fixed struct.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> EnableDynamicRequestPayloads()
    {
        _dynamicRequestPayloads = true;
        return this;
    }

    /// <summary>
    /// Enables dynamic-sized response payloads, leaving requests fixed-size.
    /// Use for RPC where a fixed query returns a variable-length result.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> EnableDynamicResponsePayloads()
    {
        _dynamicResponsePayloads = true;
        return this;
    }

    /// <summary>
    /// Sets the initial maximum slice length for both the client and server ports
    /// created from this service.
    /// </summary>
    /// <param name="value">Initial maximum number of elements in a slice</param>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> InitialMaxSliceLen(ulong value)
    {
        _initialMaxRequestSliceLen = value;
        _initialMaxResponseSliceLen = value;
        return this;
    }

    /// <summary>
    /// Sets the initial maximum slice length for the client ports created from this
    /// service, bounding the element count accepted by <c>Client.LoanSlice()</c>.
    /// </summary>
    /// <param name="value">Initial maximum number of elements in a request slice</param>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> InitialMaxRequestSliceLen(ulong value)
    {
        _initialMaxRequestSliceLen = value;
        return this;
    }

    /// <summary>
    /// Sets the initial maximum slice length for the server ports created from this
    /// service, bounding the element count accepted by <c>Request.LoanResponseSlice()</c>.
    /// </summary>
    /// <param name="value">Initial maximum number of elements in a response slice</param>
    /// <returns>This builder for method chaining</returns>
    public RequestResponseServiceBuilder<TRequest, TResponse> InitialMaxResponseSliceLen(ulong value)
    {
        _initialMaxResponseSliceLen = value;
        return this;
    }

    /// <summary>
    /// Opens an existing request-response service or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <returns>A Result containing the request-response service or an error.</returns>
    public Result<RequestResponseService<TRequest, TResponse>, Iox2Error> Open(string serviceName)
    {
        return OpenOrCreate(serviceName, iox2_service_builder_request_response_open_or_create);
    }

    /// <summary>
    /// Creates a new request-response service. Fails if the service already exists.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <returns>A Result containing the request-response service or an error.</returns>
    public Result<RequestResponseService<TRequest, TResponse>, Iox2Error> Create(string serviceName)
    {
        return OpenOrCreate(serviceName, iox2_service_builder_request_response_create);
    }

    private unsafe Result<RequestResponseService<TRequest, TResponse>, Iox2Error> OpenOrCreate(
        string serviceName,
        OpenOrCreateDelegate openOrCreateFunc)
    {
        // Create service name
        var serviceNameResult = iox2_service_name_new(
            IntPtr.Zero,
            serviceName,
            System.Text.Encoding.UTF8.GetByteCount(serviceName),
            out var serviceNameHandle);

        if (serviceNameResult != IOX2_OK)
        {
            return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, serviceNameResult, iox2_semantic_string_error_string));
        }

        try
        {
            var serviceNamePtr = iox2_cast_service_name_ptr(serviceNameHandle);

            // Get service builder
            var nodeHandle = _node._handle.DangerousGetHandle();
            var serviceBuilderHandle = iox2_node_service_builder(
                ref nodeHandle,
                IntPtr.Zero,
                serviceNamePtr);

            if (serviceBuilderHandle == IntPtr.Zero)
            {
                return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, "no handle returned"));
            }

            // Get request-response builder
            var requestResponseBuilderHandle = iox2_service_builder_request_response(serviceBuilderHandle);

            if (requestResponseBuilderHandle == IntPtr.Zero)
            {
                return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, "no handle returned"));
            }

            // Set request payload type details
            var requestTypeName = ServiceBuilder.GetRustCompatibleTypeName<TRequest>();
            var requestTypeSize = (ulong)sizeof(TRequest);
            var requestTypeAlignment = GetAlignment<TRequest>(requestTypeSize);

            var requestResult = iox2_service_builder_request_response_set_request_payload_type_details(
                ref requestResponseBuilderHandle,
                _dynamicRequestPayloads ? iox2_type_variant_e.DYNAMIC : iox2_type_variant_e.FIXED_SIZE,
                requestTypeName,
                System.Text.Encoding.UTF8.GetByteCount(requestTypeName),
                requestTypeSize,
                requestTypeAlignment);

            if (requestResult != IOX2_OK)
            {
                return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, requestResult));
            }

            // Set response payload type details
            var responseTypeName = ServiceBuilder.GetRustCompatibleTypeName<TResponse>();
            var responseTypeSize = (ulong)sizeof(TResponse);
            var responseTypeAlignment = GetAlignment<TResponse>(responseTypeSize);

            var responseResult = iox2_service_builder_request_response_set_response_payload_type_details(
                ref requestResponseBuilderHandle,
                _dynamicResponsePayloads ? iox2_type_variant_e.DYNAMIC : iox2_type_variant_e.FIXED_SIZE,
                responseTypeName,
                System.Text.Encoding.UTF8.GetByteCount(responseTypeName),
                responseTypeSize,
                responseTypeAlignment);

            if (responseResult != IOX2_OK)
            {
                return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, responseResult));
            }

            // Open or create the service
            var result = openOrCreateFunc(
                requestResponseBuilderHandle,
                IntPtr.Zero,
                out var portFactoryHandle);

            if (result != IOX2_OK)
            {
                return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Err(Iox2Error.FromNative(Iox2ErrorKind.RequestResponseServiceCreationFailed, result, iox2_request_response_open_or_create_error_string));
            }

            return Result<RequestResponseService<TRequest, TResponse>, Iox2Error>.Ok(
                new RequestResponseService<TRequest, TResponse>(
                    portFactoryHandle,
                    _initialMaxRequestSliceLen,
                    _initialMaxResponseSliceLen));
        }
        finally
        {
            iox2_service_name_drop(serviceNameHandle);
        }
    }

    private static ulong GetAlignment<T>(ulong typeSize) where T : unmanaged
    {
        if (typeof(T).IsPrimitive)
        {
            return typeSize;
        }
        else
        {
            // For structs, check if there's a StructLayout attribute specifying Pack
            var layoutAttr = typeof(T).StructLayoutAttribute;
            if (layoutAttr != null && layoutAttr.Pack > 0)
            {
                return (ulong)layoutAttr.Pack;
            }
            else
            {
                // Default to pointer size for alignment
                return (ulong)IntPtr.Size;
            }
        }
    }
}