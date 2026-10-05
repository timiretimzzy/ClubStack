import type {
  ActionEvent,
  ActionSummary,
  ConsoleEvent,
  ElementInfo,
  NetworkEvent,
  QaEvent,
  SessionSummary,
  UiChangeEvent,
} from './types';

const SENSITIVE_FIELDS = ['password', 'token', 'secret', 'key', 'auth', 'cookie', 'license', 'credential'];

function generateSessionId(): string {
  return `QA-SESSION-${Date.now().toString(36).toUpperCase()}-${Math.random().toString(36).substring(2, 8).toUpperCase()}`;
}

function getElementInfo(target: Element): ElementInfo {
  const rect = target.getBoundingClientRect();
  const role = target.getAttribute('role') || target.tagName.toLowerCase();
  const text = (target.textContent || '').trim().substring(0, 200);
  const ariaLabel = target.getAttribute('aria-label') || '';
  const name = target.getAttribute('name') || '';
  const id = target.id || '';
  const testId = target.getAttribute('data-testid') || '';
  const accessibleName = ariaLabel || text || name || id || testId;
  const parent = target.parentElement
    ? `${target.parentElement.tagName.toLowerCase()}${target.parentElement.id ? `#${target.parentElement.id}` : ''}${target.parentElement.className ? `.${target.parentElement.className.split(' ').join('.')}` : ''}`
    : '';
  const selector = buildSelector(target);

  return {
    tagName: target.tagName.toLowerCase(),
    role,
    text,
    ariaLabel,
    name,
    id,
    testId,
    accessibleName,
    parent,
    selector,
    enabled: !(target as HTMLButtonElement).disabled,
    visible: rect.width > 0 && rect.height > 0,
    viewportX: Math.round(rect.x),
    viewportY: Math.round(rect.y),
  };
}

function buildSelector(el: Element): string {
  const parts: string[] = [];
  let current: Element | null = el;
  while (current && current !== document.body) {
    let part = current.tagName.toLowerCase();
    if (current.id) {
      part += `#${current.id}`;
      parts.unshift(part);
      break;
    }
    if (current.getAttribute('data-testid')) {
      part += `[data-testid="${current.getAttribute('data-testid')}"]`;
      parts.unshift(part);
      break;
    }
    const parent = current.parentElement;
    if (parent) {
      const siblings = Array.from(parent.children).filter((c) => c.tagName === current!.tagName);
      if (siblings.length > 1) {
        const idx = siblings.indexOf(current) + 1;
        part += `:nth-of-type(${idx})`;
      }
    }
    parts.unshift(part);
    current = current.parentElement;
  }
  return parts.join(' > ');
}

function isSensitiveField(el: Element): boolean {
  const type = (el as HTMLInputElement).type || '';
  const name = (el as HTMLInputElement).name || '';
  const id = el.id || '';
  const ariaLabel = el.getAttribute('aria-label') || '';
  const combined = `${type} ${name} ${id} ${ariaLabel}`.toLowerCase();
  return SENSITIVE_FIELDS.some((s) => combined.includes(s));
}

export class QaRecorder {
  private sessionId: string | null = null;
  private actionCounter = 0;
  private requestCounter = 0;
  private changeCounter = 0;
  private consoleCounter = 0;
  private events: QaEvent[] = [];
  private actionMap = new Map<string, ActionSummary>();
  private currentActionId: string | null = null;
  private paused = false;
  private listeners: Array<(event: QaEvent) => void> = [];
  private originalFetch: typeof fetch | null = null;
  private originalXHROpen: typeof XMLHttpRequest.prototype.open | null = null;
  private originalXHRSend: typeof XMLHttpRequest.prototype.send | null = null;
  private originalConsoleError: typeof console.error | null = null;
  private originalConsoleWarn: typeof console.warn | null = null;
  private mutationObserver: MutationObserver | null = null;
  private routeListener: (() => void) | null = null;
  private uiWindow: number = 3000;
  private uiChangeTimer: ReturnType<typeof setTimeout> | null = null;
  private pendingUiChanges: UiChangeEvent[] = [];
  private screenshotDir = 'qa-results/screenshots';
  private sessionDir = 'qa-results/sessions';
  private logDir = 'qa-results/logs';

