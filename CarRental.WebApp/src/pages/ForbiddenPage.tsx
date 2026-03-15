import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

export function ForbiddenPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = (): void => {
    logout();
    navigate('/login', { replace: true });
  };

  let title = 'Доступ до цього розділу заборонений';
  let message = 'Для вашої ролі цей маршрут недоступний.';

  if (user?.role === 'User') {
    title = 'Цей модуль доступний лише співробітникам';
    message = 'Внутрішні сторінки доступні тільки менеджерам та адміністраторам.';
  } else if (user?.role === 'Manager') {
    title = 'Доступ до адмін-модуля заборонений';
    message = 'Цей маршрут доступний лише користувачам з роллю Admin.';
  }

  return (
    <div className="page-grid">
      <section className="notice-card staff-notice">
        <span className="topbar-kicker">Доступ</span>
        <h2>{title}</h2>
        <p>{message}</p>
        <div className="inline-form">
          <button type="button" className="btn primary" onClick={handleLogout}>
            Вийти із сайту
          </button>
        </div>
      </section>
    </div>
  );
}
