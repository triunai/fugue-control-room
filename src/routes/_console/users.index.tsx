import { createFileRoute, Link } from "@tanstack/react-router";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { PageHeader, LoadingRows, EmptyState, ErrorState } from "@/components/console/states";
import { StaffOnly } from "@/components/console/staff-only";
import { useMock } from "@/lib/console";
import { users, roles, type StaffUser } from "@/lib/mock";

export const Route = createFileRoute("/_console/users/")({
  head: () => ({ meta: [
    { title: "Users & access · Fugue Console" },
    { name: "description", content: "Staff users, their roles, groups and permission overrides." },
    { property: "og:title", content: "Users & access · Fugue Console" },
    { property: "og:description", content: "Manage who can do what in Fugue." },
  ] }),
  component: () => <StaffOnly><UsersList /></StaffOnly>,
});

function UsersList() {
  const res = useMock(() => users, [] as StaffUser[]);
  return (
    <>
      <PageHeader eyebrow="Identity" title="Users & access" desc="Roles grant permissions; overrides adjust them per person. Deny always wins." />
      {res.status === "loading" && <LoadingRows rows={5} />}
      {res.status === "error" && <ErrorState onRetry={res.reload} />}
      {res.status === "ok" && !res.data?.length && <EmptyState title="No users">Invite a teammate to get started.</EmptyState>}
      {res.status === "ok" && !!res.data?.length && (
        <div className="rounded-md border border-border">
          <Table>
            <TableHeader><TableRow><TableHead>User</TableHead><TableHead>Type</TableHead><TableHead>Roles</TableHead><TableHead>Overrides</TableHead><TableHead>Last seen</TableHead></TableRow></TableHeader>
            <TableBody>
              {res.data.map((u) => (
                <TableRow key={u.id} className="cursor-pointer">
                  <TableCell><Link to="/users/$userId" params={{ userId: u.id }} className="block"><p className="font-medium hover:text-primary">{u.name}</p><p className="text-xs text-muted-foreground">{u.email}</p></Link></TableCell>
                  <TableCell className="text-sm">{u.staff ? "Staff" : "Member"}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{u.roles.map((r) => roles.find((x) => x.id === r)?.name).join(", ") || "—"}</TableCell>
                  <TableCell className="font-mono text-xs">{u.overrides.length || "—"}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{u.lastSeen}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
