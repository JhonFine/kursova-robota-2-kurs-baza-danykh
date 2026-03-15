import clsx from 'clsx';
import type { ReactNode } from 'react';

export type ActiveFilterChipItem = {
  key: string;
  label: string;
  onRemove?: () => void;
  tone?: 'default' | 'accent';
};

interface FilterToolbarProps {
  children: ReactNode;
  actions?: ReactNode;
  chips?: ActiveFilterChipItem[];
  footerNote?: ReactNode;
  className?: string;
}

interface FilterFieldProps {
  label: ReactNode;
  hint?: ReactNode;
  children: ReactNode;
  className?: string;
}

interface FilterActionsProps {
  children: ReactNode;
  className?: string;
}

interface ActiveFilterChipsProps {
  items: ActiveFilterChipItem[];
  className?: string;
}

export function FilterToolbar({ children, actions, chips, footerNote, className }: FilterToolbarProps) {
  const hasFooter = Boolean(actions || footerNote || (chips && chips.length > 0));

  return (
    <section className={clsx('filter-toolbar', className)}>
      <div className="filter-toolbar-grid">{children}</div>
      {hasFooter ? (
        <div className="filter-toolbar-footer">
          <div className="filter-toolbar-footer-main">
            {footerNote ? <div className="filter-toolbar-note">{footerNote}</div> : null}
            {chips && chips.length > 0 ? <ActiveFilterChips items={chips} /> : null}
          </div>
          {actions ? <FilterActions>{actions}</FilterActions> : null}
        </div>
      ) : null}
    </section>
  );
}

export function FilterField({ label, hint, children, className }: FilterFieldProps) {
  return (
    <label className={clsx('filter-field', className)}>
      <span className="filter-field-label">{label}</span>
      {children}
      {hint ? <span className="filter-field-hint">{hint}</span> : null}
    </label>
  );
}

export function FilterActions({ children, className }: FilterActionsProps) {
  return <div className={clsx('filter-actions', className)}>{children}</div>;
}

export function ActiveFilterChips({ items, className }: ActiveFilterChipsProps) {
  return (
    <div className={clsx('active-filter-chips', className)}>
      {items.map((item) => {
        const chipClassName = clsx(
          'active-filter-chip',
          item.tone === 'accent' ? 'accent' : null,
          item.onRemove ? 'is-removable' : null,
        );

        if (!item.onRemove) {
          return (
            <span key={item.key} className={chipClassName}>
              {item.label}
            </span>
          );
        }

        return (
          <button
            key={item.key}
            type="button"
            className={chipClassName}
            onClick={item.onRemove}
            aria-label={`Прибрати фільтр "${item.label}"`}
          >
            <span>{item.label}</span>
            <strong aria-hidden="true">×</strong>
          </button>
        );
      })}
    </div>
  );
}
