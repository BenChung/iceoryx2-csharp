using System.Runtime.InteropServices;

namespace Iceoryx2.Native.Interop
{
    internal enum iox2_type_variant_e
    {
        iox2_type_variant_e_FIXED_SIZE,
        iox2_type_variant_e_DYNAMIC,
    }

    internal enum iox2_messaging_pattern_e
    {
        iox2_messaging_pattern_e_PUBLISH_SUBSCRIBE = 0,
        iox2_messaging_pattern_e_EVENT,
        iox2_messaging_pattern_e_REQUEST_RESPONSE,
        iox2_messaging_pattern_e_BLACKBOARD,
    }

    internal partial struct iox2_attribute_set_h_t
    {
    }

    internal partial struct iox2_event_id_t
    {
        [NativeTypeName("size_t")]
        public nuint value;
    }

    internal unsafe partial struct iox2_type_detail_t
    {
        [NativeTypeName("enum iox2_type_variant_e")]
        public iox2_type_variant_e variant;

        [NativeTypeName("char[256]")]
        public fixed sbyte type_name[256];

        [NativeTypeName("size_t")]
        public nuint size;

        [NativeTypeName("size_t")]
        public nuint alignment;
    }

    internal partial struct iox2_static_config_blackboard_t
    {
        [NativeTypeName("size_t")]
        public nuint max_readers;

        [NativeTypeName("size_t")]
        public nuint max_writers;

        [NativeTypeName("size_t")]
        public nuint max_nodes;

        [NativeTypeName("struct iox2_type_detail_t")]
        public iox2_type_detail_t type_details;
    }

    internal partial struct iox2_static_config_event_t
    {
        [NativeTypeName("size_t")]
        public nuint max_notifiers;

        [NativeTypeName("size_t")]
        public nuint max_listeners;

        [NativeTypeName("size_t")]
        public nuint max_nodes;

        [NativeTypeName("size_t")]
        public nuint event_id_max_value;

        [NativeTypeName("size_t")]
        public nuint notifier_dead_event;

        [NativeTypeName("_Bool")]
        public byte has_notifier_dead_event;

        [NativeTypeName("size_t")]
        public nuint notifier_dropped_event;

        [NativeTypeName("_Bool")]
        public byte has_notifier_dropped_event;

        [NativeTypeName("size_t")]
        public nuint notifier_created_event;

        [NativeTypeName("_Bool")]
        public byte has_notifier_created_event;

        [NativeTypeName("uint64_t")]
        public ulong deadline_seconds;

        [NativeTypeName("uint32_t")]
        public uint deadline_nanoseconds;

        [NativeTypeName("_Bool")]
        public byte has_deadline;
    }

    internal partial struct iox2_message_type_details_t
    {
        [NativeTypeName("struct iox2_type_detail_t")]
        public iox2_type_detail_t header;

        [NativeTypeName("struct iox2_type_detail_t")]
        public iox2_type_detail_t user_header;

        [NativeTypeName("struct iox2_type_detail_t")]
        public iox2_type_detail_t payload;
    }

    internal partial struct iox2_static_config_publish_subscribe_t
    {
        [NativeTypeName("size_t")]
        public nuint max_subscribers;

        [NativeTypeName("size_t")]
        public nuint max_publishers;

        [NativeTypeName("size_t")]
        public nuint max_nodes;

        [NativeTypeName("size_t")]
        public nuint history_size;

        [NativeTypeName("size_t")]
        public nuint subscriber_max_buffer_size;

        [NativeTypeName("size_t")]
        public nuint subscriber_max_borrowed_samples;

        [NativeTypeName("_Bool")]
        public byte enable_safe_overflow;

        [NativeTypeName("struct iox2_message_type_details_t")]
        public iox2_message_type_details_t message_type_details;
    }

    internal partial struct iox2_static_config_request_response_t
    {
        [NativeTypeName("_Bool")]
        public byte enable_safe_overflow_for_requests;

        [NativeTypeName("_Bool")]
        public byte enable_safe_overflow_for_responses;

