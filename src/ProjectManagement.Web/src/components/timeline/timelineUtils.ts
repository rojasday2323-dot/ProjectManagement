export type TimeScale = 'days' | 'weeks' | 'months';

export function getColumnWidth(scale: TimeScale): number {
  switch (scale) {
    case 'days': return 32;
    case 'weeks': return 80;
    case 'months': return 160;
  }
}

export function generateDateColumns(start: Date, end: Date, scale: TimeScale): Date[] {
  const dates: Date[] = [];
  const current = new Date(start);

  while (current <= end) {
    dates.push(new Date(current));
    switch (scale) {
      case 'days':
        current.setDate(current.getDate() + 1);
        break;
      case 'weeks':
        current.setDate(current.getDate() + 7);
        break;
      case 'months':
        current.setMonth(current.getMonth() + 1);
        break;
    }
  }
  return dates;
}

export function daysBetween(a: Date, b: Date): number {
  return Math.round((b.getTime() - a.getTime()) / (1000 * 60 * 60 * 24));
}

export function getBarPosition(
  itemStart: Date,
  itemEnd: Date,
  timelineStart: Date,
  colWidth: number,
  scale: TimeScale
): { left: number; width: number } {
  const startDays = daysBetween(timelineStart, itemStart);
  const durationDays = Math.max(daysBetween(itemStart, itemEnd), 1);

  const divisor = scale === 'days' ? 1 : scale === 'weeks' ? 7 : 30;
  const left = (startDays / divisor) * colWidth;
  const width = Math.max((durationDays / divisor) * colWidth, 6);

  return { left, width };
}

export function getMarkerPosition(
  date: Date,
  timelineStart: Date,
  colWidth: number,
  scale: TimeScale
): number {
  const days = daysBetween(timelineStart, date);
  const divisor = scale === 'days' ? 1 : scale === 'weeks' ? 7 : 30;
  return (days / divisor) * colWidth;
}

export function formatColumnHeader(date: Date, scale: TimeScale): string {
  switch (scale) {
    case 'days':
      return `${date.getDate()}`;
    case 'weeks':
      return `${date.getDate()}/${date.getMonth() + 1}`;
    case 'months':
      return date.toLocaleDateString('es-MX', { month: 'short', year: '2-digit' });
  }
}

export function formatMonthGroup(date: Date): string {
  return date.toLocaleDateString('es-MX', { month: 'short', year: 'numeric' });
}
