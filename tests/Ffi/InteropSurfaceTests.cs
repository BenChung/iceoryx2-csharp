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

using Iceoryx2.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Iceoryx2.Tests.Ffi;

/// <summary>
/// Whole-surface invariants over the P/Invoke layer. These run without touching the
/// native library, so they fail fast and point at the declaration rather than at
/// whichever feature happened to exercise it.
/// </summary>
public class InteropSurfaceTests
{
    private static Assembly BindingAssembly => typeof(Iox2NativeMethods).Assembly;

    private static IEnumerable<MethodInfo> PInvokes =>
        typeof(Iox2NativeMethods)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(m => (m.Attributes & MethodAttributes.PinvokeImpl) != 0);

    /// <summary>
    /// A single unloadable type makes Assembly.GetTypes() throw for the whole assembly,
    /// which breaks every consumer that scans it: dependency injection registration,
    /// serializer setup, plugin discovery, Unity's assembly scanning. Overlapping an
    /// object reference with non-object data in an explicit-layout struct is one way to
    /// produce that, and the failure surfaces nowhere near the offending declaration.
    /// </summary>
    [Fact]
    public void EveryTypeInTheAssembly_Loads()
    {
        try
        {
            var types = BindingAssembly.GetTypes();
            Assert.NotEmpty(types);
        }
        catch (ReflectionTypeLoadException e)
        {
            var reasons = string.Join(
                Environment.NewLine,
                e.LoaderExceptions.Where(x => x != null).Select(x => "  " + x!.Message).Distinct());
            Assert.Fail($"The binding assembly contains types the CLR cannot load:{Environment.NewLine}{reasons}");
        }
    }

    /// <summary>
    /// Every interop struct must have a computable unmanaged layout. A struct that only
    /// fails when something first marshals it turns a declaration bug into a runtime
    /// surprise in whichever call site touches it first.
    /// </summary>
    [Fact]
    public void EveryInteropStruct_HasAComputableLayout()
    {
        var failures = new List<string>();
        var examined = 0;

        var candidates = BindingAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Iceoryx2.Native", StringComparison.Ordinal))
            .Concat(typeof(Iox2NativeMethods).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));

        foreach (var type in candidates)
        {
            if (!type.IsValueType || type.IsEnum || type.IsGenericTypeDefinition)
                continue;

            examined++;
            try
            {
                var size = Marshal.SizeOf(type);
                if (size <= 0)
                    failures.Add($"{type.Name}: SizeOf returned {size}");
            }
            catch (Exception e)
            {
                failures.Add($"{type.Name}: {e.GetType().Name}: {e.Message}");
            }
        }

        // Reflection silently omits types that fail to load, so require at least one
        // examined struct rather than passing vacuously. EveryTypeInTheAssembly_Loads
        // covers whatever was omitted.
        Assert.True(examined > 0, "no interop structs were examined");

        Assert.True(failures.Count == 0,
            $"Interop structs with no computable layout:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    /// <summary>
    /// The C API is cdecl throughout. A stdcall declaration corrupts the stack on the
    /// first call rather than failing to load, so it is worth asserting statically.
    /// </summary>
    [Fact]
    public void EveryPInvoke_UsesCdecl()
    {
        var wrong = PInvokes
            .Select(m => new { m.Name, Attr = m.GetCustomAttribute<DllImportAttribute>() })
            .Where(x => x.Attr != null && x.Attr.CallingConvention != CallingConvention.Cdecl)
            .Select(x => $"{x.Name}: {x.Attr!.CallingConvention}")
            .ToList();

        Assert.True(wrong.Count == 0,
            $"P/Invokes not declared Cdecl:{Environment.NewLine}{string.Join(Environment.NewLine, wrong)}");
    }

    // Functions returning a by-value enum have no safe sentinel, so
    // scripts/generate_panic_guards.py cannot wrap them and the binding calls them
    // directly. Everything else must go through the panic-catching twin, or an internal
    // panic aborts the host process instead of surfacing as NativePanicError.
    private static readonly HashSet<string> UnguardableByValueEnumReturns = new()
    {
        "iox2_get_log_level",
        "iox2_waitset_signal_handling_mode",
    };

    [Fact]
    public void EveryPInvoke_TargetsThePanicGuardedEntryPoint()
    {
        var unguarded = new List<string>();

        foreach (var method in PInvokes)
        {
            var attr = method.GetCustomAttribute<DllImportAttribute>();
            if (attr == null)
                continue;

            // An absent EntryPoint means the managed name is the symbol name.
            var entryPoint = string.IsNullOrEmpty(attr.EntryPoint) ? method.Name : attr.EntryPoint;

            if (entryPoint.StartsWith("iox2_guarded_", StringComparison.Ordinal))
                continue;
            if (UnguardableByValueEnumReturns.Contains(entryPoint))
                continue;

            unguarded.Add($"{method.Name} -> {entryPoint}");
        }

        Assert.True(unguarded.Count == 0,
            "P/Invokes bypassing the panic guard layer. Either bind the iox2_guarded_ twin, " +
            $"or add it to UnguardableByValueEnumReturns with the reason:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unguarded));
    }

    /// <summary>
    /// Resolves every declared entry point against the shipped native library, so a
    /// binding to a symbol that does not exist fails here rather than on the first call
    /// to whichever feature uses it.
    /// </summary>
    [Fact]
    public void EveryPInvoke_ResolvesInTheNativeLibrary()
    {
        Assert.True(PInvokes.Count() > 100, $"expected many P/Invokes, saw {PInvokes.Count()}");

        // Marshal.PrelinkAll only visits public methods and every binding here is
        // internal, so go through Iox2Runtime.Prelink, which walks them explicitly.
        Iox2Runtime.Prelink();
    }
}
