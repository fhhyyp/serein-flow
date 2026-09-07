namespace SereinFlow.Contracts;

public static class RunErrorCodes
{
    public const string NotFound = "run.not_found";
    public const string Cancelled = "run.cancelled";
    public const string TimedOut = "run.timed_out";
    public const string Failed = "run.failed";
    public const string Completed = "run.completed";
    public const string Interrupted = "run.interrupted";
    public const string Started = "run.started";
    public const string StatusInvalid = "run.status_invalid";
    public const string OperatorInterrupted = "run.operator_interrupted";
    public const string QueueFull = "run.queue_full";
    public const string SchedulerUnavailable = "run.scheduler_unavailable";
    public const string WorkerLost = "run.worker_lost";
    public const string QueueTimeout = "run.queue_timeout";
    public const string CandidateDefinitionNotAllowed = "run.candidate_definition_not_allowed";
    public const string Read = "run.read";
}

public static class WorkerErrorCodes
{
    public const string NotActive = "worker.not_active";
    public const string Cancelled = "worker.cancelled";
    public const string TimedOut = "worker.timed_out";
    public const string RunnerNotFound = "worker.runner_not_found";
    public const string ProtocolMismatch = "worker.protocol_mismatch";
    public const string MessageTooLarge = "worker.message_too_large";
    public const string InvalidMessage = "worker.invalid_message";
    public const string PathOutsideRoot = "worker.path_outside_root";
    public const string Crashed = "worker.crashed";
    public const string HandshakeFailed = "worker.handshake_failed";
    public const string NotFound = "worker.not_found";
    public const string EventSequenceInvalid = "worker.event_sequence_invalid";
    public const string InvalidResult = "worker.invalid_result";
    public const string TransportClosed = "worker.transport_closed";
    public const string HandshakeRequired = "worker.handshake_required";
    public const string RunRequired = "worker.run_required";
    public const string RunFailed = "worker.run_failed";
    public const string TransportReceiveConcurrent = "worker.transport_receive_concurrent";
    public const string TransportSendFailed = "worker.transport_send_failed";
    public const string TransportReceiveFailed = "worker.transport_receive_failed";
    public const string TransportInvalidFrame = "worker.transport_invalid_frame";
    public const string Ready = "worker.ready";
    public const string Handshake = "worker.handshake";
    public const string HandshakeAccepted = "worker.handshake.accepted";
    public const string Run = "worker.run";
    public const string Cancel = "worker.cancel";
    public const string CancelAck = "worker.cancel.ack";
    public const string DebugContinue = "worker.debug.continue";
    public const string DebugStep = "worker.debug.step";
    public const string DebugStop = "worker.debug.stop";
    public const string Heartbeat = "worker.heartbeat";
    public const string HeartbeatAck = "worker.heartbeat.ack";
    public const string Event = "worker.event";
    public const string Result = "worker.result";
    public const string Error = "worker.error";
    public const string PayloadTooLarge = "worker.payload_too_large";
    public const string InvalidPayload = "worker.invalid_payload";
}

public static class WorkerDiagnosticCodes
{
    public const string RunnerPath = "runner.path";
    public const string RunnerStdoutNoise = "runner.stdout_noise";
    public const string RunnerStderr = "runner.stderr";
    public const string DebugProtocol = "debug.protocol";
}

