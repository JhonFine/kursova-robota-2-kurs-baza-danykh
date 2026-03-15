export function LoadingView({ text = 'Завантаження...' }: { text?: string }) {
  return (
    <div className="loading-view">
      <div className="loading-dot" />
      <p>{text}</p>
    </div>
  );
}

