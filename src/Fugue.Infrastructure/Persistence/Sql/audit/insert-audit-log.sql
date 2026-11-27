-- Inserts one audit_log row (joins the unit of work, or runs on its own connection for a failure row). Caller: AuditTrail.
INSERT INTO audit_log (id, occurred_at, actor_user_id, actor_type, actor_display, tenant_id, permission_used, action, entity_type, entity_id,
                       outcome, error_code, error_type, error_message, duration_ms, changes, correlation_id, saga_id, trace_id, http_method, route_template, client_ip, user_agent, metadata)
VALUES (@Id, @OccurredAt, @ActorUserId, @ActorType, @ActorDisplay, @TenantId, @PermissionUsed, @Action, @EntityType, @EntityId,
        @Outcome, @ErrorCode, @ErrorType, @ErrorMessage, @DurationMs, CAST(@Changes AS jsonb), @CorrelationId, @SagaId, @TraceId, @HttpMethod, @RouteTemplate, @ClientIp, @UserAgent, CAST(@Metadata AS jsonb))
