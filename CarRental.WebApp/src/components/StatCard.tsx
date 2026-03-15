import type { ReactNode } from 'react';

export function StatCard({ label, value, accent }: { label: string; value: ReactNode; accent?: 'mint' | 'amber' | 'blue' | 'red' }) {
  return (
    <article className={`stat-card ${accent ?? 'mint'}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

