import { BrowserRouter } from 'react-router-dom';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { QaDashboard } from './qa/QaDashboard';
import { qaRecorder } from './qa/recorder';
import './styles.css';

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element was not found.');
}

function isInteractiveElement(el: Element): boolean {
  const tag = el.tagName.toLowerCase();
  const role = el.getAttribute('role') || '';
  const type = (el as HTMLInputElement).type || '';

  if (tag === 'button' || tag === 'a' || tag === 'input' || tag === 'select' || tag === 'textarea' || tag === 'label') {
    return true;
  }
  if (role === 'button' || role === 'link' || role === 'menuitem' || role === 'tab' || role === 'checkbox' || role === 'radio') {
    return true;
  }
  if (type === 'submit' || type === 'button' || type === 'checkbox' || type === 'radio') {
    return true;
  }
  return false;
}

function findInteractiveTarget(target: EventTarget | null): Element | null {
  if (!(target instanceof Element)) return null;
  let current: Element | null = target;
  while (current && current !== document.body) {
    if (isInteractiveElement(current)) {
      return current;
    }
    current = current.parentElement;
  }
  return null;
}

function attachRecorderListeners(): void {
  document.addEventListener('click', (e) => {
    const el = findInteractiveTarget(e.target);
    if (el) {
      qaRecorder.recordAction('click', el);
    }
  }, true);

  document.addEventListener('dblclick', (e) => {
    const el = findInteractiveTarget(e.target);
    if (el) {
      qaRecorder.recordAction('dblclick', el);
    }
  }, true);

  document.addEventListener('contextmenu', (e) => {
    const el = findInteractiveTarget(e.target);
    if (el) {
      qaRecorder.recordAction('contextmenu', el);
    }
  }, true);

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === 'Escape' || e.key === 'Tab') {
      const el = findInteractiveTarget(e.target);
      if (el) {
        qaRecorder.recordAction('keydown', el);
      }
    }
  }, true);

  document.addEventListener('submit', (e) => {
    if (e.target instanceof HTMLFormElement) {
      qaRecorder.recordFormSubmit(e.target);
    }
  }, true);

  document.addEventListener('input', (e) => {
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement || e.target instanceof HTMLSelectElement) {
      qaRecorder.recordInput(e.target, (e.target as HTMLInputElement).value || '');
    }
  }, true);

  document.addEventListener('change', (e) => {
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
      qaRecorder.recordAction('change', e.target);
    }
  }, true);
}

if (import.meta.env.DEV || import.meta.env.VITE_QA_RECORDER === 'true') {
  attachRecorderListeners();
  qaRecorder.restoreSession();
}

createRoot(rootElement).render(
  <BrowserRouter>
    <App />
    {import.meta.env.DEV || import.meta.env.VITE_QA_RECORDER === 'true' ? <QaDashboard /> : null}
  </BrowserRouter>,
);