  start(): void {
    if (this.sessionId) return;
    this.sessionId = generateSessionId();
    this.actionCounter = 0;
    this.requestCounter = 0;
    this.changeCounter = 0;
    this.consoleCounter = 0;
    this.events = [];
    this.actionMap.clear();
    this.currentActionId = null;
    this.paused = false;

    this.saveSessionState();
    this.interceptFetch();
    this.interceptXHR();
    this.interceptConsole();
    this.observeDom();
    this.trackRouteChanges();

    this.emit({
      kind: 'action',
      data: {
        actionId: 'ACT-000000',
        sessionId: this.sessionId,
        type: 'route_change',
        timestamp: new Date().toISOString(),
        route: window.location.pathname,
        pageTitle: document.title,
        element: {
          tagName: 'body',
          role: 'document',
          text: 'Session started',
          ariaLabel: '',
          name: '',
          id: '',
          testId: '',
          accessibleName: 'Session started',
          parent: '',
          selector: 'body',
          enabled: true,
          visible: true,
          viewportX: 0,
          viewportY: 0,
        },
      },
    });
  }

  pause(): void {
    this.paused = true;
  }

  resume(): void {
    this.paused = false;
  }

  stop(): SessionSummary | null {
    if (!this.sessionId) return null;

    const summary = this.buildSummary();
    this.cleanup();
    this.clearSessionState();
    return summary;
  }

  clear(): void {
    this.cleanup();
    this.sessionId = null;
    this.events = [];
    this.actionMap.clear();
    this.clearSessionState();
  }

  private saveSessionState(): void {
    if (!this.sessionId) return;
    try {
      sessionStorage.setItem('qa_recorder_session', JSON.stringify({
        sessionId: this.sessionId,
        actionCounter: this.actionCounter,
        requestCounter: this.requestCounter,
        changeCounter: this.changeCounter,
        consoleCounter: this.consoleCounter,
        paused: this.paused,
      }));
    } catch {
      // ignore
    }
  }

  private clearSessionState(): void {
    try {
      sessionStorage.removeItem('qa_recorder_session');
    } catch {
      // ignore
    }
  }

  restoreSession(): boolean {
    try {
      const raw = sessionStorage.getItem('qa_recorder_session');
      if (!raw) return false;
      const state = JSON.parse(raw);
      if (!state.sessionId) return false;

      this.sessionId = state.sessionId;
      this.actionCounter = state.actionCounter || 0;
      this.requestCounter = state.requestCounter || 0;
      this.changeCounter = state.changeCounter || 0;
      this.consoleCounter = state.consoleCounter || 0;
      this.paused = state.paused || false;
      this.events = [];
      this.actionMap.clear();
      this.currentActionId = null;

      this.interceptFetch();
      this.interceptXHR();
      this.interceptConsole();
      this.observeDom();
      this.trackRouteChanges();

      return true;
    } catch {
      return false;
    }
  }

  getSessionId(): string | null {
    return this.sessionId;
  }

  isPaused(): boolean {
    return this.paused;
  }

  getActionCount(): number {
    return this.actionCounter;
  }

  getCurrentActionId(): string | null {
    return this.currentActionId;
  }

  onEvent(listener: (event: QaEvent) => void): () => void {
    this.listeners.push(listener);
    return () => {
      this.listeners = this.listeners.filter((l) => l !== listener);
    };
  }

  private emit(event: QaEvent): void {
    if (this.paused) return;
    this.events.push(event);
    for (const listener of this.listeners) {
      listener(event);
    }
  }

  private nextActionId(): string {
    this.actionCounter += 1;
    return `ACT-${this.actionCounter.toString().padStart(6, '0')}`;
  }

  private nextRequestId(): string {
    this.requestCounter += 1;
    return `REQ-${this.requestCounter.toString().padStart(6, '0')}`;
  }

  private nextChangeId(): string {
    this.changeCounter += 1;
    return `CHG-${this.changeCounter.toString().padStart(6, '0')}`;
  }

  private nextConsoleId(): string {
    this.consoleCounter += 1;
    return `CON-${this.consoleCounter.toString().padStart(6, '0')}`;
  }

