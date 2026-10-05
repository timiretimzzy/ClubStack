import type { QaEvent, SessionSummary } from './types';

export function eventsToNdjson(events: QaEvent[]): string {
  return events.map((e) => JSON.stringify(e)).join('\n') + '\n';
}

export function saveSessionJson(summary: SessionSummary): void {
  const blob = new Blob([JSON.stringify(summary, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `qa-results/sessions/${summary.sessionId}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

export function saveSessionNdjson(sessionId: string, events: QaEvent[]): void {
  const content = eventsToNdjson(events);
  const blob = new Blob([content], { type: 'application/x-ndjson' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `qa-results/logs/${sessionId}.ndjson`;
  a.click();
  URL.revokeObjectURL(url);
}

export function saveScreenshot(sessionId: string, actionId: string, dataUrl: string): void {
  const base64 = dataUrl.split(',')[1];
  const blob = atob(base64);
  const array = new Uint8Array(blob.length);
  for (let i = 0; i < blob.length; i++) {
    array[i] = blob.charCodeAt(i);
  }
  const url = URL.createObjectURL(new Blob([array], { type: 'image/png' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `qa-results/screenshots/${sessionId}/${actionId}.png`;
  a.click();
  URL.revokeObjectURL(url);
}
