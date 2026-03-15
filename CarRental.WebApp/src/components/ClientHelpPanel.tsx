interface ClientHelpPanelProps {
  open: boolean;
  view: 'search' | 'bookings';
  onClose: () => void;
}

const searchChecklist = [
  'Виберіть дату і час отримання та повернення авто.',
  'Звузьте каталог через пошук, клас авто, ціну або режим лише доступних.',
  'Оберіть модель, потім конкретний екземпляр авто, перевірте суму і підтвердіть оплату карткою.',
];

const bookingsChecklist = [
  'У Майбутніх бронюваннях видно записи, які вже створені й чекають початку.',
  'У Активних орендах показуються поточні договори, які ще тривають.',
  'Історія зберігає завершені та скасовані оренди, звідки можна забронювати авто знову.',
];

export function ClientHelpPanel({ open, view, onClose }: ClientHelpPanelProps) {
  if (!open) {
    return null;
  }

  const checklist = view === 'search' ? searchChecklist : bookingsChecklist;

  return (
    <section className="client-help-panel" aria-labelledby="client-help-title">
      <div className="client-help-panel-copy">
        <span className="topbar-kicker">Як це працює</span>
        <h2 id="client-help-title">
          {view === 'search' ? 'Швидкий сценарій оформлення' : 'Як керувати своїми бронюваннями'}
        </h2>
        <p>
          {view === 'search'
            ? 'Після підтвердження система створює оренду та фіксує початковий платіж карткою. У форму потрапляє лише маскований номер картки.'
            : 'Тут зібрані всі ваші записи. Майбутні бронювання можна скасувати, а з історії швидко повернутися до знайомого авто через повторне бронювання.'}
        </p>
      </div>

      <div className="client-help-panel-grid">
        <div className="client-help-card">
          <strong>Що зробити далі</strong>
          <ol className="client-help-list">
            {checklist.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ol>
        </div>

        <div className="client-help-card">
          <strong>Корисно знати</strong>
          <ul className="client-help-list">
            <li>Фільтри на сторінці підбору зберігаються в адресному рядку і залишаються навіть після перезавантаження сторінки.</li>
            <li>Якщо вибране авто зникло з каталогу через фільтри, блок перевірки підкаже як швидко повернути його в список.</li>
            <li>Оплата в checkout проходить як початковий платіж за договором, а повний запис одразу з’являється в історії оренд.</li>
          </ul>
        </div>
      </div>

      <div className="client-help-panel-actions">
        <button type="button" className="btn ghost" onClick={onClose}>
          Сховати підказки
        </button>
      </div>
    </section>
  );
}
