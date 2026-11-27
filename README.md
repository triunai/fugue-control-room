# Fugue Control Room

Design a desktop-first admin console called "Fugue Console" for a REST API platform.

Style: dark gothic-minimal (near-black background, bone/ivory text, one deep crimson accent,

serif display headings, clean sans body, subtle borders, no gradients), shadcn/ui + Tailwind.

Layout: left sidebar nav grouped by module, top bar with environment badge, user menu,

and a correlation-id "copy ref" chip on every error toast.

Screens:

1. Sign-in (email/password), with error states.

2. Customers: data table with keyset "Load more", search, status filter, row actions

   (edit, suspend/reactivate, delete with confirm), an edit drawer with field-level errors,

   and a "this record changed since you opened it (412)" conflict banner with Reload/Overwrite.

3. Users & access: users list → user detail with tabs Roles, Groups, Overrides

   (allow/deny chips with expiry dates; deny shown as winning).

4. Modules: toggle cards showing Config state, DB state, Effective state, and why disabled.

5. Audit log: filter bar (entity, actor, action, correlation id, date range), timeline list,

   detail panel with a field-level before/after diff (masked values shown as •••).

6. Webhooks: subscriptions list, create dialog that reveals the secret ONCE,

   deliveries table (status, attempts, next retry, last status code) with Redeliver.

7. A self-service "My profile" area showing what a non-staff user sees (fewer nav items).

Include empty, loading, error and permission-denied (404-style "not found") states.

This project was built with [Lovable](https://lovable.dev).

## Build with Lovable

Continue developing this project in the [Lovable editor](https://lovable.dev/projects/54172049-8322-4314-85cf-763d0409071f).

- **Ship faster**: describe what you want to build and Lovable handles the code.
- **Stay in sync**: every change made in Lovable is committed straight to this repository.
- **Full ownership**: this code is yours. Push to `main` on GitHub and your changes sync back into Lovable, ready for your next prompt.

## Development

Prefer working locally? You need Node.js and npm — [install with nvm](https://github.com/nvm-sh/nvm#installing-and-updating).

```sh
git clone <this-repository-url>
cd <repository-name>
npm i
npm run dev
```