public static class MessageErrorCodes
{
    public const string TopicInvalid = "message.topic_invalid";
    public const string PayloadRequired = "message.payload_required";
    public const string ChannelInvalid = "message.channel_invalid";
    public const string IdInvalid = "message.id_invalid";
    public const string EndpointForbidden = "message.endpoint_forbidden";
    public const string ProtocolMismatch = "message.protocol_mismatch";
    public const string RunMismatch = "message.run_mismatch";
    public const string IdRequired = "message.id_required";
    public const string TopicRequired = "message.topic_required";
    public const string ExternalJsonRequired = "message.external_json_required";
    public const string EndpointNotReady = "message.endpoint_not_ready";
    public const string CorrelationConflict = "message.correlation_conflict";
    public const string DeliveryTimeout = "message.delivery_timeout";
    public const string EndpointInvalid = "message.endpoint_invalid";
    public const string ReceiptInvalid = "message.receipt_invalid";
    public const string ReceiptMismatch = "message.receipt_mismatch";
    public const string EndpointJsonRequired = "message.endpoint_json_required";
    public const string ContractMismatch = "message.contract_mismatch";
    public const string PayloadTooLarge = "message.payload_too_large";
    public const string PayloadInvalid = "message.payload_invalid";
    public const string Expired = "message.expired";
    public const string ChannelFull = "message.channel_full";
    public const string QueueFull = "message.queue_full";
    public const string QueueClosed = "message.queue_closed";
    public const string EventFull = "message.event_full";
    public const string SerializationFailed = "message.serialization_failed";
    public const string DeserializationNull = "message.deserialization_null";
    public const string DeserializationFailed = "message.deserialization_failed";
    public const string ChannelOptionsConflict = "message.channel_options_conflict";
    public const string ServiceClosed = "message.service_closed";
    public const string SubscriptionClosed = "message.subscription_closed";
    public const string Rejected = "message.rejected";
    public const string Deliver = "message.deliver";
    public const string Accepted = "message.accepted";
    public const string Register = "message.register";
    public const string Unregister = "message.unregister";
    public const string TypeMismatch = "message.type_mismatch";
}

public static class DebugErrorCodes
{
    public const string InvalidCommandSequence = "debug.invalid_command_sequence";
    public const string CommandSequenceConflict = "debug.command_sequence_conflict";
    public const string InvalidTriggerQueueLimit = "debug.invalid_trigger_queue_limit";
    public const string SessionAlreadyActive = "debug.session_already_active";
    public const string ExecutionCapacityFull = "debug.execution_capacity_full";
    public const string WorkerStartFailed = "debug.worker_start_failed";
    public const string ApiRestart = "debug.api_restart";
    public const string SessionNotActive = "debug.session_not_active";
    public const string SessionNotFound = "debug.session_not_found";
    public const string SessionTerminal = "debug.session_terminal";
    public const string SessionNotPaused = "debug.session_not_paused";
    public const string Stop = "debug.stop";
    public const string CommandRejected = "debug.command_rejected";
    public const string WorkerMonitorFailed = "debug.worker_monitor_failed";
    public const string Paused = "debug.paused";
    public const string TriggerReceived = "debug.trigger.received";
    public const string TriggerQueued = "debug.trigger.queued";
    public const string TriggerAdmitted = "debug.trigger.admitted";
    public const string TriggerRejected = "debug.trigger.rejected";
    public const string TriggerCompleted = "debug.trigger.completed";
    public const string TriggerFailed = "debug.trigger.failed";
    public const string Supervisor = "debug.supervisor";
    public const string InvalidStateRevision = "debug.invalid_state_revision";
    public const string InvalidWaitTimeout = "debug.invalid_wait_timeout";
    public const string BreakpointNodeMissing = "debug.breakpoint_node_missing";
    public const string SessionStart = "debug.session.start";
    public const string StartRejected = "debug.start_rejected";
    public const string TriggerQueueFull = "debug.trigger.queue_full";
    public const string TriggerExecutionFailed = "debug.trigger.execution_failed";
    public const string Read = "debug.read";
    public const string Control = "debug.control";
}

public static class NodeErrorCodes
{
    public const string Started = "node.started";
    public const string Completed = "node.completed";
    public const string Failed = "node.failed";
    public const string Error = "node.error";
    public const string MessageTitle = "node.message.title";
    public const string MessageSubtitle = "node.message.subtitle";
    public const string MessageActionTitle = "node.message.action.title";
    public const string MessageActionSubtitle = "node.message.action.subtitle";
    public const string Ssc = "node.ssc";
    public const string Log = "node.log";
    public const string InputInvalid = "node.input_invalid";
    public const string InputMissing = "node.input_missing";
    public const string BranchFailure = "node.branch_failure";
    public const string BranchError = "node.branch_error";
    public const string ExecutionFailed = "node.execution_failed";
    public const string DuplicateParameterName = "node.duplicate_parameter_name";
    public const string MissingRequiredParameter = "node.missing_required_parameter";
    public const string EnumLiteralInvalid = "node.enum_literal_invalid";
    public const string TypeRemoved = "node.type_removed";
}

