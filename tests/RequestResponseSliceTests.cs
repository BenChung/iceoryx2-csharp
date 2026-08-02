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
using System.Runtime.InteropServices;
using Xunit;

namespace Iceoryx2.Tests
{
    public class RequestResponseSliceTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Reading
        {
            public int Id;
            public double Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Ack
        {
            public int Count;
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        /// <summary>
        /// Drives one client request through a server that echoes it back, returning the
        /// server's view of the request alongside the client's view of the response.
        /// </summary>
        private static (int RequestLength, int ResponseLength) RoundTrip(
            Server<Reading, Reading> server,
            PendingResponse<Reading> pending,
            Action<Request<Reading, Reading>> respond)
        {
            var request = PollRequest(server);
            var requestLength = request.Length;
            respond(request);
            request.Dispose();

            var response = pending.TimedReceive(TimeSpan.FromSeconds(5)).Unwrap();
            Assert.NotNull(response);
            using (response)
            {
                return (requestLength, response!.Length);
            }
        }

        private static Request<Reading, Reading> PollRequest(Server<Reading, Reading> server)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var request = server.Receive().Unwrap();
                if (request != null)
                {
                    return request;
                }
                System.Threading.Thread.Sleep(10);
            }

            throw new TimeoutException("Server did not receive a request");
        }

