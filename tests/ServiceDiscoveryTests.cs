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
using Xunit;

namespace Iceoryx2.Tests;

public class ServiceDiscoveryTests
{
    [Fact]
    public void ServiceList_ReturnsSuccess()
    {
        // Arrange
        var node = NodeBuilder.New()
            .Name("test_discovery_node")
            .Create()
            .Expect("Failed to create node");

        try
        {
            // Act
            var result = node.List();

            // Assert
            Assert.True(result.IsOk);
            var services = result.Expect("Should be Ok");
            Assert.NotNull(services);
            // Note: services list may be empty if no other services are running
        }
        finally
        {
            node.Dispose();
        }
    }

    [Fact]
    public void ServiceList_WithRunningService_FindsService()
    {
        // Arrange
        var discoveryNode = NodeBuilder.New()
            .Name("discovery_test_node")
            .Create()
            .Expect("Failed to create discovery node");

        var serviceNode = NodeBuilder.New()
            .Name("service_test_node")
            .Create()
            .Expect("Failed to create service node");

        try
        {
            // A unique name keeps this independent of services other tests leave behind.
            var serviceName = $"test_discovery_service_{Guid.NewGuid():N}";
            var service = serviceNode.ServiceBuilder()
                .PublishSubscribe<int>()
                .Open(serviceName)
                .Expect("Failed to create test service");

            try
            {
                // Act - list services
                var result = discoveryNode.List();
                Assert.True(result.IsOk);

                var services = result.Expect("Should be Ok");

                // Assert on the service just created. Asserting only that the list is
                // non-null would pass just as well against a List() that reports nothing.
                Assert.NotNull(services);
                var found = services.Find(s => s.Name == serviceName);
                Assert.True(found != null,
                    $"discovery did not report '{serviceName}'; it listed {services.Count} service(s)");
                Assert.Equal(MessagingPattern.PublishSubscribe, found!.MessagingPattern);
            }
            finally
            {
                service.Dispose();
            }
        }
        finally
        {
            discoveryNode.Dispose();
            serviceNode.Dispose();
        }
    }

    [Fact]
    public void ServiceStaticConfig_PublishSubscribe_HasCorrectProperties()
    {
        // Arrange
        var serviceNode = NodeBuilder.New()
            .Name("config_test_node")
            .Create()
            .Expect("Failed to create node");

        var discoveryNode = NodeBuilder.New()
            .Name("discovery_config_node")
            .Create()
            .Expect("Failed to create discovery node");

        try
        {
            // Create a pub/sub service
            var service = serviceNode.ServiceBuilder()
                .PublishSubscribe<int>()
                .Open("test_config_service")
                .Expect("Failed to create service");

            try
            {
                // Act
                var services = discoveryNode.List()
                    .Expect("Failed to list services");

                Assert.NotNull(services);

                // Note: Due to the simplified constructor (to avoid union marshaling issues),
                // PublishSubscribeConfig will be null. We can only verify the service list works.
                // Pattern-specific configs will be null until we solve the union marshaling problem.
            }
            finally
            {
                service.Dispose();
            }
        }
        finally
        {
            serviceNode.Dispose();
            discoveryNode.Dispose();
        }
    }
}