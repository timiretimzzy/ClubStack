import { useEffect, useRef, useState } from 'react';
import { qaRecorder } from './recorder';
import type { QaEvent, SessionSummary } from './types';

export function QaDashboard() {
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [actionCount, setActionCount] = useState(0);
  const [lastAction, setLastAction] = useState('');
  const [lastResult, setLastResult] = useState('');
  const [lastApiStatus, setLastApiStatus] = useState('');
  const [lastError, setLastError] = useState('');
  const [elapsed, setElapsed] = useState(0);
  const [isOpen, setIsOpen] = useState(false);
  const [isPaused, setIsPaused] = useState(false);
  const startTimeRef = useRef<number>(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (qaRecorder.getSessionId()) {
      setSessionId(qaRecorder.getSessionId());
    }

    const unsubscribe = qaRecorder.onEvent((event: QaEvent) => {
      if (event.kind === 'action') {
        setActionCount((c) => c + 1);
        setLastAction(`${event.data.type}: ${event.data.element.accessibleName || event.data.element.text}`);
      }
      if (event.kind === 'network') {
        setLastApiStatus(`${event.data.status}`);
      }
      if (event.kind === 'console' && event.data.level === 'error') {
        setLastError(event.data.message);
      }
      if (event.kind === 'action_end') {
        setLastResult(event.data.result);
      }
    });

    return unsubscribe;
  }, []);

  useEffect(() => {
    if (sessionId) {
      startTimeRef.current = Date.now();
      timerRef.current = setInterval(() => {
        setElapsed(Math.floor((Date.now() - startTimeRef.current) / 1000));
      }, 1000);
    } else {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    }
    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    };
  }, [sessionId]);

  const handleStart = () => {
    qaRecorder.start();
    setSessionId(qaRecorder.getSessionId());
    setActionCount(0);
    setLastAction('');
    setLastResult('');
    setLastApiStatus('');
    setLastError('');
    setElapsed(0);
  };

  const handlePause = () => {
    if (isPaused) {
      qaRecorder.resume();
      setIsPaused(false);
    } else {
      qaRecorder.pause();
      setIsPaused(true);
    }
  };

  const handleStop = () => {
    const summary = qaRecorder.stop();
    if (summary) {
      saveSession(summary);
    }
    setSessionId(null);
    setActionCount(0);
  };

  const handleClear = () => {
    qaRecorder.clear();
    setSessionId(null);
    setActionCount(0);
    setLastAction('');
    setLastResult('');
    setLastApiStatus('');
    setLastError('');
    setElapsed(0);
  };

  const saveSession = (summary: SessionSummary) => {
    const blob = new Blob([JSON.stringify(summary, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `qa-results/sessions/${summary.sessionId}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const formatElapsed = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  const showPanel = isOpen || sessionId !== null;

  return (
    <div className="qa-recorder">
      <button
        className="qa-recorder-toggle"
        onClick={() => setIsOpen(!isOpen)}
        title="QA Recorder"
      >
        {sessionId ? 'QA REC ●' : 'QA REC ○'}
      </button>

      {showPanel && (
        <div className="qa-recorder-panel">
          <div className="qa-recorder-header">
            <span>QA Recorder</span>
            <button onClick={() => setIsOpen(false)}>×</button>
          </div>

          <div className="qa-recorder-body">
            <div className="qa-recorder-row">
              <span>Session:</span>
              <span>{sessionId || 'Not started'}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Actions:</span>
              <span>{actionCount}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Elapsed:</span>
              <span>{formatElapsed(elapsed)}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Last Action:</span>
              <span>{lastAction || '—'}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Last Result:</span>
              <span className={`qa-result-${lastResult.toLowerCase()}`}>{lastResult || '—'}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Last API:</span>
              <span>{lastApiStatus || '—'}</span>
            </div>
            <div className="qa-recorder-row">
              <span>Last Error:</span>
              <span className="qa-error">{lastError || '—'}</span>
            </div>
          </div>

          <div className="qa-recorder-controls">
            {!sessionId ? (
              <button className="btn btn-primary" onClick={handleStart}>
                Start QA Session
              </button>
            ) : (
              <>
                <button className="btn" onClick={handlePause}>
                  {isPaused ? 'Resume' : 'Pause'}
                </button>
                <button className="btn btn-primary" onClick={handleStop}>
                  Stop & Save
                </button>
                <button className="btn btn-danger" onClick={handleClear}>
                  Clear
                </button>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
