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

using Iceoryx2.Native;
using System;
using System.Text;

namespace Iceoryx2;

/// <summary>
/// Messaging pattern types supported by iceoryx2 services.
/// </summary>
public enum MessagingPattern
{
    /// <summary>
    /// Publish-Subscribe pattern for one-to-many communication.
    /// </summary>
    PublishSubscribe = 0,

    /// <summary>
    /// Event pattern for asynchronous notifications.
    /// </summary>
    Event = 1,

    /// <summary>
    /// Request-Response pattern for synchronous two-way communication.
    /// </summary>
    RequestResponse = 2,

    /// <summary>
    /// Blackboard pattern for shared state.
    /// </summary>
    Blackboard = 3
}

/// <summary>
/// Static configuration information for an iceoryx2 service.
/// Contains metadata and settings that define the service characteristics.
/// </summary>
public class ServiceStaticConfig
{
    /// <summary>
    /// Gets the unique identifier of the service.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the name of the service.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the messaging pattern used by this service.
    /// </summary>
    public MessagingPattern MessagingPattern { get; }

    /// <summary>
    /// Gets the event-specific configuration if the service uses the Event pattern.
    /// </summary>
    public EventStaticConfig? EventConfig { get; }

    /// <summary>
    /// Gets the publish-subscribe configuration if the service uses the PublishSubscribe pattern.
    /// </summary>
    public PublishSubscribeStaticConfig? PublishSubscribeConfig { get; }

    /// <summary>
    /// Gets the request-response configuration if the service uses the RequestResponse pattern.
    /// </summary>
    public RequestResponseStaticConfig? RequestResponseConfig { get; }

    /// <summary>
    /// Gets the blackboard configuration if the service uses the Blackboard pattern.
    /// </summary>
    public BlackboardStaticConfig? BlackboardConfig { get; }

    /// <summary>
    /// Internal constructor for creating a service config from basic parameters.
    /// Used when the full native struct cannot be safely marshaled.
    /// </summary>
    internal ServiceStaticConfig(byte[] id, byte[] name, Iox2NativeMethods.iox2_messaging_pattern_e messagingPattern)
    {
        Id = ExtractString(id);
        Name = ExtractString(name);
        MessagingPattern = (MessagingPattern)messagingPattern;
        // Pattern-specific configs are null when using this simplified constructor
    }

    private static string ExtractString(byte[] bytes)
    {
        // Find the null terminator
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}

/// <summary>
/// Static configuration for Event messaging pattern services.
/// </summary>
public class EventStaticConfig
{
    /// <summary>
    /// Gets the maximum number of notifiers.
    /// </summary>
    public ulong MaxNotifiers { get; }

    /// <summary>
    /// Gets the maximum number of listeners.
    /// </summary>
    public ulong MaxListeners { get; }

    /// <summary>
    /// Gets the maximum number of nodes.
    /// </summary>
    public ulong MaxNodes { get; }

    /// <summary>
    /// Gets the maximum event ID value.
    /// </summary>
    public ulong EventIdMaxValue { get; }

    /// <summary>
    /// Gets the notifier dead event ID if configured.
    /// </summary>
    public ulong? NotifierDeadEvent { get; }

    /// <summary>
    /// Gets the notifier dropped event ID if configured.
    /// </summary>
    public ulong? NotifierDroppedEvent { get; }

    /// <summary>
    /// Gets the notifier created event ID if configured.
    /// </summary>
    public ulong? NotifierCreatedEvent { get; }

}

/// <summary>
/// Static configuration for PublishSubscribe messaging pattern services.
/// </summary>
public class PublishSubscribeStaticConfig
{
    /// <summary>
    /// Gets the maximum number of subscribers.
    /// </summary>
    public ulong MaxSubscribers { get; }

    /// <summary>
    /// Gets the maximum number of publishers.
    /// </summary>
    public ulong MaxPublishers { get; }

    /// <summary>
    /// Gets the maximum number of nodes.
    /// </summary>
    public ulong MaxNodes { get; }

    /// <summary>
    /// Gets the history size.
    /// </summary>
    public ulong HistorySize { get; }

    /// <summary>
    /// Gets the maximum subscriber buffer size.
    /// </summary>
    public ulong SubscriberMaxBufferSize { get; }

    /// <summary>
    /// Gets the maximum number of borrowed samples per subscriber.
    /// </summary>
    public ulong SubscriberMaxBorrowedSamples { get; }

    /// <summary>
    /// Gets whether safe overflow is enabled.
    /// </summary>
    public bool EnableSafeOverflow { get; }

}

/// <summary>
/// Static configuration for RequestResponse messaging pattern services.
/// </summary>
public class RequestResponseStaticConfig
{
    /// <summary>
    /// Gets whether safe overflow is enabled for requests.
    /// </summary>
    public bool EnableSafeOverflowForRequests { get; }

    /// <summary>
    /// Gets whether safe overflow is enabled for responses.
    /// </summary>
    public bool EnableSafeOverflowForResponses { get; }

    /// <summary>
    /// Gets whether fire-and-forget requests are enabled.
    /// </summary>
    public bool EnableFireAndForgetRequests { get; }

    /// <summary>
    /// Gets the maximum number of active requests per client.
    /// </summary>
    public ulong MaxActiveRequestsPerClient { get; }

    /// <summary>
    /// Gets the maximum number of loaned requests.
    /// </summary>
    public ulong MaxLoanedRequests { get; }

    /// <summary>
    /// Gets the maximum response buffer size.
    /// </summary>
    public ulong MaxResponseBufferSize { get; }

    /// <summary>
    /// Gets the maximum number of servers.
    /// </summary>
    public ulong MaxServers { get; }

    /// <summary>
    /// Gets the maximum number of clients.
    /// </summary>
    public ulong MaxClients { get; }

    /// <summary>
    /// Gets the maximum number of nodes.
    /// </summary>
    public ulong MaxNodes { get; }

    /// <summary>
    /// Gets the maximum number of borrowed responses per pending response.
    /// </summary>
    public ulong MaxBorrowedResponsesPerPendingResponse { get; }

}

/// <summary>
/// Static configuration for Blackboard messaging pattern services.
/// </summary>
public class BlackboardStaticConfig
{
    /// <summary>
    /// Gets the maximum number of readers.
    /// </summary>
    public ulong MaxReaders { get; }

    /// <summary>
    /// Gets the maximum number of writers.
    /// </summary>
    public ulong MaxWriters { get; }

    /// <summary>
    /// Gets the maximum number of nodes.
    /// </summary>
    public ulong MaxNodes { get; }

}