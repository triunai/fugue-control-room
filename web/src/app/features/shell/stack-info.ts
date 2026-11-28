/** What Fugue is built on, as shown in the "Stack" dialog. Versions are the ones pinned in package.json,
 *  Directory.Packages.props and docker-compose.yml; stack-info.spec.ts fails when the Angular major drifts. */
export interface StackItem {
  readonly name: string;
  readonly version: string;
  readonly note?: string;
}
export interface StackGroup {
  readonly title: string;
  readonly items: readonly StackItem[];
}

export const STACK: readonly StackGroup[] = [
  {
    title: 'Web',
    items: [
      { name: 'Angular', version: '22', note: 'standalone components, signals' },
      { name: 'Angular CDK', version: '22' },
      { name: 'TanStack Query', version: '5', note: 'server state' },
      { name: 'NgRx Signals', version: '22', note: 'local stores' },
      { name: 'Univer', version: '1.6', note: 'the spreadsheet grid' },
      { name: 'FullCalendar', version: '7', note: 'SLA calendar' },
      { name: 'exceljs', version: '4.4', note: 'XLSX export' },
      { name: 'Tailwind CSS', version: '4' },
      { name: 'Vitest', version: '5', note: 'unit tests' },
      { name: 'Playwright', version: '1.65', note: 'end-to-end tests' },
    ],
  },
  {
    title: 'API',
    items: [
      { name: '.NET / ASP.NET Core', version: '8' },
      { name: 'EF Core + Npgsql', version: '8.0', note: 'Npgsql provider 8.0.11; Dapper 2.1 for reads' },
      { name: 'Mediator (source generated)', version: '3.0' },
      { name: 'FluentValidation', version: '12.2' },
      { name: 'Serilog', version: '9.0', note: 'structured logs' },
      { name: 'Confluent.Kafka', version: '2.16', note: 'optional adapter, OFF: claim events go out through the outbox as signed webhooks' },
      { name: 'Meilisearch client', version: '0.20', note: 'server v1.12' },
      { name: 'xUnit + Testcontainers', version: '2.9 / 4.17' },
    ],
  },
  {
    title: 'Data',
    items: [
      { name: 'PostgreSQL', version: '17', note: 'postgres:17-alpine in docker-compose.yml' },
      {
        name: 'Entity ids',
        version: 'UUID v7',
        note: 'time-ordered GUIDs, generated in the API (Uuid7.NewGuid), not by the database',
      },
    ],
  },
];
