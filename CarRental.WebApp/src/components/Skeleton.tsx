import clsx from 'clsx';

interface SkeletonBlockProps {
  className?: string;
  width?: string;
  height?: number | string;
}

interface StatCardSkeletonsProps {
  count?: number;
}

interface TableSkeletonProps {
  rows?: number;
  compact?: boolean;
}

interface CardSkeletonGridProps {
  count?: number;
  className?: string;
}

export function SkeletonBlock({ className, width, height }: SkeletonBlockProps) {
  return (
    <span
      className={clsx('ui-skeleton', className)}
      style={{
        width,
        height,
      }}
      aria-hidden="true"
    />
  );
}

export function StatCardSkeletons({ count = 4 }: StatCardSkeletonsProps) {
  return (
    <section className="stats-grid stat-skeleton-grid" aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <article key={index} className="stat-card stat-card-skeleton">
          <SkeletonBlock width="42%" height={12} />
          <SkeletonBlock width="58%" height={38} className="is-spaced" />
          <SkeletonBlock width="30%" height={10} />
        </article>
      ))}
    </section>
  );
}

export function TableSkeleton({ rows = 6, compact = false }: TableSkeletonProps) {
  return (
    <div className={clsx('table-skeleton', compact && 'compact')} aria-hidden="true">
      <div className="table-skeleton-head">
        <SkeletonBlock width="16%" height={12} />
        <SkeletonBlock width="18%" height={12} />
        <SkeletonBlock width="14%" height={12} />
        <SkeletonBlock width="12%" height={12} />
      </div>

      <div className="table-skeleton-body">
        {Array.from({ length: rows }, (_, index) => (
          <div key={index} className="table-skeleton-row">
            <SkeletonBlock width="15%" height={14} />
            <SkeletonBlock width="22%" height={14} />
            <SkeletonBlock width="18%" height={14} />
            <SkeletonBlock width="12%" height={14} />
            <SkeletonBlock width="14%" height={14} />
          </div>
        ))}
      </div>
    </div>
  );
}

export function CardSkeletonGrid({ count = 4, className }: CardSkeletonGridProps) {
  return (
    <div className={clsx('cards-skeleton-grid', className)} aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <article key={index} className="card-skeleton">
          <SkeletonBlock className="card-skeleton-media" />
          <div className="card-skeleton-body">
            <SkeletonBlock width="28%" height={12} />
            <SkeletonBlock width="58%" height={28} />
            <SkeletonBlock width="88%" height={12} />
            <SkeletonBlock width="74%" height={12} />
            <div className="card-skeleton-stats">
              <SkeletonBlock width="100%" height={64} />
              <SkeletonBlock width="100%" height={64} />
            </div>
            <SkeletonBlock width="100%" height={44} />
          </div>
        </article>
      ))}
    </div>
  );
}

