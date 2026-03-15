import { useEffect, useState } from 'react';
import { Api } from '../api/client';
import type { HealthStatus } from '../api/types';
import { LoadingView } from '../components/LoadingView';
import { Panel } from '../components/Panel';

export function HealthPage() {
  const [loading, setLoading] = useState(true);
  const [health, setHealth] = useState<HealthStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = async (): Promise<void> => {
    try {
      setLoading(true);
      setError(null);
      const data = await Api.getHealth();
      setHealth(data);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
  }, []);

  if (loading && !health) {
    return <LoadingView text="Перевірка стану системи..." />;
  }

  return (
    <div className="page-grid">
      <Panel title="Health Check" subtitle="Контроль доступності Web API та PostgreSQL.">
        <div className="health-card">
          <strong className={health?.status === 'healthy' ? 'ok' : 'bad'}>
            {health?.status ?? 'unknown'}
          </strong>
          <p>Database: {health?.database ?? '-'}</p>
          <p>UTC: {health?.utcNow ?? '-'}</p>
          <button type="button" className="btn primary" onClick={() => void refresh()}>
            Оновити
          </button>
        </div>
      </Panel>
      {error ? <p className="error-box">{error}</p> : null}
    </div>
  );
}

