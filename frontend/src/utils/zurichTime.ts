const TZ = 'Europe/Zurich';

function getParts(utcIso: string) {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: TZ,
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(new Date(utcIso));
}

function get(parts: Intl.DateTimeFormatPart[], type: string) {
  return parts.find(p => p.type === type)?.value ?? '';
}

export function formatZurichDatetime(utcIso: string): string {
  const p = getParts(utcIso);
  return `${get(p, 'day')} ${get(p, 'month')} ${get(p, 'year')} ${get(p, 'hour')}:${get(p, 'minute')}`;
}

export function formatZurichShort(utcIso: string): string {
  const p = getParts(utcIso);
  return `${get(p, 'day')} ${get(p, 'month')} ${get(p, 'hour')}:${get(p, 'minute')}`;
}

export function formatZurichTime(utcIso: string): string {
  const p = getParts(utcIso);
  return `${get(p, 'hour')}:${get(p, 'minute')}`;
}

// Converts a datetime-local input value (treated as Zurich time) to a UTC ISO string.
// Uses a probe to determine the actual Zurich offset (CET +01:00 / CEST +02:00) at that date.
export function zurichInputToUtcIso(datetimeLocalStr: string): string {
  // Start by treating input as CET (UTC+1)
  const withCET = new Date(datetimeLocalStr + ':00+01:00');
  const zurichHour = parseInt(
    new Intl.DateTimeFormat('en-US', {
      timeZone: TZ, hour: '2-digit', hourCycle: 'h23',
    }).format(withCET),
    10,
  );
  const targetHour = parseInt(datetimeLocalStr.slice(11, 13), 10);
  const diff = targetHour - zurichHour; // 0 in CET, -1 in CEST
  return new Date(withCET.getTime() + diff * 3_600_000).toISOString();
}
