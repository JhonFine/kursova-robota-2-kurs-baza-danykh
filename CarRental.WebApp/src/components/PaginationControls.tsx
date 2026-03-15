interface PaginationControlsProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  disabled?: boolean;
}

export function PaginationControls({
  page,
  pageSize,
  totalCount,
  onPageChange,
  disabled = false,
}: PaginationControlsProps) {
  const safePageSize = Math.max(pageSize, 1);
  const totalPages = Math.max(1, Math.ceil(totalCount / safePageSize));
  const currentPage = Math.min(Math.max(page, 1), totalPages);
  const from = totalCount === 0 ? 0 : (currentPage - 1) * safePageSize + 1;
  const to = Math.min(totalCount, currentPage * safePageSize);

  return (
    <div className="pagination-bar">
      <span className="pagination-meta">
        {from}-{to} із {totalCount}
      </span>
      <div className="pagination-actions">
        <button
          type="button"
          className="btn"
          disabled={disabled || currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          Назад
        </button>
        <span className="pagination-page">
          Сторінка {currentPage} / {totalPages}
        </span>
        <button
          type="button"
          className="btn"
          disabled={disabled || currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          Далі
        </button>
      </div>
    </div>
  );
}