  private interceptFetch(): void {
    this.originalFetch = window.fetch;
    const self = this;
    window.fetch = async function (input: RequestInfo | URL, init?: RequestInit) {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
      const method = init?.method || 'GET';
      const actionId = self.currentActionId || 'UNLINKED';
      const requestId = self.nextRequestId();
      const startTime = performance.now();
      const timestamp = new Date().toISOString();

      try {
        const response = await self.originalFetch!.call(window, input, init);
        const duration = performance.now() - startTime;
        const contentType = response.headers.get('content-type') || '';
        let responseBody = '';
        let errorBody = '';
        let businessErrorCode = '';

        if (contentType.includes('application/json')) {
          try {
            const cloned = response.clone();
            const text = await cloned.text();
            responseBody = text.substring(0, 5000);
            if (!response.ok) {
              errorBody = responseBody;
              try {
                const parsed = JSON.parse(text);
                businessErrorCode = parsed.code || parsed.title || '';
              } catch {
                // not JSON
              }
            }
          } catch {
            // ignore
          }
        }

        const correlationId = response.headers.get('x-correlation-id') || response.headers.get('x-request-id') || '';

        self.emit({
          kind: 'network',
          data: {
            requestId,
            actionId,
            sessionId: self.sessionId || '',
            method,
            url,
            route: window.location.pathname,
            timestamp,
            duration: Math.round(duration),
            status: response.status,
            responseContentType: contentType,
            responseBody,
            errorBody,
            correlationId,
            businessErrorCode,
            retryCount: 0,
          },
        });

        return response;
      } catch (error) {
        const duration = performance.now() - startTime;
        self.emit({
          kind: 'network',
          data: {
            requestId,
            actionId,
            sessionId: self.sessionId || '',
            method,
            url,
            route: window.location.pathname,
            timestamp,
            duration: Math.round(duration),
            status: 0,
            responseContentType: '',
            responseBody: '',
            errorBody: error instanceof Error ? error.message : String(error),
            correlationId: '',
            businessErrorCode: '',
            retryCount: 0,
          },
        });
        throw error;
      }
    };
  }

  private interceptXHR(): void {
    this.originalXHROpen = XMLHttpRequest.prototype.open;
    this.originalXHRSend = XMLHttpRequest.prototype.send;
    const self = this;

    XMLHttpRequest.prototype.open = function (this: XMLHttpRequest, method: string, url: string | URL, ...args: unknown[]) {
      (this as any).__qaMethod = method;
      (this as any).__qaUrl = url.toString();
      const original = self.originalXHROpen!;
      if (args.length === 0) return original.call(this, method, url, true);
      if (args.length === 1) return original.call(this, method, url, args[0] as boolean);
      if (args.length === 2) return original.call(this, method, url, args[0] as boolean, args[1] as string);
      return original.call(this, method, url, args[0] as boolean, args[1] as string, args[2] as string);
    };

    XMLHttpRequest.prototype.send = function (body?: Document | XMLHttpRequestBodyInit | null) {
      const xhr = this;
      const method = (xhr as any).__qaMethod || 'GET';
      const url = (xhr as any).__qaUrl || '';
      const actionId = self.currentActionId || 'UNLINKED';
      const requestId = self.nextRequestId();
      const startTime = performance.now();
      const timestamp = new Date().toISOString();

      xhr.addEventListener('loadend', () => {
        const duration = performance.now() - startTime;
        const contentType = xhr.getResponseHeader('content-type') || '';
        let responseBody = '';
        let errorBody = '';
        let businessErrorCode = '';

        if (contentType.includes('application/json')) {
          responseBody = (xhr.responseText || '').substring(0, 5000);
          if (xhr.status >= 400) {
            errorBody = responseBody;
            try {
              const parsed = JSON.parse(responseBody);
              businessErrorCode = parsed.code || parsed.title || '';
            } catch {
              // not JSON
            }
          }
        }

        const correlationId = xhr.getResponseHeader('x-correlation-id') || xhr.getResponseHeader('x-request-id') || '';

        self.emit({
          kind: 'network',
          data: {
            requestId,
            actionId,
            sessionId: self.sessionId || '',
            method,
            url,
            route: window.location.pathname,
            timestamp,
            duration: Math.round(duration),
            status: xhr.status,
            responseContentType: contentType,
            responseBody,
            errorBody,
            correlationId,
            businessErrorCode,
            retryCount: 0,
          },
        });
      });

      return self.originalXHRSend!.call(this, body);
    };
  }

  private interceptConsole(): void {
    this.originalConsoleError = console.error;
    this.originalConsoleWarn = console.warn;
    const self = this;

    console.error = function (...args: unknown[]) {
      const message = args.map((a) => (typeof a === 'string' ? a : JSON.stringify(a))).join(' ');
      const stack = args.find((a) => a instanceof Error)?.stack || '';
      self.emit({
        kind: 'console',
        data: {
          consoleId: self.nextConsoleId(),
          actionId: self.currentActionId || 'UNLINKED',
          sessionId: self.sessionId || '',
          timestamp: new Date().toISOString(),
          level: 'error',
          message: message.substring(0, 2000),
          stack: stack.substring(0, 2000),
        },
      });
      return self.originalConsoleError!.apply(console, args);
    };

    console.warn = function (...args: unknown[]) {
      const message = args.map((a) => (typeof a === 'string' ? a : JSON.stringify(a))).join(' ');
      self.emit({
        kind: 'console',
        data: {
          consoleId: self.nextConsoleId(),
          actionId: self.currentActionId || 'UNLINKED',
          sessionId: self.sessionId || '',
          timestamp: new Date().toISOString(),
          level: 'warn',
          message: message.substring(0, 2000),
          stack: '',
        },
      });
      return self.originalConsoleWarn!.apply(console, args);
    };
  }