public static class LibraryErrorCodes
{
    public const string NotAllowed = "library.not_allowed";
    public const string ServiceDependencyForbidden = "library.service_dependency_forbidden";
    public const string Archived = "library.archived";
    public const string UpgradeSourceNotReferenced = "library.upgrade_source_not_referenced";
    public const string UpgradeFlowNotUsingSource = "library.upgrade_flow_not_using_source";
    public const string UpgradeParameterRemoved = "library.upgrade_parameter_removed";
    public const string UpgradeParameterRemovedUnused = "library.upgrade_parameter_removed_unused";
    public const string UpgradeSourceContractUnknown = "library.upgrade_source_contract_unknown";
    public const string UpgradeManifestMissing = "library.upgrade_manifest_missing";
    public const string UpgradeSourceNotUsed = "library.upgrade_source_not_used";
    public const string UpgradeNodeRemoved = "library.upgrade_node_removed";
    public const string UpgradeNodeMatchUnknown = "library.upgrade_node_match_unknown";
    public const string UpgradeNodeExecutionChanged = "library.upgrade_node_execution_changed";
    public const string UpgradeReturnTypeChanged = "library.upgrade_return_type_changed";
    public const string UpgradeParameterRenamed = "library.upgrade_parameter_renamed";
    public const string UpgradeParameterChanged = "library.upgrade_parameter_changed";
    public const string UpgradeRequiredParameterAdded = "library.upgrade_required_parameter_added";
    public const string UpgradeExact = "library.upgrade_exact";
    public const string Md = "library.md";
    public const string Build = "library.build";
    public const string Zip = "library.zip";
    public const string Metadata = "library.metadata";
    public const string Import = "library.import";
    public const string NotFound = "library.not_found";
    public const string FlipflopReturnTypeInvalid = "library.flipflop_return_type_invalid";
    public const string UpgradeFlowRequired = "library.upgrade_flow_required";
    public const string UpgradeNotFound = "library.upgrade_not_found";
    public const string UpgradeNotApplicable = "library.upgrade_not_applicable";
    public const string UpgradeFlowNotInPlan = "library.upgrade_flow_not_in_plan";
    public const string UpgradeFlowAlreadyApplied = "library.upgrade_flow_already_applied";
    public const string UpgradeBlocked = "library.upgrade_blocked";
    public const string UpgradeConfirmationRequired = "library.upgrade_confirmation_required";
    public const string UpgradeFlowInvalid = "library.upgrade_flow_invalid";
    public const string UpgradeDuplicateFlow = "library.upgrade_duplicate_flow";
    public const string UpgradeFamilyUnassigned = "library.upgrade_family_unassigned";
    public const string UpgradeFamilyMismatch = "library.upgrade_family_mismatch";
    public const string UpgradeTargetContractUnknown = "library.upgrade_target_contract_unknown";
    public const string UpgradeTargetCatalogInvalid = "library.upgrade_target_catalog_invalid";
    public const string MetadataMissing = "library.metadata_missing";
    public const string IdentifierInvalid = "library.identifier_invalid";
    public const string AssemblyInvalid = "library.assembly_invalid";
    public const string TypeInvalid = "library.type_invalid";
    public const string MethodInvalid = "library.method_invalid";
    public const string MethodNotFound = "library.method_not_found";
    public const string MethodAmbiguous = "library.method_ambiguous";
    public const string TypeNotFound = "library.type_not_found";
    public const string NativeLoaderUnavailable = "library.native_loader_unavailable";
    public const string RootMissing = "library.root_missing";
    public const string AssemblyNotFound = "library.assembly_not_found";
    public const string AssemblyAmbiguous = "library.assembly_ambiguous";
    public const string AssemblyPathInvalid = "library.assembly_path_invalid";
    public const string PackageEmpty = "library.package_empty";
    public const string PackageDuplicateEntry = "library.package_duplicate_entry";
    public const string PackagePathInvalid = "library.package_path_invalid";
    public const string PathInvalid = "library.path_invalid";
    public const string ResultConverterScopeMissing = "library.result_converter_scope_missing";
    public const string InvocationFailed = "library.invocation_failed";
    public const string ResultConverterInvalid = "library.result_converter_invalid";
    public const string ResultConverterInputInvalid = "library.result_converter_input_invalid";
    public const string ResultConversionFailed = "library.result_conversion_failed";
    public const string FamilyAssign = "library.family.assign";
    public const string IdRequired = "library.id_required";
    public const string FamilyNotFound = "library.family_not_found";
    public const string FamilyNameRequired = "library.family_name_required";
    public const string Package = "library.package";
    public const string Read = "library.read";
    public const string Manage = "library.manage";
    public const string NativePathInvalid = "library.native_path_invalid";
    public const string NativeDirectoryInvalid = "library.native_directory_invalid";
    public const string NativeDirectoryMissing = "library.native_directory_missing";
    public const string NativeDirectoryScanFailed = "library.native_directory_scan_failed";
    public const string NativeDirectoryEmpty = "library.native_directory_empty";
    public const string NativeLoadFailed = "library.native_load_failed";
    public const string PackageManifestMissing = "library.package_manifest_missing";
    public const string PackageNodeRemoved = "library.package_node_removed";
    public const string PackageNodeMatchUnknown = "library.package_node_match_unknown";
    public const string PackageNodeExact = "library.package_node_exact";
    public const string PackageRequiredParameterAdded = "library.package_required_parameter_added";
    public const string PackageExact = "library.package_exact";
    public const string PackageNodeExecutionChanged = "library.package_node_execution_changed";
    public const string PackageReturnTypeChanged = "library.package_return_type_changed";
    public const string PackageParameterRemoved = "library.package_parameter_removed";
    public const string PackageParameterTypeChanged = "library.package_parameter_type_changed";
    public const string PackageParameterMappingRequired = "library.package_parameter_mapping_required";
    public const string PackageParameterMetadataChanged = "library.package_parameter_metadata_changed";
    public const string ServiceProviderFailed = "library.service_provider_failed";
    public const string ServiceActivationFailed = "library.service_activation_failed";
    public const string ResultConverterActivationFailed = "library.result_converter_activation_failed";
    public const string ServiceValidationFailed = "library.service_validation_failed";
    public const string ServiceNodeTypeInvalid = "library.service_node_type_invalid";
    public const string ServiceDiscoveryFailed = "library.service_discovery_failed";
    public const string ResultConverterDiscoveryFailed = "library.result_converter_discovery_failed";
    public const string ServiceLifetimeInvalid = "library.service_lifetime_invalid";
    public const string ServiceLifetimeConflict = "library.service_lifetime_conflict";
    public const string ServiceContractDuplicate = "library.service_contract_duplicate";
    public const string ServiceImplementationInvalid = "library.service_implementation_invalid";
    public const string ServiceConstructorInvalid = "library.service_constructor_invalid";
    public const string ServiceContractInvalid = "library.service_contract_invalid";
}