        [NativeTypeName("_Bool")]
        public byte enable_fire_and_forget_requests;

        [NativeTypeName("size_t")]
        public nuint max_active_requests_per_client;

        [NativeTypeName("size_t")]
        public nuint max_loaned_requests;

        [NativeTypeName("size_t")]
        public nuint max_response_buffer_size;

        [NativeTypeName("size_t")]
        public nuint max_servers;

        [NativeTypeName("size_t")]
        public nuint max_clients;

        [NativeTypeName("size_t")]
        public nuint max_nodes;

        [NativeTypeName("size_t")]
        public nuint max_borrowed_responses_per_pending_response;

        [NativeTypeName("struct iox2_message_type_details_t")]
        public iox2_message_type_details_t request_message_type_details;

        [NativeTypeName("struct iox2_message_type_details_t")]
        public iox2_message_type_details_t response_message_type_details;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal partial struct iox2_static_config_details_t
    {
        [FieldOffset(0)]
        [NativeTypeName("struct iox2_static_config_event_t")]
        public iox2_static_config_event_t @event;

        [FieldOffset(0)]
        [NativeTypeName("struct iox2_static_config_publish_subscribe_t")]
        public iox2_static_config_publish_subscribe_t publish_subscribe;

        [FieldOffset(0)]
        [NativeTypeName("struct iox2_static_config_request_response_t")]
        public iox2_static_config_request_response_t request_response;

        [FieldOffset(0)]
        [NativeTypeName("struct iox2_static_config_blackboard_t")]
        public iox2_static_config_blackboard_t blackboard;
    }

    internal unsafe partial struct iox2_static_config_t
    {
        [NativeTypeName("char[64]")]
        public fixed sbyte id[64];

        [NativeTypeName("char[255]")]
        public fixed sbyte name[255];

        [NativeTypeName("enum iox2_messaging_pattern_e")]
        public iox2_messaging_pattern_e messaging_pattern;

        [NativeTypeName("union iox2_static_config_details_t")]
        public iox2_static_config_details_t details;

        [NativeTypeName("iox2_attribute_set_h")]
        public iox2_attribute_set_h_t* attributes;
    }

    internal static partial class Iox2Constants
    {
        [NativeTypeName("#define IOX2_OK 0")]
        public const int IOX2_OK = 0;

        [NativeTypeName("#define IOX2_ATTRIBUTE_KEY_LENGTH 64")]
        public const int IOX2_ATTRIBUTE_KEY_LENGTH = 64;

        [NativeTypeName("#define IOX2_ATTRIBUTE_VALUE_LENGTH 256")]
        public const int IOX2_ATTRIBUTE_VALUE_LENGTH = 256;

        [NativeTypeName("#define IOX2_MAX_ATTRIBUTES_PER_SERVICE 8")]
        public const int IOX2_MAX_ATTRIBUTES_PER_SERVICE = 8;

        [NativeTypeName("#define IOX2_NODE_NAME_LENGTH 128")]
        public const int IOX2_NODE_NAME_LENGTH = 128;

        [NativeTypeName("#define IOX2_SERVICE_NAME_LENGTH 255")]
        public const int IOX2_SERVICE_NAME_LENGTH = 255;

        [NativeTypeName("#define IOX2_SERVICE_HASH_LENGTH 64")]
        public const int IOX2_SERVICE_HASH_LENGTH = 64;

        [NativeTypeName("#define IOX2_TYPE_NAME_LENGTH 256")]
        public const int IOX2_TYPE_NAME_LENGTH = 256;

        [NativeTypeName("#define IOX2_MAX_BLACKBOARD_KEY_SIZE 64")]
        public const int IOX2_MAX_BLACKBOARD_KEY_SIZE = 64;

        [NativeTypeName("#define IOX2_MAX_BLACKBOARD_KEY_ALIGNMENT 8")]
        public const int IOX2_MAX_BLACKBOARD_KEY_ALIGNMENT = 8;
    }
}
