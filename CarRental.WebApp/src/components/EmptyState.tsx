import clsx from 'clsx';
import type { ReactNode } from 'react';

interface EmptyStateProps {
  icon?: string;
  title: string;
  description: ReactNode;
  actions?: ReactNode;
  className?: string;
  compact?: boolean;
}

export function EmptyState({
  icon = '...',
  title,
  description,
  actions,
  className,
  compact = false,
}: EmptyStateProps) {
  return (
    <section className={clsx('empty-state-card', compact && 'compact', className)}>
      <span className="empty-state-ornament" aria-hidden="true">
        {icon}
      </span>
      <div className="empty-state-copy">
        <strong>{title}</strong>
        <p>{description}</p>
      </div>
      {actions ? <div className="empty-state-actions">{actions}</div> : null}
    </section>
  );
}

