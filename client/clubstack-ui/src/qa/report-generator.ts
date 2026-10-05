import type { SessionSummary } from './types';

export function generateMarkdownReport(summary: SessionSummary): string {
  const lines: string[] = [];

  lines.push(`# QA Session Report: ${summary.sessionId}`);
  lines.push('');
  lines.push(`**Started:** ${summary.startedAt}`);
  lines.push(`**Ended:** ${summary.endedAt}`);
  lines.push(`**Duration:** ${new Date(summary.endedAt).getTime() - new Date(summary.startedAt).getTime()}ms`);
  lines.push('');
  lines.push('## Summary');
  lines.push('');
  lines.push(`| Metric | Count |`);
  lines.push(`|---|---|`);
  lines.push(`| Total Actions | ${summary.actions} |`);
  lines.push(`| Passed | ${summary.passed} |`);
  lines.push(`| Failed | ${summary.failed} |`);
  lines.push(`| Blocked | ${summary.blocked} |`);
  lines.push(`| Unknown | ${summary.unknown} |`);
  lines.push(`| Network Errors | ${summary.networkErrors} |`);
  lines.push(`| Console Errors | ${summary.consoleErrors} |`);
  lines.push('');
  lines.push('## Actions');
  lines.push('');

  for (const action of summary.actionSummaries) {
    lines.push(`## ${action.actionId}`);
    lines.push('');
    lines.push(`**Time:** ${action.timestamp}`);
    lines.push(`**Route:** ${action.route}`);
    lines.push(`**Action:** ${action.type}`);
    lines.push(`**Element:** ${action.element.accessibleName || action.element.text || action.element.tagName}`);
    lines.push(`**Selector:** \`${action.element.selector}\``);
    lines.push('');

    if (action.networkRequests.length > 0) {
      lines.push('### Network Requests');
      lines.push('');
      for (const req of action.networkRequests) {
        lines.push(`- **${req.method}** ${req.url}`);
        lines.push(`  - Status: ${req.status}`);
        lines.push(`  - Duration: ${req.duration}ms`);
        if (req.correlationId) {
          lines.push(`  - Correlation ID: ${req.correlationId}`);
        }
        if (req.businessErrorCode) {
          lines.push(`  - Business Error Code: ${req.businessErrorCode}`);
        }
        if (req.errorBody) {
          lines.push(`  - Error Body: ${req.errorBody.substring(0, 500)}`);
        }
      }
      lines.push('');
    } else {
      lines.push('### Network Requests');
      lines.push('');
      lines.push('NO_NETWORK_REQUEST');
      lines.push('');
    }

    if (action.uiChanges.length > 0) {
      lines.push('### UI Changes');
      lines.push('');
      for (const change of action.uiChanges) {
        lines.push(`- **${change.changeType}:** ${change.description}`);
      }
      lines.push('');
    }

    if (action.consoleEvents.length > 0) {
      lines.push('### Console Events');
      lines.push('');
      for (const evt of action.consoleEvents) {
        lines.push(`- **${evt.level}:** ${evt.message}`);
        if (evt.stack) {
          lines.push(`  - Stack: ${evt.stack.substring(0, 500)}`);
        }
      }
      lines.push('');
    }

    lines.push(`**Result:** ${action.result}`);
    lines.push(`**Classification:** ${action.classification}`);
    lines.push('');
    lines.push('---');
    lines.push('');
  }

  return lines.join('\n');
}

export function downloadReport(summary: SessionSummary): void {
  const markdown = generateMarkdownReport(summary);
  const blob = new Blob([markdown], { type: 'text/markdown' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `QA_SESSION_REPORT_${summary.sessionId}.md`;
  a.click();
  URL.revokeObjectURL(url);
}
