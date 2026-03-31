import clsx from 'clsx';
import { useEffect } from 'react';
import type { ReactNode } from 'react';

type FeedbackTone = 'success' | 'error' | 'warning' | 'info';

interface FeedbackBannerProps {
  tone?: FeedbackTone;
  title?: string;
  children: ReactNode;
  className?: string;
  onDismiss?: () => void;
  autoHideMs?: number;
}

const iconByTone: Record<FeedbackTone, string> = {
  success: 'OK',
  error: '!',
  warning: '!',
  info: 'i',
};

export function FeedbackBanner({
  tone = 'info',
  title,
  children,
  className,
  onDismiss,
  autoHideMs,
}: FeedbackBannerProps) {
  useEffect(() => {
    if (!onDismiss || !autoHideMs) {
      return undefined;
    }

    const timeoutId = window.setTimeout(onDismiss, autoHideMs);
    return () => window.clearTimeout(timeoutId);
  }, [autoHideMs, onDismiss]);

  return (
    <section
      className={clsx('feedback-banner', `tone-${tone}`, className)}
      role={tone === 'error' ? 'alert' : 'status'}
      aria-live={tone === 'error' ? 'assertive' : 'polite'}
    >
      <span className="feedback-banner-icon" aria-hidden="true">
        {iconByTone[tone]}
      </span>

      <div className="feedback-banner-copy">
        {title ? <strong>{title}</strong> : null}
        <div>{children}</div>
      </div>

      {onDismiss ? (
        <button
          type="button"
          className="feedback-banner-dismiss"
          onClick={onDismiss}
          aria-label="Dismiss notification"
        >
          x
        </button>
      ) : null}
    </section>
  );
}

