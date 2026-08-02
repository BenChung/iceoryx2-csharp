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
using System;
using System.Runtime.InteropServices;

namespace Iceoryx2;

/// <summary>
/// Base class for all iceoryx2 errors. Provides rich, contextual error information
/// that enables better diagnostics and troubleshooting.
/// </summary>
public abstract class Iox2Error
{
    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public abstract string Message { get; }

    /// <summary>
    /// Gets the error kind for pattern matching and backward compatibility.
    /// </summary>
    public abstract Iox2ErrorKind Kind { get; }

    /// <summary>
    /// Gets additional details about the error, if available.
    /// </summary>
    public virtual string? Details { get; }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    public override string ToString() => Message;

    /// <summary>
    /// Creates an Iox2Error from an error kind with optional details.
    /// </summary>
    public static Iox2Error FromKind(Iox2ErrorKind kind, string? details = null)
    {
        return kind switch
        {
            Iox2ErrorKind.NodeCreationFailed => new NodeCreationError(details),
            Iox2ErrorKind.ServiceCreationFailed => new ServiceCreationError(null, details),
            Iox2ErrorKind.PublisherCreationFailed => new PublisherCreationError(details),
            Iox2ErrorKind.SubscriberCreationFailed => new SubscriberCreationError(details),
            Iox2ErrorKind.SampleLoanFailed => new SampleLoanError(details),
            Iox2ErrorKind.SendFailed => new SendError(details),
            Iox2ErrorKind.ReceiveFailed => new ReceiveError(details),
            Iox2ErrorKind.NotifierCreationFailed => new NotifierCreationError(details),
            Iox2ErrorKind.ListenerCreationFailed => new ListenerCreationError(details),
            Iox2ErrorKind.NotifyFailed => new NotifyError(null, details),
            Iox2ErrorKind.WaitFailed => new WaitError(details),
            Iox2ErrorKind.EventServiceCreationFailed => new EventServiceCreationError(null, details),
            Iox2ErrorKind.RequestResponseServiceCreationFailed => new RequestResponseServiceCreationError(null, details),
            Iox2ErrorKind.ClientCreationFailed => new ClientCreationError(details),
            Iox2ErrorKind.ServerCreationFailed => new ServerCreationError(details),
            Iox2ErrorKind.RequestLoanFailed => new RequestLoanError(details),
            Iox2ErrorKind.RequestSendFailed => new RequestSendError(details),
            Iox2ErrorKind.ResponseLoanFailed => new ResponseLoanError(details),
            Iox2ErrorKind.ResponseSendFailed => new ResponseSendError(details),
            Iox2ErrorKind.ResponseReceiveFailed => new ResponseReceiveError(details),
            Iox2ErrorKind.InvalidHandle => new InvalidHandleError(details),
            Iox2ErrorKind.WaitSetCreationFailed => new WaitSetCreationError(details),
            Iox2ErrorKind.WaitSetAttachmentFailed => new WaitSetAttachmentError(details),
            Iox2ErrorKind.WaitSetRunFailed => new WaitSetRunError(details),
            Iox2ErrorKind.ConnectionUpdateFailed => new ConnectionUpdateError(details),
            Iox2ErrorKind.BlackboardServiceCreationFailed => new BlackboardServiceCreationError(null, details),
            Iox2ErrorKind.WriterCreationFailed => new WriterCreationError(details),
            Iox2ErrorKind.ReaderCreationFailed => new ReaderCreationError(details),
            Iox2ErrorKind.EntryAccessFailed => new EntryAccessError(details),
            Iox2ErrorKind.ConfigCreationFailed => new ConfigCreationError(details),
            Iox2ErrorKind.NativePanic => new NativePanicError(details),
            Iox2ErrorKind.Unknown => new UnknownError(details),
            _ => new UnknownError(details)
        };
    }

    /// <summary>
    /// Creates the error for a failed native call: a <see cref="NativePanicError"/>
    /// when a caught panic caused the failure, otherwise an error of
    /// <paramref name="kind"/> carrying the native error code and its
    /// human-readable name from the given *_error_string function.
    /// </summary>
    internal static Iox2Error FromNative(Iox2ErrorKind kind, int code, Func<int, IntPtr> errorString)
    {
        var panic = Iox2Runtime.TakeLastPanic();
        if (panic != null)
            return new NativePanicError(panic);

        // Error enums start at 1; other values must not reach the native
        // enum-typed error_string functions.
        var reason = code > 0 ? Marshal.PtrToStringUTF8(errorString(code)) : null;
        return FromKind(kind, reason != null ? $"error {code}: {reason}" : $"error {code}");
    }

    /// <summary>
    /// Creates the error for a failed native call whose error enum has no
    /// *_error_string function: a <see cref="NativePanicError"/> when a caught
    /// panic caused the failure, otherwise an error of <paramref name="kind"/>
    /// carrying the native error code.
    /// </summary>
    internal static Iox2Error FromNative(Iox2ErrorKind kind, int code)
    {
        var panic = Iox2Runtime.TakeLastPanic();
        if (panic != null)
            return new NativePanicError(panic);
        return FromKind(kind, $"error {code}");
    }