public static class DeviceErrorCodes
{
    public const string NotReady = "device.not_ready";
    public const string Faulted = "device.faulted";
}

public static class ProjectErrorCodes
{
    public const string Archived = "project.archived";
    public const string LibraryChanged = "project.library.changed";
    public const string LibraryAttach = "project.library.attach";
    public const string LibraryDetach = "project.library.detach";
    public const string NotFound = "project.not_found";
    public const string AlreadyExists = "project.already_exists";
    public const string ArchiveEnvironmentInterfaceExists = "project.archive_environment_interface_exists";
    public const string VersionConflict = "project.version_conflict";
    public const string Create = "project.create";
    public const string Read = "project.read";
    public const string Write = "project.write";
}

public static class ProjectLibraryErrorCodes
{
    public const string InUse = "project_library.in_use";
    public const string InUseByProductionHistory = "project_library.in_use_by_production_history";
    public const string NotReferenced = "project_library.not_referenced";
    public const string NodeMetadataInvalid = "project_library.node_metadata_invalid";
    public const string NodeContractInvalid = "project_library.node_contract_invalid";
    public const string ArtifactMissing = "project_library.artifact_missing";
    public const string AlreadyReferenced = "project_library.already_referenced";
}

public static class FlowErrorCodes
{
    public const string RunPolicyMissing = "flow.run_policy_missing";
    public const string RunPolicyInvalid = "flow.run_policy_invalid";
    public const string Invalid = "flow.invalid";
    public const string Md = "flow.md";
    public const string RunAlreadyActive = "flow.run_already_active";
    public const string NotFound = "flow.not_found";
    public const string VersionInvalid = "flow.version_invalid";
    public const string VersionConflict = "flow.version_conflict";
    public const string Changed = "flow.changed";
    public const string AlreadyExists = "flow.already_exists";
    public const string VersionTrackInvalid = "flow.version_track_invalid";
    public const string Patch = "flow.patch";
    public const string PublishNoChange = "flow.publish_no_change";
    public const string RollbackNoChange = "flow.rollback_no_change";
    public const string NodeMissing = "flow.node_missing";
    public const string DuplicateCanvasId = "flow.duplicate_canvas_id";
    public const string DuplicateNodeId = "flow.duplicate_node_id";
    public const string UnknownEntryNode = "flow.unknown_entry_node";
    public const string UnknownConnectionEndpoint = "flow.unknown_connection_endpoint";
    public const string DuplicateConnectionId = "flow.duplicate_connection_id";
    public const string Write = "flow.write";
    public const string StepLimitExceeded = "flow.step_limit_exceeded";
    public const string CycleDetected = "flow.cycle_detected";
    public const string ProductionVersionRequired = "flow.production_version_required";
}

