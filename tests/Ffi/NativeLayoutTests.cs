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

using Iceoryx2.Native.Interop;
using System.Runtime.InteropServices;
using Xunit;

namespace Iceoryx2.Tests.Ffi;

/// <summary>
/// Sanity checks on the ClangSharp-generated interop layouts. The generator derives
/// them from the cbindgen header with a real C compiler front end, so these assert
/// that the generated types are present and marshalable rather than re-deriving the
/// numbers, which would just restate the generator's own output.
/// </summary>
public class NativeLayoutTests
{
    [Fact]
    public void StaticConfig_PadsBeforeMessagingPattern()
    {
        // The offset Node.List reads. Computed by hand once, it was wrong: repr(C) pads
        // the id+name char run out to the 4-byte enum's alignment, so reading at
        // id + name returned a padding byte and shifted every discriminant by 256.
        var idOffset = Marshal.OffsetOf<iox2_static_config_t>(nameof(iox2_static_config_t.id)).ToInt32();
        var nameOffset = Marshal.OffsetOf<iox2_static_config_t>(nameof(iox2_static_config_t.name)).ToInt32();
        var patternOffset = Marshal.OffsetOf<iox2_static_config_t>(
            nameof(iox2_static_config_t.messaging_pattern)).ToInt32();

        Assert.Equal(0, idOffset);
        Assert.Equal(Iox2Constants.IOX2_SERVICE_HASH_LENGTH, nameOffset);

        var afterName = nameOffset + Iox2Constants.IOX2_SERVICE_NAME_LENGTH;
        Assert.True(patternOffset > afterName,
            "the char arrays end on an odd boundary, so the enum must be padded past them");
        Assert.Equal(0, patternOffset % 4);
    }

    [Fact]
    public void GeneratedLayouts_AreMarshalable()
    {
        // The details union overlaps four structs. Generated as fixed buffers they stay
        // blittable; an object-typed array field there makes the type fail to load.
        Assert.True(Marshal.SizeOf<iox2_static_config_t>() > 0);
        Assert.True(Marshal.SizeOf<iox2_static_config_details_t>() > 0);
        Assert.Equal(sizeof(ulong), Marshal.SizeOf<iox2_event_id_t>());
    }

    [Fact]
    public void GeneratedConstants_MatchTheHeader()
    {
        // A parse failure in the generator would emit zeros or drop the constants.
        Assert.Equal(0, Iox2Constants.IOX2_OK);
        Assert.Equal(64, Iox2Constants.IOX2_SERVICE_HASH_LENGTH);
        Assert.Equal(255, Iox2Constants.IOX2_SERVICE_NAME_LENGTH);
        Assert.Equal(128, Iox2Constants.IOX2_NODE_NAME_LENGTH);
    }
}
