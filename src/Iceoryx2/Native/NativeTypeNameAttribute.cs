// Copyright (c) 2026 Contributors to the Eclipse Foundation
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
using System.Diagnostics;

namespace Iceoryx2.Native;

/// <summary>
/// Records the C spelling of a type that ClangSharp mapped to a C# one, so the
/// generated bindings in Interop.g.cs stay traceable to the cbindgen header.
/// Documentation only; it has no runtime effect.
/// </summary>
[Conditional("DEBUG")]
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property
    | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name) => Name = name;

    /// <summary>The type's spelling in the C header.</summary>
    public string Name { get; }
}