    /// <summary>
    /// Creates the error for a native call that returned no handle: a
    /// <see cref="NativePanicError"/> when a caught panic caused it (guarded
    /// calls return a null sentinel after a panic), otherwise an error of
    /// <paramref name="kind"/> with the given detail.
    /// </summary>
    internal static Iox2Error FromNative(Iox2ErrorKind kind, string detail)
    {
        var panic = Iox2Runtime.TakeLastPanic();
        if (panic != null)
            return new NativePanicError(panic);
        return FromKind(kind, detail);
    }

    /// <summary>
    /// Creates the error for an exception that escaped a native interaction.
    /// The single policy point for how much of the exception the error
    /// carries.
    /// </summary>
    internal static Iox2Error FromException(Iox2ErrorKind kind, Exception exception) =>
        FromKind(kind, exception.ToString());

    // Backward compatibility: Static error instances

    /// <summary>Gets a <see cref="NodeCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error NodeCreationFailed => new NodeCreationError();

    /// <summary>Gets a <see cref="ServiceCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error ServiceCreationFailed => new ServiceCreationError(null);

    /// <summary>Gets a <see cref="PublisherCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error PublisherCreationFailed => new PublisherCreationError();

    /// <summary>Gets a <see cref="SubscriberCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error SubscriberCreationFailed => new SubscriberCreationError();

    /// <summary>Gets a <see cref="SampleLoanError"/> instance for backward compatibility.</summary>
    public static Iox2Error SampleLoanFailed => new SampleLoanError();

    /// <summary>Gets a <see cref="SendError"/> instance for backward compatibility.</summary>
    public static Iox2Error SendFailed => new SendError();

    /// <summary>Gets a <see cref="ReceiveError"/> instance for backward compatibility.</summary>
    public static Iox2Error ReceiveFailed => new ReceiveError();

    /// <summary>Gets a <see cref="NotifierCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error NotifierCreationFailed => new NotifierCreationError();

    /// <summary>Gets a <see cref="ListenerCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error ListenerCreationFailed => new ListenerCreationError();

    /// <summary>Gets a <see cref="NotifyError"/> instance for backward compatibility.</summary>
    public static Iox2Error NotifyFailed => new NotifyError();

    /// <summary>Gets a <see cref="WaitError"/> instance for backward compatibility.</summary>
    public static Iox2Error WaitFailed => new WaitError();

    /// <summary>Gets an <see cref="EventServiceCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error EventServiceCreationFailed => new EventServiceCreationError(null);

    /// <summary>Gets a <see cref="RequestResponseServiceCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error RequestResponseServiceCreationFailed => new RequestResponseServiceCreationError(null);

    /// <summary>Gets a <see cref="ClientCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error ClientCreationFailed => new ClientCreationError();

    /// <summary>Gets a <see cref="ServerCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error ServerCreationFailed => new ServerCreationError();

    /// <summary>Gets a <see cref="RequestLoanError"/> instance for backward compatibility.</summary>
    public static Iox2Error RequestLoanFailed => new RequestLoanError();

    /// <summary>Gets a <see cref="RequestSendError"/> instance for backward compatibility.</summary>
    public static Iox2Error RequestSendFailed => new RequestSendError();

    /// <summary>Gets a <see cref="ResponseLoanError"/> instance for backward compatibility.</summary>
    public static Iox2Error ResponseLoanFailed => new ResponseLoanError();

    /// <summary>Gets a <see cref="ResponseSendError"/> instance for backward compatibility.</summary>
    public static Iox2Error ResponseSendFailed => new ResponseSendError();

    /// <summary>Gets a <see cref="ResponseReceiveError"/> instance for backward compatibility.</summary>
    public static Iox2Error ResponseReceiveFailed => new ResponseReceiveError();

    /// <summary>Gets an <see cref="InvalidHandleError"/> instance for backward compatibility.</summary>
    public static Iox2Error InvalidHandle => new InvalidHandleError();

    /// <summary>Gets a <see cref="WaitSetCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error WaitSetCreationFailed => new WaitSetCreationError();

    /// <summary>Gets a <see cref="WaitSetAttachmentError"/> instance for backward compatibility.</summary>
    public static Iox2Error WaitSetAttachmentFailed => new WaitSetAttachmentError();

    /// <summary>Gets a <see cref="WaitSetRunError"/> instance for backward compatibility.</summary>
    public static Iox2Error WaitSetRunFailed => new WaitSetRunError();

    /// <summary>Gets a <see cref="ConnectionUpdateError"/> instance for backward compatibility.</summary>
    public static Iox2Error ConnectionUpdateFailed => new ConnectionUpdateError();

    /// <summary>Gets a <see cref="BlackboardServiceCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error BlackboardServiceCreationFailed => new BlackboardServiceCreationError(null);

    /// <summary>Gets a <see cref="WriterCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error WriterCreationFailed => new WriterCreationError();

    /// <summary>Gets a <see cref="ReaderCreationError"/> instance for backward compatibility.</summary>
    public static Iox2Error ReaderCreationFailed => new ReaderCreationError();

    /// <summary>Gets an <see cref="EntryAccessError"/> instance for backward compatibility.</summary>
    public static Iox2Error EntryAccessFailed => new EntryAccessError();

    /// <summary>Gets an <see cref="UnknownError"/> instance for backward compatibility.</summary>
    public static Iox2Error Unknown => new UnknownError();
}