        [Fact]
        public void SliceLoan_RoundTripsElementsAndLengths()
        {
            using var node = NodeBuilder.New().Name("rr_slice_roundtrip").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Reading>()
                .EnableDynamicPayloads()
                .InitialMaxSliceLen(64)
                .Open(UniqueName("rr_slice_roundtrip"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            var requestMut = client.LoanSlice(5).Unwrap();
            Assert.Equal(5, requestMut.Length);

            var requestSpan = requestMut.PayloadAsSpan;
            Assert.Equal(5, requestSpan.Length);
            for (int i = 0; i < requestSpan.Length; i++)
            {
                requestSpan[i] = new Reading { Id = i, Value = i * 1.5 };
            }

            using var pending = requestMut.Send().Unwrap();

            var request = PollRequest(server);
            Assert.Equal(5, request.Length);

            var received = request.PayloadAsReadOnlySpan;
            Assert.Equal(5, received.Length);
            for (int i = 0; i < received.Length; i++)
            {
                Assert.Equal(i, received[i].Id);
                Assert.Equal(i * 1.5, received[i].Value);
            }

            // Respond with a different element count to prove the two directions are independent.
            var responseMut = request.LoanResponseSlice(3).Unwrap();
            Assert.Equal(3, responseMut.Length);
            var responseSpan = responseMut.PayloadAsSpan;
            for (int i = 0; i < responseSpan.Length; i++)
            {
                responseSpan[i] = new Reading { Id = 100 + i, Value = i * 2.5 };
            }
            responseMut.Send().Unwrap();
            request.Dispose();

            var response = pending.TimedReceive(TimeSpan.FromSeconds(5)).Unwrap();
            Assert.NotNull(response);
            using (response)
            {
                Assert.Equal(3, response!.Length);
                var responseReceived = response.PayloadAsReadOnlySpan;
                Assert.Equal(3, responseReceived.Length);
                for (int i = 0; i < responseReceived.Length; i++)
                {
                    Assert.Equal(100 + i, responseReceived[i].Id);
                    Assert.Equal(i * 2.5, responseReceived[i].Value);
                }
            }
        }

        [Fact]
        public void SendCopySpan_RoundTripsElements()
        {
            using var node = NodeBuilder.New().Name("rr_slice_sendcopy").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Reading>()
                .EnableDynamicPayloads()
                .InitialMaxSliceLen(16)
                .Open(UniqueName("rr_slice_sendcopy"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            var requestData = new[]
            {
                new Reading { Id = 7, Value = 7.5 },
                new Reading { Id = 8, Value = 8.5 },
                new Reading { Id = 9, Value = 9.5 },
            };

            using var pending = client.SendCopy(new ReadOnlySpan<Reading>(requestData)).Unwrap();

            var request = PollRequest(server);
            Assert.Equal(3, request.Length);
            var received = request.PayloadAsReadOnlySpan;
            for (int i = 0; i < requestData.Length; i++)
            {
                Assert.Equal(requestData[i].Id, received[i].Id);
                Assert.Equal(requestData[i].Value, received[i].Value);
            }

            var responseData = new[]
            {
                new Reading { Id = 70, Value = 70.5 },
                new Reading { Id = 80, Value = 80.5 },
            };
            request.SendCopyResponse(new ReadOnlySpan<Reading>(responseData)).Unwrap();
            request.Dispose();

            var response = pending.TimedReceive(TimeSpan.FromSeconds(5)).Unwrap();
            Assert.NotNull(response);
            using (response)
            {
                Assert.Equal(2, response!.Length);
                var responseReceived = response.PayloadAsReadOnlySpan;
                for (int i = 0; i < responseData.Length; i++)
                {
                    Assert.Equal(responseData[i].Id, responseReceived[i].Id);
                    Assert.Equal(responseData[i].Value, responseReceived[i].Value);
                }
            }
        }

        [Fact]
        public void DynamicRequestWithFixedResponse_RoundTrips()
        {
            using var node = NodeBuilder.New().Name("rr_slice_asymmetric").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Ack>()
                .EnableDynamicRequestPayloads()
                .InitialMaxRequestSliceLen(32)
                .Open(UniqueName("rr_slice_asymmetric"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            var requestMut = client.LoanSlice(4).Unwrap();
            var requestSpan = requestMut.PayloadAsSpan;
            for (int i = 0; i < requestSpan.Length; i++)
            {
                requestSpan[i] = new Reading { Id = i, Value = i };
            }
            using var pending = requestMut.Send().Unwrap();

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            Request<Reading, Ack>? request = null;
            while (DateTime.UtcNow < deadline && request == null)
            {
                request = server.Receive().Unwrap();
                if (request == null) System.Threading.Thread.Sleep(10);
            }
            Assert.NotNull(request);

            using (request)
            {
                Assert.Equal(4, request!.Length);
                // The fixed-size response side keeps the single-element API.
                request.SendCopyResponse(new Ack { Count = request.Length }).Unwrap();
            }

            var response = pending.TimedReceive(TimeSpan.FromSeconds(5)).Unwrap();
            Assert.NotNull(response);
            using (response)
            {
                Assert.Equal(1, response!.Length);
                Assert.Equal(4, response.Payload.Count);
            }
        }

        [Fact]
        public void LoanBeyondInitialMaxSliceLen_ReturnsError()
        {
            using var node = NodeBuilder.New().Name("rr_slice_overflow").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Reading>()
                .EnableDynamicPayloads()
                .InitialMaxSliceLen(4)
                .Open(UniqueName("rr_slice_overflow"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();

            var result = client.LoanSlice(1024);

            Assert.False(result.IsOk);
        }

        [Fact]
        public void LoanSliceOfZero_Throws()
        {
            using var node = NodeBuilder.New().Name("rr_slice_zero").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Reading>()
                .EnableDynamicPayloads()
                .InitialMaxSliceLen(8)
                .Open(UniqueName("rr_slice_zero"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();

            Assert.Throws<ArgumentException>(() => client.LoanSlice(0));
        }

        [Fact]
        public void FixedSizeService_KeepsSingleElementSemantics()
        {
            using var node = NodeBuilder.New().Name("rr_slice_fixed").Create().Unwrap();

            using var service = node.ServiceBuilder()
                .RequestResponse<Reading, Reading>()
                .Open(UniqueName("rr_slice_fixed"))
                .Unwrap();

            using var client = service.CreateClient().Unwrap();
            using var server = service.CreateServer().Unwrap();

            var requestMut = client.Loan().Unwrap();
            Assert.Equal(1, requestMut.Length);
            requestMut.Payload = new Reading { Id = 42, Value = 4.2 };
            using var pending = requestMut.Send().Unwrap();

            var (requestLength, responseLength) = RoundTrip(
                server,
                pending,
                request =>
                {
                    Assert.Equal(42, request.Payload.Id);
                    request.SendCopyResponse(new Reading { Id = 43, Value = 4.3 }).Unwrap();
                });

            Assert.Equal(1, requestLength);
            Assert.Equal(1, responseLength);
        }
    }
}
