namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

// These are storage-only shapes. Their property names intentionally match SQLite column names;
// the Domain model remains independent of EF Core and database naming conventions.

internal sealed class CoreHistoryEntity
{
    public Guid history_id { get; set; }
    public Guid project_id { get; set; }
    public string history_kind { get; set; } = "";
    public Guid? owner_map_id { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreBranchEntity
{
    public Guid branch_id { get; set; }
    public Guid project_id { get; set; }
    public Guid parent_history_id { get; set; }
    public Guid own_history_id { get; set; }
    public long parent_day_number { get; set; }
    public long parent_offset_seconds { get; set; }
    public string branch_name { get; set; } = "";
    public string lifecycle_status { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreCalendarEntity
{
    public Guid calendar_id { get; set; }
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public string calendar_role { get; set; } = "";
    public string? calendar_type { get; set; }
    public string calendar_name { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreCalendarConfigurationEntity
{
    public Guid calendar_configuration_id { get; set; }
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public Guid calendar_id { get; set; }
    public Guid owner_history_id { get; set; }
    public Guid? source_command_id { get; set; }
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
    public Guid object_type_id { get; set; }
    public Guid project_id { get; set; }
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
    public Guid object_identity_id { get; set; }
    public Guid project_id { get; set; }
    public Guid object_type_id { get; set; }
    public long object_type_schema_version { get; set; }
    public string object_type_semantics_id { get; set; } = "";
    public string sharing_scope { get; set; } = "";
    public Guid? owner_map_id { get; set; }
    public string lifecycle_status { get; set; } = "";
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectDefinitionEntity
{
    public Guid project_id { get; set; }
    public Guid object_identity_id { get; set; }
    public long definition_schema_version { get; set; }
    public string definition_payload_json { get; set; } = "";
    public string definition_hash { get; set; } = "";
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectIdAllocatorEntity
{
    public Guid project_id { get; set; }
    public Guid object_type_id { get; set; }
    public long next_sequence { get; set; }
    public long allocator_format_version { get; set; }
    public long created_at_utc_ms { get; set; }
    public long updated_at_utc_ms { get; set; }
}

internal sealed class CoreObjectIdReservationEntity
{
    public Guid reservation_id { get; set; }
    public Guid project_id { get; set; }
    public Guid object_type_id { get; set; }
    public Guid? object_identity_id { get; set; }
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
    public Guid map_object_ref_id { get; set; }
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public Guid object_identity_id { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreMapInitialStateEntity
{
    public Guid initial_state_id { get; set; }
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreObjectDefinitionAssetEntity
{
    public Guid object_definition_asset_id { get; set; }
    public Guid project_id { get; set; }
    public Guid object_identity_id { get; set; }
    public Guid asset_id { get; set; }
    public string role { get; set; } = "";
    public long created_at_utc_ms { get; set; }
}


// Immutable time-zero baseline revision snapshot and the sole mutable cursor row.
internal sealed class CoreMapInitialStateRevisionEntity
{
    public Guid revision_id { get; set; }
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public Guid initial_state_id { get; set; }
    public long revision_number { get; set; }
    public Guid? parent_revision_id { get; set; }
    public long baseline_schema_version { get; set; }
    public string content_fingerprint { get; set; } = "";
    public string? operation_kind { get; set; }
    public string? operation_payload_json { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreMapInitialStateRevisionMemberEntity
{
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public Guid initial_state_id { get; set; }
    public Guid revision_id { get; set; }
    public Guid object_identity_id { get; set; }
    public string initial_membership_payload_json { get; set; } = "";
    public string? initial_placement_payload_json { get; set; }
    public long? initial_placement_schema_version { get; set; }
    public long created_at_utc_ms { get; set; }
}

internal sealed class CoreMapInitialStateCursorEntity
{
    public Guid project_id { get; set; }
    public Guid map_id { get; set; }
    public Guid initial_state_id { get; set; }
    public Guid active_revision_id { get; set; }
    public Guid? selected_redo_child_revision_id { get; set; }
    public long cursor_version { get; set; }
    public long updated_at_utc_ms { get; set; }
}
