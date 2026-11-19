export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('es-MX', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function daysUntil(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const target = new Date(iso);
  const now = new Date();
  return Math.ceil((target.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
}

const statusColors: Record<string, string> = {
  Active: '#2563eb',
  Planning: '#7c3aed',
  Completed: '#16a34a',
  OnHold: '#ca8a04',
  Cancelled: '#dc2626',
  Pending: '#6b7280',
  InProgress: '#2563eb',
  Delayed: '#ea580c',
  Delivered: '#16a34a',
  Rejected: '#dc2626',
  Missed: '#dc2626',
  Deployed: '#16a34a',
  Failed: '#dc2626',
  Rollback: '#ea580c',
};

export function getStatusColor(status: string): string {
  return statusColors[status] || '#6b7280';
}

const priorityColors: Record<string, string> = {
  Low: '#6b7280',
  Medium: '#2563eb',
  High: '#ea580c',
  Critical: '#dc2626',
};

export function getPriorityColor(priority: string): string {
  return priorityColors[priority] || '#6b7280';
}
