import { useState } from 'react';
import { Plus, Trash2, Users } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';

type Role = 'Owner' | 'Admin' | 'QA Engineer' | 'Viewer';

interface Member {
  id: string;
  name: string;
  email: string;
  role: Role;
  status: 'Active' | 'Invited';
}

const seedMembers: Member[] = [
  { id: 'u1', name: 'Surya N', email: 'surya@company.com', role: 'Owner', status: 'Active' },
  { id: 'u2', name: 'Priya K', email: 'priya@company.com', role: 'Admin', status: 'Active' },
  { id: 'u3', name: 'Rahul V', email: 'rahul@company.com', role: 'QA Engineer', status: 'Invited' },
];

export function AdministrationPage() {
  const [members, setMembers] = useState(seedMembers);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ name: '', email: '', role: 'Viewer' as Role });

  function invite() {
    setMembers((prev) => [
      {
        id: `u-${Date.now()}`,
        name: form.name,
        email: form.email,
        role: form.role,
        status: 'Invited',
      },
      ...prev,
    ]);
    setOpen(false);
    setForm({ name: '', email: '', role: 'Viewer' });
  }

  function removeMember(id: string) {
    setMembers((prev) => prev.filter((m) => m.id !== id));
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Administration</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Manage users, roles and tenant-level access controls.</p>
        </div>
        <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-1.5" /> Invite user</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Members</p><p className="text-3xl font-bold mt-1">{members.length}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Active</p><p className="text-3xl font-bold mt-1">{members.filter((m) => m.status === 'Active').length}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Pending invites</p><p className="text-3xl font-bold mt-1">{members.filter((m) => m.status === 'Invited').length}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold flex items-center gap-2"><Users className="h-4 w-4 text-violet-600" /> Workspace members</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {members.map((m) => (
                <TableRow key={m.id}>
                  <TableCell className="font-medium">{m.name}</TableCell>
                  <TableCell>{m.email}</TableCell>
                  <TableCell><Badge variant="outline">{m.role}</Badge></TableCell>
                  <TableCell><Badge variant={m.status === 'Active' ? 'success' : 'warning'}>{m.status}</Badge></TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" onClick={() => removeMember(m.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Invite workspace user</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-1">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} placeholder="Jane Doe" />
            </div>
            <div className="space-y-1.5">
              <Label>Email</Label>
              <Input value={form.email} onChange={(e) => setForm((p) => ({ ...p, email: e.target.value }))} placeholder="jane@company.com" />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <select value={form.role} onChange={(e) => setForm((p) => ({ ...p, role: e.target.value as Role }))} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">
                <option value="Owner">Owner</option>
                <option value="Admin">Admin</option>
                <option value="QA Engineer">QA Engineer</option>
                <option value="Viewer">Viewer</option>
              </select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button disabled={!form.name || !form.email} onClick={invite}>Send invite</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
