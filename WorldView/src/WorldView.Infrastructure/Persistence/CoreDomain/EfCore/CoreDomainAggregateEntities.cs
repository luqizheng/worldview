namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

// These are storage-only shapes. Their property names intentionally match SQLite column names;
// the Domain model remains independent of EF Core and database naming conventions.

internal sealed class CoreHistoryEntity
{
    public string history_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string history_kind { get; set; } = "";
    public string? owner_map_id { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreBranchEntity
{
    public string branch_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string parent_history_id { get; set; } = "";
    public string own_history_id { get; set; } = "";
    public long parent_day_number { get; set; }
    public long parent_offset_seconds { get; set; }
    public string branch_name { get; set; } = "";
    public string lifecycle_status { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreCalendarEntity
{
    public string calendar_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string map_id { get; set; } = "";
    public string calendar_role { get; set; } = "";
    public string? calendar_type { get; set; }
    public string calendar_name { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreCalendarConfigurationEntity
{
    public string calendar_configuration_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string map_id { get; set; } = "";
    public string calendar_id { get; set; } = "";
    public string owner_history_id { get; set; } = "";
    public string? source_command_id { get; set; }
    public string configuration_origin { get; set; } = "";
    public long effective_from_day_number { get; set; }
    public long effective_from_offset_seconds { get; set; }
    public long? effective_until_day_number { get; set; }
    public long? effective_until_offset_seconds { get; set; }
    public long rules_schema_version { get; set; }
    public string rules_payload_json { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectTypeEntity
{
    public string object_type_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string type_key { get; set; } = "";
    public string display_name { get; set; } = "";
    public long allocatable { get; set; }
    public string allocation_format_json { get; set; } = "";
    public long allocation_format_version { get; set; }
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectEntity
{
    public string object_identity_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string object_type_id { get; set; } = "";
    public long object_type_schema_version { get; set; }
    public string object_type_semantics_id { get; set; } = "";
    public string sharing_scope { get; set; } = "";
    public string? owner_map_id { get; set; }
    public string lifecycle_status { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectDefinitionEntity
{
    public string project_id { get; set; } = "";
    public string object_identity_id { get; set; } = "";
    public long definition_schema_version { get; set; }
    public string definition_payload_json { get; set; } = "";
    public string definition_hash { get; set; } = "";
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectIdAllocatorEntity
{
    public string project_id { get; set; } = "";
    public string object_type_id { get; set; } = "";
    public long next_sequence { get; set; }
    public long allocator_format_version { get; set; }
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectIdReservationEntity
{
    public string reservation_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string object_type_id { get; set; } = "";
    public string? object_identity_id { get; set; }
    public long allocated_sequence { get; set; }
    public string allocated_id_value { get; set; } = "";
    public string reservation_state { get; set; } = "";
    public string? request_key { get; set; }
    public string? abandon_reason { get; set; }
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreMapObjectRefEntity
{
    public string map_object_ref_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string map_id { get; set; } = "";
    public string object_identity_id { get; set; } = "";
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreMapInitialStateEntity
{
    public string initial_state_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string map_id { get; set; } = "";
    public long baseline_schema_version { get; set; }
    public string content_fingerprint { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreMapInitialObjectEntity
{
    public string project_id { get; set; } = "";
    public string map_id { get; set; } = "";
    public string initial_state_id { get; set; } = "";
    public string object_identity_id { get; set; } = "";
    public string initial_membership_payload_json { get; set; } = "";
    public string? initial_placement_payload_json { get; set; }
    public long? initial_placement_schema_version { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreObjectDefinitionAssetEntity
{
    public string object_definition_asset_id { get; set; } = "";
    public string project_id { get; set; } = "";
    public string object_identity_id { get; set; } = "";
    public string asset_id { get; set; } = "";
    public string role { get; set; } = "";
    public long created_at_utc_ms { get; set; }
}
