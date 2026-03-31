import clsx from 'clsx';

interface LoadingViewProps {
  text?: string;
  className?: string;
}

interface InlineSpinnerProps {
  className?: string;
}

export function LoadingView({ text = 'Завантаження...', className }: LoadingViewProps) {
  return (
    <div className={clsx('loading-view', className)}>
      <div className="loading-dot" />
      <p>{text}</p>
    </div>
  );
}

export function InlineSpinner({ className }: InlineSpinnerProps) {
  return <span className={clsx('ui-inline-spinner', className)} aria-hidden="true" />;
}
