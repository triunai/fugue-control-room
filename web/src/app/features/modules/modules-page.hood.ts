import { HoodNote } from '../hood/hood-note';

export const MODULES_HOOD: HoodNote = {
  why: [
    'A module is a feature area that can be switched off without removing code: a configuration kill switch plus the runtime switch here.',
    'Off means gone: its endpoints answer 404 module.disabled (a 403 would confirm the feature exists) and its permissions leave /me.',
  ],
  flow: [
    {
      layer: 'web',
      text: 'The list seeds each module’s version as its ETag; flipping a switch sends PATCH /modules/{code} with that If-Match.',
      path: 'web/src/app/api/modules.api.ts',
    },
    {
      layer: 'api',
      text: 'ModulesController.SetActive is a one-liner behind MODULES_MANAGE; [IfMatch] puts the header in the contract.',
      path: 'src/Fugue.Api/Controllers/ModulesController.cs',
    },
    {
      layer: 'handler',
      text: 'SetModuleActiveHandler refuses a missing If-Match (428) and switching SYSTEM off (409 module.system_protected).',
      path: 'src/Fugue.Application/Features/Modules/Commands/SetModuleActive.cs',
    },
    {
      layer: 'db',
      text: 'ModuleRegistry compares the version, then saves through EF with xmin as the concurrency token: a stale write is 412.',
      path: 'src/Fugue.Infrastructure/Identity/ModuleRegistry.cs',
    },
    {
      layer: 'audit',
      text: 'The same SaveChanges writes the module’s audit row (is_active old and new) through the audit interceptor.',
      path: 'src/Fugue.Infrastructure/Audit/AuditLogSaveChangesInterceptor.cs',
    },
    {
      layer: 'api',
      text: 'Every later request: ModuleGateMiddleware, after authentication and before authorization, answers 404 for an off module.',
      path: 'src/Fugue.Api/Modules/ModuleGateMiddleware.cs',
    },
    {
      layer: 'db',
      text: 'The permission query drops the permissions of a module that is off, so menus and checks follow on the next /me.',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/identity/user-access.sql',
    },
  ],
  how: [
    'Calls GET /modules (MODULES_VIEW): configuration, database and effective state; PATCH /modules/{code} (MODULES_MANAGE) flips is_active.',
    'PATCH needs If-Match: missing is 428, stale is 412; success is 204 with the new ETag.',
    'Effective = configuration Modules:<CODE>:Enabled AND is_active AND not deleted; configuration off wins over the database.',
    'A request refused by the gate is logged and written to access_audit; anonymous callers still get the 401.',
  ],
  adrs: [
    {
      label: 'ADR-009 Module management: kill switch plus runtime registry',
      path: 'docs/adr/ADR-009-module-management.md',
    },
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/ModuleManagementTests.cs',
      name: 'Database_switch_round_trip_disabled_is_404_module_disabled_then_back',
      asserts: 'Switching a module off makes its endpoints 404 module.disabled; switching it back on restores them.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/ModuleManagementTests.cs',
      name: 'Configuration_off_wins_over_an_active_database_row',
      asserts: 'A module off in configuration stays off even when its database row is active.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/ModuleManagementTests.cs',
      name: 'Toggle_needs_a_current_if_match_and_protects_system_and_unknown_codes',
      asserts: 'The toggle needs a current If-Match, refuses to switch SYSTEM off and 404s an unknown code.',
    },
    {
      layer: 'api',
      path: 'bruno/fugue/modules/03-customers-while-off.bru',
      name: 'Customers answer 404 module.disabled while the module is off',
      asserts: 'With CUSTOMERS off, the customers endpoints answer 404 module.disabled.',
    },
    {
      layer: 'arch',
      path: 'tests/Fugue.ArchTests/FitnessTests.cs',
      name: 'Fitness_Modules_every_feature_controller_carries_exactly_one_Module_attribute',
      asserts: 'Every feature controller carries exactly one [Module], so the gate covers every feature endpoint.',
    },
  ],
  decisions: [
    { label: 'D-023 Module management (config kill switch + runtime registry)', path: 'docs/decisions.md' },
    { label: 'D-036 S3 hardening, incl. fail-closed module config', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '404',
      name: 'module disabled',
      path: 'src/Fugue.Api/Modules/ModuleGateMiddleware.cs',
      code: `if (module is not null
    && context.User.Identity?.IsAuthenticated == true
    && !await context.RequestServices.GetRequiredService<IModuleRegistry>().IsEffectivelyEnabledAsync(module.Code, context.RequestAborted))
…
    await AuthProblems.WriteAsync(context, StatusCodes.Status404NotFound, "module.disabled", "The requested resource was not found.");`,
      note: 'Never 403: that would confirm the feature exists.',
    },
    {
      status: '409',
      name: 'system protected',
      path: 'src/Fugue.Application/Features/Modules/Commands/SetModuleActive.cs',
      code: `if (!command.Active && code == ModuleCodes.System)
{
    return Result.Fail<ModuleDto>(ModuleErrors.SystemProtected());
}`,
      note: 'SYSTEM holds module management itself, so it cannot be switched off.',
    },
    {
      status: '412',
      name: 'stale module',
      path: 'src/Fugue.Infrastructure/Identity/ModuleRegistry.cs',
      code: `if (expectedVersion != module.RowVersion)
{
    return Result.Fail<ModuleDto>(ModuleErrors.VersionMismatch());
}`,
      note: 'Someone switched it since you loaded the list: reload, then retry.',
    },
    {
      status: 'rule',
      name: 'configuration wins',
      path: 'src/Fugue.Infrastructure/Identity/ModuleRegistry.cs',
      code: `return !config.DisabledCodes().Contains(key)
    && await db.Modules.AsNoTracking().AnyAsync(m => m.Code == key && m.IsActive && !m.IsDeleted, ct);`,
    },
  ],
};
