import { useEffect, useState } from 'react';

export interface ClientGuideStep {
  id: string;
  title: string;
  description: string[];
  targetIds?: string[];
}

interface HighlightRect {
  top: number;
  left: number;
  width: number;
  height: number;
}

interface ClientOnboardingGuideProps {
  open: boolean;
  currentStep: number;
  steps: ClientGuideStep[];
  onPrevious: () => void;
  onNext: () => void;
  onSkip: () => void;
  onFinish: () => void;
}

function findGuideTarget(step: ClientGuideStep): HTMLElement | null {
  if (!step.targetIds) {
    return null;
  }

  for (const targetId of step.targetIds) {
    const element = document.getElementById(targetId);
    if (element) {
      return element;
    }
  }

  return null;
}

function measureHighlightRect(element: HTMLElement): HighlightRect | null {
  const rect = element.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0) {
    return null;
  }

  const padding = 12;
  const top = Math.max(rect.top - padding, 12);
  const left = Math.max(rect.left - padding, 12);
  const maxWidth = Math.max(window.innerWidth - left - 12, 0);
  const maxHeight = Math.max(window.innerHeight - top - 12, 0);

  return {
    top,
    left,
    width: Math.min(rect.width + padding * 2, maxWidth),
    height: Math.min(rect.height + padding * 2, maxHeight),
  };
}

export function ClientOnboardingGuide({
  open,
  currentStep,
  steps,
  onPrevious,
  onNext,
  onSkip,
  onFinish,
}: ClientOnboardingGuideProps) {
  const step = steps[currentStep] ?? null;
  const [highlightRect, setHighlightRect] = useState<HighlightRect | null>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = originalOverflow;
    };
  }, [open]);

  useEffect(() => {
    if (!open || !step) {
      const frameId = window.requestAnimationFrame(() => {
        setHighlightRect(null);
      });

      return () => {
        window.cancelAnimationFrame(frameId);
      };
    }

    const target = findGuideTarget(step);
    if (!target) {
      const frameId = window.requestAnimationFrame(() => {
        setHighlightRect(null);
      });

      return () => {
        window.cancelAnimationFrame(frameId);
      };
    }

    let animationFrameId = 0;

    const updateHighlight = (): void => {
      setHighlightRect(measureHighlightRect(target));
    };

    target.scrollIntoView({
      behavior: 'smooth',
      block: 'center',
      inline: 'nearest',
    });

    animationFrameId = window.requestAnimationFrame(updateHighlight);
    const timeoutId = window.setTimeout(() => {
      animationFrameId = window.requestAnimationFrame(updateHighlight);
    }, 320);
    window.addEventListener('resize', updateHighlight);
    window.addEventListener('scroll', updateHighlight, true);

    return () => {
      window.clearTimeout(timeoutId);
      window.cancelAnimationFrame(animationFrameId);
      window.removeEventListener('resize', updateHighlight);
      window.removeEventListener('scroll', updateHighlight, true);
    };
  }, [open, step]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const handleKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        onSkip();
        return;
      }

      if (event.key === 'ArrowLeft' && currentStep > 0) {
        onPrevious();
        return;
      }

      if (event.key === 'ArrowRight') {
        if (currentStep >= steps.length - 1) {
          onFinish();
          return;
        }

        onNext();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [currentStep, onFinish, onNext, onPrevious, onSkip, open, steps.length]);

  if (!open || !step) {
    return null;
  }

  const isLastStep = currentStep >= steps.length - 1;

  return (
    <div
      className="client-guide-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="client-guide-title"
    >
      <div className="client-guide-scrim" aria-hidden="true" />
      {highlightRect ? (
        <div
          className="client-guide-highlight"
          aria-hidden="true"
          style={{
            top: `${highlightRect.top}px`,
            left: `${highlightRect.left}px`,
            width: `${highlightRect.width}px`,
            height: `${highlightRect.height}px`,
          }}
        />
      ) : null}

      <section className={`client-guide-card${highlightRect ? '' : ' centered'}`}>
        <div className="client-guide-step">
          Крок {currentStep + 1} з {steps.length}
        </div>
        <h2 id="client-guide-title">{step.title}</h2>

        <div className="client-guide-copy">
          {step.description.map((paragraph, index) => (
            <p key={`${step.id}-${index}`}>{paragraph}</p>
          ))}
        </div>

        <div className="client-guide-actions">
          <button type="button" className="btn ghost" onClick={onSkip}>
            Пропустити
          </button>

          <div className="client-guide-actions-main">
            <button
              type="button"
              className="btn ghost"
              onClick={onPrevious}
              disabled={currentStep === 0}
            >
              Назад
            </button>

            {isLastStep ? (
              <button type="button" className="btn primary" onClick={onFinish}>
                Завершити
              </button>
            ) : (
              <button type="button" className="btn primary" onClick={onNext}>
                Далі
              </button>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}
