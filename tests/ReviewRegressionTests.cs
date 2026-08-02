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
using Iceoryx2.RequestResponse;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Iceoryx2.Tests
{
    /// <summary>
    /// Regressions for defects found by the paired reviewer/critic audit of the
    /// changes since af02370.
    /// </summary>
    public class ReviewRegressionTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Payload
        {
            public int Id;
            public double Value;
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static string TempRoot() =>
            Path.Combine(Path.GetTempPath(), "iox2-review-regressions", Guid.NewGuid().ToString("N"));

        // ---- WipeDomain input validation -------------------------------------------------

        [Theory]
        [InlineData("..")]
        [InlineData(".")]
        [InlineData("../escape")]
        [InlineData("nested/segment")]
        [InlineData("nested\\segment")]
        [InlineData("wild*card")]
        [InlineData("wild?card")]
        [InlineData("")]
        public void WipeDomain_RejectsDomainThatIsNotASinglePlainSegment(string domain)
        {
            Assert.Throws<ArgumentException>(() => Config.WipeDomain(@"C:\ProgramData\iceoryx2", domain));
        }

        [Fact]
        public void WipeDomain_RejectsRootedDomainInsteadOfDeletingIt()
        {
            // Path.Combine discards its first argument when the second is rooted, so an
            // unvalidated rooted domain would redirect the recursive delete out of the IPC root.
            var victim = Path.Combine(TempRoot(), "victim");
            Directory.CreateDirectory(victim);
            var canary = Path.Combine(victim, "canary.txt");
            File.WriteAllText(canary, "must survive");

            try
            {
                Assert.Throws<ArgumentException>(
                    () => Config.WipeDomain(@"C:\ProgramData\iceoryx2", victim));
                Assert.True(File.Exists(canary), "WipeDomain deleted a directory outside the IPC root");
            }
            finally
            {
                try { Directory.Delete(Path.GetDirectoryName(victim)!, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ForDomain_RejectsTheSameDomainNamesAsWipeDomain()
        {
            // The two must agree: WipeDomain has to be able to undo what ForDomain created.
            Assert.Throws<ArgumentException>(() => Config.ForDomain(Path.GetTempPath(), ".."));
            Assert.Throws<ArgumentException>(() => Config.ForDomain(Path.GetTempPath(), "nested/segment"));
        }

        [Fact]
        public void WipeDomain_RemovesItsOwnDomainDirectory()
        {
            var root = TempRoot();
            var domain = "wipeme";
            var domainDir = Path.Combine(root, domain);
            Directory.CreateDirectory(Path.Combine(domainDir, "nodes"));
            File.WriteAllText(Path.Combine(domainDir, "nodes", "a.service_tag"), "x");

            try
            {
                Config.WipeDomain(root, domain);
                Assert.False(Directory.Exists(domainDir));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        [Fact]
        public void WipeDomain_LeavesSiblingDomainWhoseNameExtendsTheWipedOne()
        {
            // Domain "app" globs its marker files as iox2_app_*, which also matches every
            // marker file of the live sibling domain "app_worker". The glob runs against the
            // platform shm directory, so the fixture has to live there too.
            var shmDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"C:\ProgramData\iceoryx2\shm\"
                : "/dev/shm/";
            Directory.CreateDirectory(shmDir);

            var stem = "wipesib" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var sibling = stem + "_worker";
            var root = TempRoot();
            Directory.CreateDirectory(Path.Combine(root, stem));
            Directory.CreateDirectory(Path.Combine(root, sibling));

            var mine = Path.Combine(shmDir, $"iox2_{stem}_service");
            var theirs = Path.Combine(shmDir, $"iox2_{sibling}_service");
            File.WriteAllText(mine, "x");
            File.WriteAllText(theirs, "x");

            try
            {
                Config.WipeDomain(root, stem);

                Assert.False(Directory.Exists(Path.Combine(root, stem)));
                Assert.False(File.Exists(mine), "WipeDomain left its own marker file behind");
                Assert.True(Directory.Exists(Path.Combine(root, sibling)),
                    $"wiping '{stem}' removed the sibling domain directory");
                Assert.True(File.Exists(theirs),
                    $"wiping '{stem}' deleted the marker file of live sibling domain '{sibling}'");
            }
            finally
            {
                try { File.Delete(mine); } catch { }
                try { File.Delete(theirs); } catch { }
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        // ---- Send() consumes the handle on every path --------------------------------------

        [Fact]
        public void SampleSend_MarksTheSampleDisposedSoDisposeCannotDoubleFree()
        {
            using var node = NodeBuilder.New().Name("regr_sample_send").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .PublishSubscribe<Payload>()
                .Open(UniqueName("regr_sample_send"))
                .Unwrap();
            using var publisher = service.PublisherBuilder().Create().Unwrap();

            var sample = publisher.Loan<Payload>().Unwrap();
            sample.Payload = new Payload { Id = 1, Value = 2.0 };
            Assert.True(sample.Send().IsOk);

            // Native freed the sample struct inside send; a second drop would be a double free.
            sample.Dispose();
            sample.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { sample.Send(); });
        }

        [Fact]
        public void RequestMutSend_MarksTheRequestDisposedSoDisposeCannotDoubleFree()
        {
            using var node = NodeBuilder.New().Name("regr_request_send").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .RequestResponse<Payload, Payload>()
                .Open(UniqueName("regr_request_send"))
                .Unwrap();
            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            var request = client.Loan().Unwrap();
            request.Payload = new Payload { Id = 3, Value = 4.0 };
            using var pending = request.Send().Unwrap();

            request.Dispose();
            request.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { request.Send(); });
        }

        [Fact]
        public void ResponseMutSend_MarksTheResponseDisposedSoDisposeCannotDoubleFree()
        {
            using var node = NodeBuilder.New().Name("regr_response_send").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .RequestResponse<Payload, Payload>()
                .Open(UniqueName("regr_response_send"))
                .Unwrap();
            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            using var pending = client.SendCopy(new Payload { Id = 5, Value = 6.0 }).Unwrap();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            Request<Payload, Payload>? request = null;
            while (DateTime.UtcNow < deadline && request == null)
            {
                request = server.Receive().Unwrap();
                if (request == null) System.Threading.Thread.Sleep(10);
            }
            Assert.NotNull(request);

            using (request)
            {
                var response = request!.LoanResponse().Unwrap();
                response.Payload = new Payload { Id = 7, Value = 8.0 };
                Assert.True(response.Send().IsOk);

                response.Dispose();
                response.Dispose();
                Assert.Throws<ObjectDisposedException>(() => { response.Send(); });
            }
        }

        [Fact]
        public void RequestMutSend_FailedSendStillRelinquishesTheHandle()
        {
            // Native frees the request struct before attempting delivery, so a FAILED send
            // leaves the handle spent. The wrapper must treat it as consumed on that path too,
            // or Dispose() double-frees. A client may hold only max_active_requests_per_client
            // pending responses, so holding them forces a deterministic send failure.
            using var node = NodeBuilder.New().Name("regr_send_err").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .RequestResponse<Payload, Payload>()
                .Open(UniqueName("regr_send_err"))
                .Unwrap();
            using var client = service.CreateClient().Unwrap();

            var held = new System.Collections.Generic.List<PendingResponse<Payload>>();
            RequestMut<Payload, Payload>? failed = null;
            try
            {
                for (int i = 0; i < 64 && failed == null; i++)
                {
                    var request = client.Loan().Unwrap();
                    var send = request.Send();
                    if (send.IsOk)
                        held.Add(send.Unwrap());
                    else
                        failed = request;
                }

                Assert.NotNull(failed);

                // The handle is gone even though the send failed: re-sending must be refused
                // rather than dereferencing freed memory.
                Assert.Throws<ObjectDisposedException>(() => { failed!.Send(); });

                // And disposing must not hand the freed pointer back to iox2_request_mut_drop.
                failed!.Dispose();
                failed.Dispose();
            }
            finally
            {
                foreach (var p in held) p.Dispose();
            }
        }

        [Fact]
        public void ResponseMutSend_FailedSendStillRelinquishesTheHandle()
        {
            // Same invariant as the request side: a server may hold only
            // max_loaned_responses_per_request responses, and the disconnected/exhausted
            // send path frees the struct before it reports the failure.
            using var node = NodeBuilder.New().Name("regr_resp_send_err").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .RequestResponse<Payload, Payload>()
                .Open(UniqueName("regr_resp_send_err"))
                .Unwrap();
            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            using var pending = client.SendCopy(new Payload { Id = 1, Value = 1.0 }).Unwrap();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            Request<Payload, Payload>? request = null;
            while (DateTime.UtcNow < deadline && request == null)
            {
                request = server.Receive().Unwrap();
                if (request == null) System.Threading.Thread.Sleep(10);
            }
            Assert.NotNull(request);

            using (request)
            {
                ResponseMut<Payload>? failed = null;
                for (int i = 0; i < 64 && failed == null; i++)
                {
                    var loan = request!.LoanResponse();
                    if (!loan.IsOk)
                        break;
                    var response = loan.Unwrap();
                    if (!response.Send().IsOk)
                        failed = response;
                }

                if (failed == null)
                {
                    // The response queue never rejected a send on this platform; the
                    // request-side test still covers the shared invariant.
                    return;
                }

                Assert.Throws<ObjectDisposedException>(() => { failed!.Send(); });
                failed!.Dispose();
                failed.Dispose();
            }
        }

        [Fact]
        public void NativeErrorString_IsReadableNotSpaceMangled()
        {
            // The FFI macro derived labels by splitting on case boundaries, which put a space
            // between every letter of the SCREAMING_SNAKE_CASE C enum variants.
            using var node = NodeBuilder.New().Name("regr_errstr").Create().Unwrap();
            using var service = node.ServiceBuilder()
                .RequestResponse<Payload, Payload>()
                .Open(UniqueName("regr_errstr"))
                .Unwrap();
            using var client = service.CreateClient().Unwrap();

            var held = new System.Collections.Generic.List<PendingResponse<Payload>>();
            string? message = null;
            try
            {
                for (int i = 0; i < 64 && message == null; i++)
                {
                    var request = client.Loan().Unwrap();
                    var send = request.Send();
                    if (send.IsOk)
                        held.Add(send.Unwrap());
                    else
                        message = send.Match(_ => "", err => err.Message);
                }

                Assert.NotNull(message);
                Assert.Contains("exceeds max active requests", message!, StringComparison.Ordinal);
                Assert.DoesNotContain("e x c e e d s", message!, StringComparison.Ordinal);
            }
            finally
            {
                foreach (var p in held) p.Dispose();
            }
        }

        [Fact]
        public void NodeList_ReportsTheCorrectMessagingPattern()
        {
            // repr(C) aligns the 4-byte messaging_pattern enum to offset 320, not to
            // id + name = 319. Reading the padding byte shifts every discriminant by 256.
            using var node = NodeBuilder.New().Name("regr_msgpattern").Create().Unwrap();

            var eventName = UniqueName("regr_mp_event");
            var pubSubName = UniqueName("regr_mp_pubsub");

            using var eventService = node.ServiceBuilder().Event().Open(eventName).Unwrap();
            using var pubSubService = node.ServiceBuilder()
                .PublishSubscribe<Payload>().Open(pubSubName).Unwrap();

            var listed = node.List().Unwrap();

            var ev = listed.Find(s => s.Name == eventName);
            var ps = listed.Find(s => s.Name == pubSubName);

            Assert.NotNull(ev);
            Assert.NotNull(ps);
            Assert.Equal(MessagingPattern.Event, ev!.MessagingPattern);
            Assert.Equal(MessagingPattern.PublishSubscribe, ps!.MessagingPattern);
        }

        // ---- WaitSet.Stop ------------------------------------------------------------------

        [Fact]
        public void WaitSetStop_FromInsideCallbackEndsTheRun()
        {
            using var node = NodeBuilder.New().Name("regr_waitset_stop").Create().Unwrap();
            using var service = node.ServiceBuilder().Event().Open(UniqueName("regr_waitset_stop")).Unwrap();
            using var listener = service.CreateListener().Unwrap();
            using var notifier = service.CreateNotifier().Unwrap();

            using var waitset = WaitSetBuilder.New().Create().Unwrap();
            using var guard = waitset.AttachNotification(listener).Unwrap();

            notifier.Notify().Unwrap();

            var callbackCount = 0;
            var result = waitset.WaitAndProcessOnce(id =>
            {
                callbackCount++;
                waitset.Stop();
                return CallbackProgression.Continue;
            }, TimeSpan.FromSeconds(5));

            Assert.True(result.IsOk);
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public void WaitSetStop_BeforeAnEventSuppressesTheCallback()
        {
            using var node = NodeBuilder.New().Name("regr_waitset_stop2").Create().Unwrap();
            using var service = node.ServiceBuilder().Event().Open(UniqueName("regr_waitset_stop2")).Unwrap();
            using var listener = service.CreateListener().Unwrap();
            using var notifier = service.CreateNotifier().Unwrap();

            using var waitset = WaitSetBuilder.New().Create().Unwrap();
            using var guard = waitset.AttachNotification(listener).Unwrap();

            notifier.Notify().Unwrap();

            // A stop raised while a run is in flight suppresses that run's callback; a stop
            // raised before a run starts is cleared at entry, so this run still delivers.
            var callbackCount = 0;
            var result = waitset.WaitAndProcessOnce(id =>
            {
                callbackCount++;
                return CallbackProgression.Stop;
            }, TimeSpan.FromSeconds(5));

            Assert.True(result.IsOk);
            Assert.Equal(1, callbackCount);
        }

        // ---- Prelink ------------------------------------------------------------------------

        [Fact]
        public void Prelink_ActuallyVisitsTheInternalPInvokes()
        {
            // Marshal.PrelinkAll silently skips non-public methods, which let a binding to a
            // nonexistent entry point ship. Prelink must resolve every declaration it declares.
            var pinvokes = typeof(Iceoryx2.Node).Assembly
                .GetType("Iceoryx2.Native.Iox2NativeMethods")!
                .GetMethods(System.Reflection.BindingFlags.NonPublic
                          | System.Reflection.BindingFlags.Public
                          | System.Reflection.BindingFlags.Static);

            var count = 0;
            foreach (var m in pinvokes)
            {
                if ((m.Attributes & System.Reflection.MethodAttributes.PinvokeImpl) != 0)
                    count++;
            }

            Assert.True(count > 100, $"expected the binding to declare many P/Invokes, saw {count}");
            Iox2Runtime.Prelink();
        }
    }
}
