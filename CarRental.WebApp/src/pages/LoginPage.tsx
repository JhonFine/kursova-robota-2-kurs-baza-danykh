import { useMemo, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { getDefaultPathByRole } from '../utils/access';

export function LoginPage() {
  const { isAuthenticated, user, login, register } = useAuth();
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [form, setForm] = useState({
    login: '',
    password: '',
    fullName: '',
    phone: '',
  });
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const defaultPath = useMemo(() => getDefaultPathByRole(user?.role), [user?.role]);
  const screenKicker = 'Вхід на сайт';
  const screenTitle = mode === 'login'
    ? 'Вхід до системи'
    : 'Створення облікового запису клієнта';
  const introCopy = mode === 'login'
    ? 'Увійдіть під своїм обліковим записом: клієнти потрапляють у self-service, а менеджери та адміністратори бачать внутрішні розділи за своєю роллю.'
    : 'Після реєстрації система одразу авторизує вас і відкриє клієнтський self-service.';

  if (isAuthenticated) {
    return <Navigate to={defaultPath} replace />;
  }

  const onSubmit = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setError(null);

    if (mode === 'register') {
      if (form.password.length < 8) {
        setError('Мінімальна довжина пароля: 8 символів.');
        return;
      }

      if (form.password !== confirmPassword) {
        setError('Паролі не співпадають.');
        return;
      }

      if (!form.fullName.trim()) {
        setError("Вкажіть ПІБ.");
        return;
      }
    }

    try {
      setLoading(true);
      if (mode === 'login') {
        await login(form.login.trim(), form.password);
      } else {
        await register({
          fullName: form.fullName.trim(),
          login: form.login.trim(),
          phone: form.phone.trim(),
          password: form.password,
        });
      }

      navigate('/', { replace: true });
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  };

  const switchMode = (nextMode: 'login' | 'register'): void => {
    setMode(nextMode);
    setError(null);
    setShowPassword(false);
    setShowConfirmPassword(false);
  };

  return (
    <div className="auth-screen">
      <div className="auth-card">
        <span className="topbar-kicker">{screenKicker}</span>
        <h1>{screenTitle}</h1>
        <p>{introCopy}</p>

        <div className="auth-mode-tabs" role="tablist" aria-label="Режим авторизації">
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'login'}
            className={`auth-mode-tab${mode === 'login' ? ' active' : ''}`}
            onClick={() => switchMode('login')}
          >
            Вхід
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'register'}
            className={`auth-mode-tab${mode === 'register' ? ' active' : ''}`}
            onClick={() => switchMode('register')}
          >
            Реєстрація
          </button>
        </div>

        <form onSubmit={onSubmit} className="auth-form">
          {mode === 'register' ? (
            <label>
              <span className="auth-field-header">ПІБ</span>
              <input
                required
                autoComplete="name"
                value={form.fullName}
                onChange={(event) => setForm((prev) => ({ ...prev, fullName: event.target.value }))}
                placeholder="Іваненко Іван Іванович"
              />
            </label>
          ) : null}

          <label>
            <span className="auth-field-header">Ім'я для входу</span>
            <input
              required
              autoComplete={mode === 'login' ? 'username' : 'new-username'}
              value={form.login}
              onChange={(event) => setForm((prev) => ({ ...prev, login: event.target.value }))}
              placeholder="client01"
            />
            <span className="auth-field-hint">Використовуйте коротке ім'я латиницею без пробілів.</span>
          </label>

          {mode === 'register' ? (
            <label>
              <span className="auth-field-header">Телефон</span>
              <input
                required
                type="tel"
                autoComplete="tel"
                value={form.phone}
                onChange={(event) => setForm((prev) => ({ ...prev, phone: event.target.value }))}
                placeholder="+380501112233"
              />
              <span className="auth-field-hint">Номер потрібен для зв'язку по бронюванню та ідентифікації клієнта.</span>
            </label>
          ) : null}

          <label>
            <span className="auth-field-header">Пароль</span>
            <div className="password-input-wrap">
              <input
                required
                type={showPassword ? 'text' : 'password'}
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                value={form.password}
                onChange={(event) => setForm((prev) => ({ ...prev, password: event.target.value }))}
                placeholder="********"
              />
              <button
                type="button"
                className="password-toggle"
                onClick={() => setShowPassword((value) => !value)}
              >
                {showPassword ? 'Сховати' : 'Показати'}
              </button>
            </div>
            {mode === 'register' ? (
              <span className="auth-field-hint">Мінімум 8 символів. Краще використати пароль, який ви більше ніде не повторюєте.</span>
            ) : null}
          </label>

          {mode === 'register' ? (
            <label>
              <span className="auth-field-header">Підтвердження пароля</span>
              <div className="password-input-wrap">
                <input
                  required
                  type={showConfirmPassword ? 'text' : 'password'}
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  placeholder="********"
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowConfirmPassword((value) => !value)}
                >
                  {showConfirmPassword ? 'Сховати' : 'Показати'}
                </button>
              </div>
            </label>
          ) : null}

          {error ? <p className="error-box">{error}</p> : null}

          <button type="submit" className="btn primary" disabled={loading}>
            {loading ? 'Завантаження...' : mode === 'login' ? 'Увійти' : 'Створити обліковий запис'}
          </button>
        </form>

        <div className="auth-card-actions">
          <p className="auth-note">
            {mode === 'login'
              ? 'Немає облікового запису? Перейдіть на вкладку Реєстрація, щоб одразу потрапити на свою сторінку.'
              : "Вже маєте обліковий запис? Перейдіть на вкладку Вхід і використайте своє ім'я для входу та пароль."}
          </p>
        </div>
      </div>
    </div>
  );
}
