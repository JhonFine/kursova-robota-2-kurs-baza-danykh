import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="page-grid">
      <div className="notice-card">
        <h2>404</h2>
        <p>Сторінку не знайдено.</p>
        <Link to="/" className="btn primary">Повернутись</Link>
      </div>
    </div>
  );
}

