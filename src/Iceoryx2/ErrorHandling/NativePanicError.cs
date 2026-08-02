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

namespace Iceoryx2.ErrorHandling
{
    /// <summary>
    /// Represents an internal panic caught inside the native iceoryx2
    /// library. The library's internal state may be inconsistent afterwards:
    /// dispose all iceoryx2 objects and restart in a fresh domain.
    /// </summary>
    public class NativePanicError : Iox2Error
    {
        /// <summary>
        /// Gets the error kind for pattern matching.
        /// </summary>
        public override Iox2ErrorKind Kind => Iox2ErrorKind.NativePanic;

        /// <summary>
        /// Gets the native panic message, including its source location.
        /// </summary>
        public override string? Details { get; }

        /// <summary>
        /// Gets a human-readable error message.
        /// </summary>
        public override string Message => Details != null
            ? $"The native iceoryx2 library panicked: {Details}"
            : "The native iceoryx2 library panicked.";

        /// <summary>
        /// Initializes a new instance of the <see cref="NativePanicError"/> class.
        /// </summary>
        /// <param name="details">The native panic message.</param>
        public NativePanicError(string? details = null)
        {
            Details = details;
        }
    }
}