  private observeDom(): void {
    this.mutationObserver = new MutationObserver((mutations) => {
      if (this.paused || !this.currentActionId) return;

      for (const mutation of mutations) {
        if (mutation.type === 'childList') {
          for (const node of mutation.addedNodes) {
            if (node instanceof HTMLElement) {
              const text = (node.textContent || '').trim();
              if (text && text.length < 500) {
                this.queueUiChange('element_added', text.substring(0, 200), node);
              }
            }
          }
          for (const node of mutation.removedNodes) {
            if (node instanceof HTMLElement) {
              const text = (node.textContent || '').trim();
              if (text && text.length < 500) {
                this.queueUiChange('element_removed', text.substring(0, 200), node);
              }
            }
          }
        }
        if (mutation.type === 'attributes') {
          const target = mutation.target as HTMLElement;
          if (target) {
            this.queueUiChange('attribute_changed', `${mutation.attributeName} changed`, target);
          }
        }
      }
    });

    this.mutationObserver.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['class', 'disabled', 'aria-expanded', 'aria-selected', 'data-state'],
    });
  }

  private queueUiChange(changeType: string, description: string, el: Element): void {
    const change: UiChangeEvent = {
      changeId: this.nextChangeId(),
      actionId: this.currentActionId || 'UNLINKED',
      sessionId: this.sessionId || '',
      timestamp: new Date().toISOString(),
      changeType,
      description,
      selector: buildSelector(el),
    };

    this.pendingUiChanges.push(change);

    if (this.uiChangeTimer) {
      clearTimeout(this.uiChangeTimer);
    }
    this.uiChangeTimer = setTimeout(() => {
      for (const c of this.pendingUiChanges) {
        this.emit({ kind: 'ui_change', data: c });
      }
      this.pendingUiChanges = [];
    }, 500);
  }

  private trackRouteChanges(): void {
    let lastPath = window.location.pathname;

    this.routeListener = () => {
      const currentPath = window.location.pathname;
      if (currentPath !== lastPath) {
        lastPath = currentPath;
        this.emit({
          kind: 'action',
          data: {
            actionId: this.nextActionId(),
            sessionId: this.sessionId || '',
            type: 'route_change',
            timestamp: new Date().toISOString(),
            route: currentPath,
            pageTitle: document.title,
            element: {
              tagName: 'body',
              role: 'document',
              text: `Navigated to ${currentPath}`,
              ariaLabel: '',
              name: '',
              id: '',
              testId: '',
              accessibleName: `Navigated to ${currentPath}`,
              parent: '',
              selector: 'body',
              enabled: true,
              visible: true,
              viewportX: 0,
              viewportY: 0,
            },
          },
        });
      }
    };

    window.addEventListener('popstate', this.routeListener);
    window.addEventListener('hashchange', this.routeListener);
  }

  recordAction(type: string, target: Element): string {
    if (!this.sessionId || this.paused) return '';

    const actionId = this.nextActionId();
    this.currentActionId = actionId;

    const action: ActionEvent = {
      actionId,
      sessionId: this.sessionId,
      type: type as any,
      timestamp: new Date().toISOString(),
      route: window.location.pathname,
      pageTitle: document.title,
      element: getElementInfo(target),
    };

    this.emit({ kind: 'action', data: action });

    const summary: ActionSummary = {
      actionId,
      sessionId: this.sessionId,
      type: type as any,
      timestamp: action.timestamp,
      route: action.route,
      element: action.element,
      result: 'UNKNOWN',
      classification: 'UNCLASSIFIED',
      networkRequests: [],
      uiChanges: [],
      consoleEvents: [],
    };
    this.actionMap.set(actionId, summary);

    setTimeout(() => {
      this.finalizeAction(actionId);
    }, this.uiWindow);

    return actionId;
  }

  recordFormSubmit(target: Element): string {
    return this.recordAction('form_submit', target);
  }

  recordInput(target: Element, value: string): void {
    if (!this.sessionId || this.paused) return;
    const actionId = this.currentActionId;
    if (!actionId) return;

    const sanitized = isSensitiveField(target) ? '[REDACTED]' : value;
    const el = target as HTMLInputElement;
    const field = el.name || el.id || el.getAttribute('aria-label') || el.type || 'unknown';

    this.queueUiChange('input', `${field}=${sanitized} (length=${value.length})`, target);
  }

  private finalizeAction(actionId: string): void {
    const summary = this.actionMap.get(actionId);
    if (!summary) return;

    const networkRequests = this.events
      .filter((e): e is { kind: 'network'; data: NetworkEvent } => e.kind === 'network' && e.data.actionId === actionId)
      .map((e) => e.data);

    const uiChanges = this.events
      .filter((e): e is { kind: 'ui_change'; data: UiChangeEvent } => e.kind === 'ui_change' && e.data.actionId === actionId)
      .map((e) => e.data);

    const consoleEvents = this.events
      .filter((e): e is { kind: 'console'; data: ConsoleEvent } => e.kind === 'console' && e.data.actionId === actionId)
      .map((e) => e.data);

    summary.networkRequests = networkRequests;
    summary.uiChanges = uiChanges;
    summary.consoleEvents = consoleEvents;

    const hasError = networkRequests.some((r) => r.status >= 400) || consoleEvents.some((c) => c.level === 'error');
    const hasSuccess = networkRequests.some((r) => r.status >= 200 && r.status < 300);
    const hasUiChange = uiChanges.length > 0;

    if (hasError) {
      summary.result = 'FAIL';
      summary.classification = 'UNCLASSIFIED';
    } else if (hasSuccess) {
      summary.result = 'PASS';
    } else if (hasUiChange) {
      summary.result = 'PASS';
    } else {
      summary.result = 'NO_EFFECT';
    }

    this.emit({
      kind: 'action_end',
      data: {
        actionId,
        result: summary.result,
        classification: summary.classification,
      },
    });

    if (this.currentActionId === actionId) {
      this.currentActionId = null;
    }
  }

  private buildSummary(): SessionSummary {
    const summaries = Array.from(this.actionMap.values());
    const passed = summaries.filter((s) => s.result === 'PASS').length;
    const failed = summaries.filter((s) => s.result === 'FAIL').length;
    const blocked = summaries.filter((s) => s.result === 'BLOCKED').length;
    const unknown = summaries.filter((s) => s.result === 'UNKNOWN').length;
    const networkErrors = summaries.reduce((acc, s) => acc + s.networkRequests.filter((r) => r.status >= 400).length, 0);
    const consoleErrors = summaries.reduce((acc, s) => acc + s.consoleEvents.filter((c) => c.level === 'error').length, 0);

    const firstEvent = this.events.find((e) => e.kind === 'action');
    const startedAt = firstEvent && firstEvent.kind === 'action' ? firstEvent.data.timestamp : new Date().toISOString();

    return {
      sessionId: this.sessionId || '',
      startedAt,
      endedAt: new Date().toISOString(),
      actions: summaries.length,
      passed,
      failed,
      blocked,
      unknown,
      networkErrors,
      consoleErrors,
      actionSummaries: summaries,
    };
  }

  private cleanup(): void {
    if (this.originalFetch) {
      window.fetch = this.originalFetch;
      this.originalFetch = null;
    }
    if (this.originalXHROpen) {
      XMLHttpRequest.prototype.open = this.originalXHROpen;
      this.originalXHROpen = null;
    }
    if (this.originalXHRSend) {
      XMLHttpRequest.prototype.send = this.originalXHRSend;
      this.originalXHRSend = null;
    }
    if (this.originalConsoleError) {
      console.error = this.originalConsoleError;
      this.originalConsoleError = null;
    }
    if (this.originalConsoleWarn) {
      console.warn = this.originalConsoleWarn;
      this.originalConsoleWarn = null;
    }
    if (this.mutationObserver) {
      this.mutationObserver.disconnect();
      this.mutationObserver = null;
    }
    if (this.routeListener) {
      window.removeEventListener('popstate', this.routeListener);
      window.removeEventListener('hashchange', this.routeListener);
      this.routeListener = null;
    }
    if (this.uiChangeTimer) {
      clearTimeout(this.uiChangeTimer);
      this.uiChangeTimer = null;
    }
  }

  getEvents(): QaEvent[] {
    return [...this.events];
  }

  getSessionDir(): string {
    return this.sessionDir;
  }

  getScreenshotDir(): string {
    return this.screenshotDir;
  }

  getLogDir(): string {
    return this.logDir;
  }
}

export const qaRecorder = new QaRecorder();
