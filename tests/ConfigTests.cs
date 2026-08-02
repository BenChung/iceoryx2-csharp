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
        public void Prelink_ResolvesAllNativeEntryPoints()
        {
            // Guards against binding drift: every DllImport must resolve
            // against the current native library.
            Iox2Runtime.Prelink();
        }

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
        public void ForDomain_RelativeIpcRoot_Throws()
        {
            Assert.Throws<ArgumentException>(() => Config.ForDomain("relative\\path", "dom"));
        }

        [Fact]
        public void WipeDomain_RemovesDomainStateAndPlatformMarkers()
        {
            var ipcRoot = UniqueIpcRoot();
            var domain = "wipetest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using (var config = Config.ForDomain(ipcRoot, domain).Unwrap())
            {
                using var node = NodeBuilder.New()
                    .Name("wipe_test_node")
                    .WithConfig(config)
                    .Create()
                    .Unwrap();
                using var service = node.ServiceBuilder()
                    .PublishSubscribe<TestPayload>()
                    .Open("wipe_test_service")
                    .Unwrap();
                using var publisher = service.PublisherBuilder().Create().Unwrap();
            }

            // Native cleanup also runs on the finalizer thread for any test
            // object left undisposed; drain it so no cleanup scan races the
            // wipe below (a scan hitting vanishing files aborts the process).
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // A leftover marker as a crashed process would leave it.
            var platformTmp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"C:\ProgramData\iceoryx2\tmp\"
                : "/tmp/";
            var plantedMarker = Path.Combine(platformTmp, $"iox2_{domain}_leftover.shm_state");
            Directory.CreateDirectory(platformTmp);
            File.WriteAllText(plantedMarker, "");

            var clean = Config.WipeDomain(ipcRoot, domain);

            Assert.True(clean);
            Assert.False(Directory.Exists(Path.Combine(ipcRoot, domain)));
            Assert.False(File.Exists(plantedMarker));
        }

        [Fact]
        public void OverlongDomainPaths_SurfaceAsNativePanicInsteadOfCrash()
        {
            // ipcRoot + domain combine into paths beyond iceoryx2's 255-char
            // limit, which is a fatal_panic in the native library. Containment
            // must surface it as an error and keep the process alive.
            var ipcRoot = Path.Combine(Path.GetTempPath(), new string('a', 60));
            var domain = new string('d', 80);
            using var config = Config.ForDomain(ipcRoot, domain).Unwrap();

            string? reported = null;
            Action<string> handler = message => reported = message;
            Iox2Runtime.NativePanic += handler;
            try
            {
                var result = NodeBuilder.New().WithConfig(config).Create();
                if (result.IsOk)
                {
                    using var node = result.Unwrap();
                    var service = node.ServiceBuilder()
                        .PublishSubscribe<TestPayload>()
                        .Open("panic_probe");
                    Assert.True(service.IsErr);
                }
                else
                {
                    Assert.True(result.IsErr);
                }
            }
            finally
            {
                Iox2Runtime.NativePanic -= handler;
            }

            Assert.NotNull(reported);
            Assert.Contains("path length", reported);
        }

        [Fact]
        public void Listeners_WorkUnderLongDomainRoots()
        {
            // Event sockets live at <root>\<prefix><listener-id>.event; with a
            // realistic ~100-char domain root this exceeds the classic 108-byte
            // sun_path limit, which the forked Windows platform layer raises to
            // hold a full 255-char path.
            var ipcRoot = Path.Combine(Path.GetTempPath(), "Long Company Name", "Long Game Name",
                "script_ipc-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var domain = "longroot_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var config = Config.ForDomain(ipcRoot, domain).Unwrap();
            using var node = NodeBuilder.New().WithConfig(config).Create().Unwrap();
            using var service = node.ServiceBuilder().Event().Open("svc/heartbeat/response").Unwrap();

            using var listener = service.CreateListener().Unwrap();
            using var notifier = service.CreateNotifier().Unwrap();

            notifier.Notify().Unwrap();
            Assert.NotNull(listener.TryWait().Unwrap());
        }

        [Fact]
        public void Listeners_WorkUnderShortDomainRoots()
        {
            var ipcRoot = @"C:\ProgramData\sp_ipc";
            var domain = "t_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var config = Config.ForDomain(ipcRoot, domain).Unwrap();
            using var node = NodeBuilder.New().WithConfig(config).Create().Unwrap();
            using var service = node.ServiceBuilder().Event().Open("svc/heartbeat/response").Unwrap();

            using var listener = service.CreateListener().Unwrap();
            using var notifier = service.CreateNotifier().Unwrap();

            notifier.Notify().Unwrap();
            Assert.NotNull(listener.TryWait().Unwrap());

            node.Dispose();
            Config.WipeDomain(ipcRoot, domain);
        }

        [Fact]
        public void ServiceErrors_CarryNativeReason()
        {
            var ipcRoot = UniqueIpcRoot();
            using var config = Config.ForDomain(ipcRoot, "errdetail").Unwrap();
            using var node = NodeBuilder.New().WithConfig(config).Create().Unwrap();
            using var first = node.ServiceBuilder().Event().Create("dup_event").Unwrap();

            var second = node.ServiceBuilder().Event().Create("dup_event");

            Assert.True(second.IsErr);
            var message = second.Match(ok => "", err => err.Message);
            Assert.Contains("already exists", message);
        }

        [Fact]
        public void WithConfig_ServicesLiveUnderDomainRoot()
        {
            var ipcRoot = UniqueIpcRoot();
            using var config = Config.ForDomain(ipcRoot, "cfgtest").Unwrap();

            using var node = NodeBuilder.New()
                .Name("config_test_node")
                .WithConfig(config)
                .Create()
                .Unwrap();
            using var service = node.ServiceBuilder()
                .PublishSubscribe<TestPayload>()
                .Open("config_test_service")
                .Unwrap();

            var servicesDir = Path.Combine(ipcRoot, "cfgtest", "services");
            Assert.True(Directory.Exists(servicesDir));
            Assert.NotEmpty(Directory.GetFiles(servicesDir));
        }
    }
}