public static class ScriptErrorCodes
{
    public const string SourceTooLarge = "script.source_too_large";
    public const string InputMissing = "script.input_missing";
    public const string InputUnknown = "script.input_unknown";
    public const string Cancelled = "script.cancelled";
    public const string DefinitionMissing = "script.definition_missing";
    public const string ValueUnsupported = "script.value_unsupported";
    public const string CompileFailed = "script.compile_failed";
    public const string RuntimeFailed = "script.runtime_failed";
    public const string OutputShapeInvalid = "script.output_shape_invalid";
    public const string ValueConversionFailed = "script.value_conversion_failed";
    public const string DuplicateNode = "script.duplicate_node";
    public const string NodeIdInvalid = "script.node_id_invalid";
    public const string InvalidSourceHash = "script.invalid_source_hash";
    public const string Compile = "script.compile";
    public const string CompileTimeout = "script.compile_timeout";
    public const string LanguageVersionUnsupported = "script.language_version_unsupported";
    public const string InputsTooMany = "script.inputs_too_many";
    public const string InputNameInvalid = "script.input_name_invalid";
    public const string CompileDiagnostic = "script.compile_diagnostic";
}

public static class FlowCallErrorCodes
{
    public const string ReturnTypeMismatch = "flowcall.return_type_mismatch";
    public const string ContextInvalid = "flowcall.context_invalid";
    public const string TargetMissing = "flowcall.target_missing";
    public const string ExecutorNotConfigured = "flowcall.executor_not_configured";
    public const string TargetFlowUnavailable = "flowcall.target_flow_unavailable";
    public const string TargetNotPublic = "flowcall.target_not_public";
    public const string TargetCanvasMismatch = "flowcall.target_canvas_mismatch";
    public const string ParameterBindingInvalid = "flowcall.parameter_binding_invalid";
    public const string ParameterBindingMissing = "flowcall.parameter_binding_missing";
    public const string CycleDetected = "flowcall.cycle_detected";
}

public static class FlipFlopErrorCodes
{
    public const string MetadataMissing = "flipflop.metadata_missing";
    public const string ExecutionFailed = "flipflop.execution_failed";
    public const string ExecutorInvalid = "flipflop.executor_invalid";
}

public static class WorkpieceErrorCodes
{
    public const string NotConfigured = "workpiece.not_configured";
    public const string UploadFailed = "workpiece.upload_failed";
    public const string TooLarge = "workpiece.too_large";
    public const string NameRequired = "workpiece.name_required";
    public const string NameInvalid = "workpiece.name_invalid";
    public const string NodeIdRequired = "workpiece.node_id_required";
    public const string NodeIdInvalid = "workpiece.node_id_invalid";
    public const string ExecutionIdRequired = "workpiece.execution_id_required";
    public const string ContentTypeInvalid = "workpiece.content_type_invalid";
}

