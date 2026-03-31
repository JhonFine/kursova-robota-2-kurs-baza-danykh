import { useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import type { UserRole } from '../api/types';
import { useAuth } from '../auth/useAuth';
import { ClientHelpPanel } from '../components/ClientHelpPanel';
import { getDefaultPathByRole, getNavigationSections, getPageMeta } from '../utils/access';

const roleLabelByRole: Record<UserRole, string> = {
  Admin: 'Адміністратор',
  Manager: 'Менеджер',
  User: 'Клієнт',
};

const clientTabs = [
  { label: 'Підібрати авто', path: '/prokat/search' },
  { label: 'Мої бронювання', path: '/prokat/bookings' },
  { label: 'Профіль', path: '/prokat/profile' },
] as const;

// Клієнтський shell не має лівого staff-меню, тому заголовок і help-view
// повністю залежать від поточного підрозділу self-service сценарію.
function resolveClientHeader(pathname: string): {
  title: string;
  description: string;
  helpView: 'search' | 'bookings';
} {
  if (pathname.startsWith('/prokat/profile')) {
    return {
      title: 'Профіль клієнта',
      description: 'Оновлюйте контакти, документи та пароль перед новими бронюваннями.',
      helpView: 'bookings',
    };
  }

  if (pathname.startsWith('/prokat/bookings')) {
    return {
      title: 'Мої бронювання',
      description: 'Переглядайте майбутні, активні та завершені оренди без змішування з каталогом.',
      helpView: 'bookings',
    };
  }

  return {
    title: 'Онлайн-прокат авто',
    description: 'Підбір авто, вибір конкретного екземпляра і оформлення оренди з оплатою карткою в одному сценарії.',
    helpView: 'search',
  };
}

export function AppShell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [isClientHelpOpen, setIsClientHelpOpen] = useState(false);

  const showStaffShell = user?.role === 'Admin' || user?.role === 'Manager';
  const navigationSections = getNavigationSections(user?.role);
  const pageMeta = getPageMeta(location.pathname);
  const defaultPath = getDefaultPathByRole(user?.role);
  const clientHeader = resolveClientHeader(location.pathname);

  const handleLogout = (): void => {
    logout();
    navigate('/login', { replace: true });
  };

  // Один shell обслуговує одразу дві різні інформаційні архітектури:
  // клієнтський self-service і staff/admin кабінет.
  if (!showStaffShell) {
    return (
      <div className="client-shell">
        <header className="client-header">
          <div className="client-header-copy">
            <div className="client-header-kicker-row">
              <span className="topbar-kicker">Сайт для клієнтів</span>
              <button
                type="button"
                className={`client-top-nav-btn client-help-toggle${isClientHelpOpen ? ' active' : ''}`}
                aria-expanded={isClientHelpOpen}
                onClick={() => setIsClientHelpOpen((value) => !value)}
              >
                Як це працює
              </button>
            </div>

            <h1 className="topbar-title">{clientHeader.title}</h1>
            <p className="topbar-subtitle">{clientHeader.description}</p>

            <div className="client-top-nav" aria-label="Клієнтська навігація">
              {clientTabs.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  className={({ isActive }) => `client-top-nav-btn${isActive ? ' active' : ''}`}
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
          </div>

          <div className="topbar-actions">
            <div className="topbar-user-card">
              <div className="topbar-user-emblem" aria-hidden="true">CR</div>
              <div className="topbar-user-meta">
                <strong>{user?.fullName}</strong>
                <span className="topbar-role">{user ? roleLabelByRole[user.role] : 'Користувач'}</span>
              </div>
            </div>
            <button type="button" className="btn ghost topbar-logout-btn" onClick={handleLogout}>
              Вийти
            </button>
          </div>
        </header>

        <ClientHelpPanel
          open={isClientHelpOpen}
          view={clientHeader.helpView}
          onClose={() => setIsClientHelpOpen(false)}
        />

        <main className="client-content">
          <Outlet />
        </main>
      </div>
    );
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <button
          type="button"
          className="brand-card"
          onClick={() => navigate(defaultPath, { replace: true })}
        >
          <span className="brand-kicker">Сайт для працівників</span>
          <strong>CarRental</strong>
          <span className="brand-copy">
            Внутрішня сторінка для щоденної роботи менеджера та адміністратора.
          </span>
        </button>

        <nav className="sidebar-nav" aria-label="Основна навігація">
          {navigationSections.map((section) => (
            <section key={section.section} className="sidebar-section">
              <p className="sidebar-section-label">{section.section}</p>
              <div className="sidebar-links">
                {section.items.map((item) => (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
                  >
                    <span className="nav-link-title">{item.label}</span>
                    <span className="nav-link-copy">{item.description}</span>
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </nav>
      </aside>

      <div className="content-area">
        <header className="topbar">
          <div className="topbar-copy">
            <span className="topbar-kicker">{pageMeta.section}</span>
            <h1 className="topbar-title">{pageMeta.label}</h1>
            <p className="topbar-subtitle">{pageMeta.description}</p>
          </div>

          <div className="topbar-actions">
            <div className="topbar-user-card">
              <div className="topbar-user-emblem" aria-hidden="true">CR</div>
              <div className="topbar-user-meta">
                <strong>{user?.fullName}</strong>
                <span className="topbar-role">{user ? roleLabelByRole[user.role] : 'Користувач'}</span>
              </div>
            </div>
            <button type="button" className="btn ghost topbar-logout-btn" onClick={handleLogout}>
              Вийти
            </button>
          </div>
        </header>

        <main className="page-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
