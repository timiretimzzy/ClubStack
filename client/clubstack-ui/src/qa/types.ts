export type ActionType =
  | 'click'
  | 'dblclick'
  | 'contextmenu'
  | 'keydown'
  | 'form_submit'
  | 'input'
  | 'change'
  | 'route_change'
  | 'page_reload'
  | 'navigation';

export type ActionResult = 'PASS' | 'FAIL' | 'NO_EFFECT' | 'BLOCKED' | 'UNKNOWN';

export type FailureClassification =
  | 'P1'
  | 'P2'
  | 'P3'
  | 'P4'
  | 'P5'
  | 'P6'
  | 'UNCLASSIFIED';

export interface ElementInfo {
  tagName: string;
  role: string;
  text: string;
  ariaLabel: string;
  name: string;
  id: string;
  testId: string;
  accessibleName: string;
  parent: string;
  selector: string;
  enabled: boolean;
  visible: boolean;
  viewportX: number;
  viewportY: number;
}

export interface ActionEvent {
  actionId: string;
  sessionId: string;
  type: ActionType;
  timestamp: string;
  route: string;
  pageTitle: string;
  element: ElementInfo;
  screenshotBefore?: string;
  screenshotAfter?: string;
}

export interface NetworkEvent {
  requestId: string;
  actionId: string;
  sessionId: string;
  method: string;
  url: string;
  route: string;
  timestamp: string;
  duration: number;
  status: number;
  responseContentType: string;
  responseBody: string;
  errorBody: string;
  correlationId: string;
  businessErrorCode: string;
  retryCount: number;
}

export interface UiChangeEvent {
  changeId: string;
  actionId: string;
  sessionId: string;
  timestamp: string;
  changeType: string;
  description: string;
  selector: string;
}

export interface ConsoleEvent {
  consoleId: string;
  actionId: string;
  sessionId: string;
  timestamp: string;
  level: string;
  message: string;
  stack: string;
}

export interface ActionSummary {
  actionId: string;
  sessionId: string;
  type: ActionType;
  timestamp: string;
  route: string;
  element: ElementInfo;
  result: ActionResult;
  classification: FailureClassification;
  networkRequests: NetworkEvent[];
  uiChanges: UiChangeEvent[];
  consoleEvents: ConsoleEvent[];
  screenshotBefore?: string;
  screenshotAfter?: string;
}

export interface SessionSummary {
  sessionId: string;
  startedAt: string;
  endedAt: string;
  actions: number;
  passed: number;
  failed: number;
  blocked: number;
  unknown: number;
  networkErrors: number;
  consoleErrors: number;
  actionSummaries: ActionSummary[];
}

export type QaEvent =
  | { kind: 'action'; data: ActionEvent }
  | { kind: 'network'; data: NetworkEvent }
  | { kind: 'ui_change'; data: UiChangeEvent }
  | { kind: 'console'; data: ConsoleEvent }
  | { kind: 'action_end'; data: { actionId: string; result: ActionResult; classification: FailureClassification } };