public static class McpErrorCodes
{
    public const string Unauthenticated = "mcp.unauthenticated";
    public const string InvalidArguments = "mcp.invalid_arguments";
    public const string FlowPatchReferenceInvalid = "mcp.flow_patch.reference_invalid";
    public const string PostApplyVerificationFailed = "mcp.post_apply_verification_failed";
    public const string ValidationFailed = "mcp.validation_failed";
    public const string LibraryPackageInvalid = "mcp.library_package_invalid";
    public const string PreviewPackageUnavailable = "mcp.preview_package_unavailable";
    public const string LibraryAccessDenied = "mcp.library_access_denied";
    public const string AiGuidanceUriUnsupported = "mcp.ai_guidance_uri_unsupported";
    public const string AiGuidanceTooLarge = "mcp.ai_guidance_too_large";
    public const string AiGuidanceAccessDenied = "mcp.ai_guidance_access_denied";
    public const string AiGuidanceUnavailable = "mcp.ai_guidance_unavailable";
    public const string PreviewOwnerMismatch = "mcp.preview_owner_mismatch";
    public const string PreviewAccessDenied = "mcp.preview_access_denied";
    public const string ProjectAccessDenied = "mcp.project_access_denied";
    public const string InvalidPatchValue = "mcp.invalid_patch_value";
    public const string PreviewStatePersistFailed = "mcp.preview_state_persist_failed";
    public const string KeysManage = "mcp.keys.manage";
    public const string SessionRequired = "mcp.session_required";
    public const string ToolTimeout = "mcp.tool_timeout";
    public const string InternalError = "mcp.internal_error";
    public const string KeySetupAlreadyCompleted = "mcp.key_setup_already_completed";
    public const string KeyAlreadyRevoked = "mcp.key_already_revoked";
    public const string KeyNotFound = "mcp.key_not_found";
    public const string ProjectNotFound = "mcp.project_not_found";
    public const string ProjectArchived = "mcp.project_archived";
    public const string AdministratorRequired = "mcp.administrator_required";
    public const string PermissionDenied = "mcp.permission_denied";
    public const string PreviewNotFound = "mcp.preview_not_found";
    public const string PreviewFingerprintMismatch = "mcp.preview_fingerprint_mismatch";
    public const string PreviewNotPending = "mcp.preview_not_pending";
    public const string PreviewExpired = "mcp.preview_expired";
    public const string LibraryNodeTemplateProjectInvalid = "mcp.library_node_template.project_invalid";
    public const string LibraryNodeTemplateLibraryInvalid = "mcp.library_node_template.library_invalid";
    public const string LibraryNodeTemplateContractInvalid = "mcp.library_node_template.contract_invalid";
    public const string LibraryNodeTemplatePositionInvalid = "mcp.library_node_template.position_invalid";
    public const string LibraryNodeTemplateLibraryNotAttached = "mcp.library_node_template.library_not_attached";
    public const string LibraryNodeTemplateLibraryNotFound = "mcp.library_node_template.library_not_found";
    public const string LibraryNodeTemplateContractNotFound = "mcp.library_node_template.contract_not_found";
    public const string LibraryNodeTemplateNodeTypeUnsupported = "mcp.library_node_template.node_type_unsupported";
    public const string BuiltinNodeTemplateIdInvalid = "mcp.builtin_node_template.id_invalid";
    public const string BuiltinNodeTemplatePositionInvalid = "mcp.builtin_node_template.position_invalid";
    public const string BuiltinNodeTemplateNotFound = "mcp.builtin_node_template.not_found";
    public const string FlowPatchOperationsInvalid = "mcp.flow_patch.operations_invalid";
    public const string FlowPatchOperationRequired = "mcp.flow_patch.operation_required";
    public const string FlowPatchLegacyInput = "mcp.flow_patch.legacy_input";
    public const string FlowPatchOperationUnknown = "mcp.flow_patch.operation_unknown";
    public const string FlowPatchSchemaVersionInvalid = "mcp.flow_patch.schema_version_invalid";
    public const string FlowPatchSchemaVersionUnsupported = "mcp.flow_patch.schema_version_unsupported";
    public const string FlowPatchFieldRequired = "mcp.flow_patch.field_required";
    public const string FlowPatchPayloadInvalid = "mcp.flow_patch.payload_invalid";
    public const string FlowPatchDuplicateId = "mcp.flow_patch.duplicate_id";
    public const string FlowPatchUnexpectedField = "mcp.flow_patch.unexpected_field";
    public const string FlowPatchEnumEncodingInvalid = "mcp.flow_patch.enum_encoding_invalid";
    public const string FlowPatchFieldInvalid = "mcp.flow_patch.field_invalid";
    public const string IdempotencyConflict = "mcp.idempotency_conflict";
}

public static class DomainErrorCodes
{
    public const string EmptyName = "domain.empty_name";
}
