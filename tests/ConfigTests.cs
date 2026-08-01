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

using Iceoryx2;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Iceoryx2.Tests
{
    public class ConfigTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TestPayload
        {
            public int Value;
        }

        private static string UniqueIpcRoot() =>
            Path.Combine(Path.GetTempPath(), "iox2-csharp-config-tests", Guid.NewGuid().ToString("N"));

        [Fact]
        public void Default_ExposesRootPathAndPrefix()
        {
            using var config = Config.Default().Unwrap();

            Assert.False(string.IsNullOrEmpty(config.RootPath));
            Assert.Equal("iox2_", config.Prefix);
        }

        [Fact]
        public void FromGlobal_ExposesRootPathAndPrefix()
        {
            using var config = Config.FromGlobal().Unwrap();

            Assert.False(string.IsNullOrEmpty(config.RootPath));
            Assert.False(string.IsNullOrEmpty(config.Prefix));
        }

        [Fact]
        public void FromFile_MissingFile_ReturnsError()
        {
            var result = Config.FromFile(Path.Combine(UniqueIpcRoot(), "missing.toml"));

            Assert.True(result.IsErr);
        }

        [Fact]
        public void RootPath_RoundTrips()
        {
            using var config = Config.Default().Unwrap();
            var path = Path.Combine(UniqueIpcRoot(), "domain");

            config.RootPath = path;

            Assert.Equal(path, config.RootPath);
        }

        [Fact]
        public void Prefix_RoundTrips()
        {
            using var config = Config.Default().Unwrap();

            config.Prefix = "myapp_";

            Assert.Equal("myapp_", config.Prefix);
        }

        [Fact]
        public void RootPath_InvalidValue_Throws()
        {
            using var config = Config.Default().Unwrap();

            Assert.Throws<ArgumentException>(() => config.RootPath = new string('a', 300));
        }

        [Fact]
        public void Prefix_InvalidValue_Throws()
        {
            using var config = Config.Default().Unwrap();

            Assert.Throws<ArgumentException>(() => config.Prefix = new string('a', 300));
        }

        [Fact]
        public void Clone_IsIndependent()
        {
            using var config = Config.Default().Unwrap();
            config.Prefix = "original_";
            using var clone = config.Clone();

            config.Prefix = "changed_";

            Assert.Equal("original_", clone.Prefix);
            Assert.Equal("changed_", config.Prefix);
        }

        [Fact]
        public void ForDomain_DerivesNamespaceAndCreatesRoot()
        {
            var ipcRoot = UniqueIpcRoot();

            using var config = Config.ForDomain(ipcRoot, "testdom").Unwrap();

            Assert.Equal(Path.Combine(ipcRoot, "testdom"), config.RootPath);
            Assert.Equal("iox2_testdom_", config.Prefix);
            Assert.True(Directory.Exists(config.RootPath));
        }

        [Fact]
        public void WithConfig_ServicesLiveUnderDomainRoot()
        {
            var ipcRoot = UniqueIpcRoot();
            using var config = Config.ForDomain(ipcRoot, "cfgtest").Unwrap();

            var node = NodeBuilder.New()
                .Name("config_test_node")
                .WithConfig(config)
                .Create()
                .Unwrap();
            var service = node.ServiceBuilder()
                .PublishSubscribe<TestPayload>()
                .Open("config_test_service")
                .Unwrap();

            var servicesDir = Path.Combine(ipcRoot, "cfgtest", "services");
            Assert.True(Directory.Exists(servicesDir));
            Assert.NotEmpty(Directory.GetFiles(servicesDir));
        }
    }
}
