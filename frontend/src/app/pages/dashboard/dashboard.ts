import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-dashboard',
    standalone: true,
    template: `
    <section class="dashboard">
      <div class="welcome-row">
        <div>
          <p class="date-line">Понедельник, 14 октября 2026</p>
          <h1>Добрый день</h1>
        </div>
        <button class="primary-action" type="button"><span aria-hidden="true">+</span> Новая заявка</button>
      </div>

      <div class="metrics-grid">
        <article class="metric-card">
          <div><span class="metric-label">Всего заявок</span><strong>128</strong></div>
          <span class="metric-icon blue" aria-hidden="true"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8.5"/><path d="M12 3.5v4M12 16.5v4M3.5 12h4m9 0h4"/></svg></span>
          <p class="metric-change">+12% <span>к прошлой неделе</span></p>
        </article>
        <article class="metric-card">
          <div><span class="metric-label">Новые</span><strong>24</strong></div>
          <span class="metric-icon violet" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg></span>
          <p class="metric-change">+5 <span>к прошлой неделе</span></p>
        </article>
        <article class="metric-card">
          <div><span class="metric-label">В работе</span><strong>67</strong></div>
          <span class="metric-icon amber" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="m14.5 6.5 3-3a5 5 0 0 1-6.7 6.7L5 16a2.1 2.1 0 0 0 3 3l5.8-5.8a5 5 0 0 0 6.7-6.7l-3 3-3-3Z"/></svg></span>
          <p class="metric-change">+8% <span>к прошлой неделе</span></p>
        </article>
        <article class="metric-card">
          <div><span class="metric-label">SLA соблюдён</span><strong>94,2%</strong></div>
          <span class="metric-icon green" aria-hidden="true"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="m8 12 2.5 2.5L16.5 9"/></svg></span>
          <p class="metric-change">+2,4% <span>к прошлой неделе</span></p>
        </article>
      </div>

      <div class="dashboard-grid">
        <section class="queue-panel">
          <header class="panel-heading">
            <div><h2>Очередь заявок</h2><p>Последние обращения в Service Desk</p></div>
            <button class="secondary-action" type="button"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5h16l-6.5 7.5V19l-3 1v-7.5L4 5Z"/></svg>Фильтры</button>
          </header>
          <div class="queue-tools">
            <label class="search-box"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.8" cy="10.8" r="6.3"/><path d="m15.5 15.5 4 4"/></svg><input type="search" placeholder="Поиск по номеру, теме или автору" aria-label="Поиск по заявкам" /></label>
            <select aria-label="Статус заявки"><option>Все заявки</option><option>Новая</option><option>В работе</option><option>Ожидание</option><option>Эскалация</option></select>
          </div>
          <div class="table-scroll">
            <table>
              <thead><tr><th>Заявка</th><th>Приоритет</th><th>Статус</th><th>SLA</th><th></th></tr></thead>
              <tbody>
                <tr><td><div class="ticket-title"><span class="ticket-id">INC-1048</span><strong>Не подключается VPN после обновления</strong><small>Анна Белова · Сетевая команда</small></div></td><td><span class="tag priority-critical">Критический</span></td><td><span class="tag status-working">В работе</span></td><td><span class="sla-warn">01:18</span></td><td><button class="row-more" type="button" aria-label="Действия с заявкой INC-1048">···</button></td></tr>
                <tr><td><div class="ticket-title"><span class="ticket-id">SR-1047</span><strong>Выдать доступ к корпоративному диску</strong><small>Михаил Орлов · Сервис-деск</small></div></td><td><span class="tag priority-high">Высокий</span></td><td><span class="tag status-new">Новая</span></td><td>03:42</td><td><button class="row-more" type="button" aria-label="Действия с заявкой SR-1047">···</button></td></tr>
                <tr><td><div class="ticket-title"><span class="ticket-id">INC-1046</span><strong>Пропал доступ к принтеру на 3 этаже</strong><small>Елена Смирнова · Сервис-деск</small></div></td><td><span class="tag priority-medium">Средний</span></td><td><span class="tag status-waiting">Ожидание</span></td><td>05:26</td><td><button class="row-more" type="button" aria-label="Действия с заявкой INC-1046">···</button></td></tr>
                <tr><td><div class="ticket-title"><span class="ticket-id">SR-1045</span><strong>Установка лицензии Figma</strong><small>Дмитрий Волков · ИТ-отдел</small></div></td><td><span class="tag priority-low">Низкий</span></td><td><span class="tag status-working">В работе</span></td><td>07:10</td><td><button class="row-more" type="button" aria-label="Действия с заявкой SR-1045">···</button></td></tr>
                <tr><td><div class="ticket-title"><span class="ticket-id">INC-1044</span><strong>Медленная работа CRM</strong><small>Ольга Кузнецова · Приложения</small></div></td><td><span class="tag priority-high">Высокий</span></td><td><span class="tag status-escalation">Эскалация</span></td><td class="sla-warn">00:34</td><td><button class="row-more" type="button" aria-label="Действия с заявкой INC-1044">···</button></td></tr>
              </tbody>
            </table>
          </div>
          <footer class="queue-footer"><span>Показано 5 из 128 заявок</span><button type="button">Все заявки <span aria-hidden="true">→</span></button></footer>
        </section>

        <aside class="right-rail">
          <section class="activity-panel">
            <header class="activity-heading"><div><h2>Активность</h2><p>Сегодня, 14 октября</p></div><button class="row-more" type="button" aria-label="Другие события">···</button></header>
            <div class="activity-list">
              <div class="activity-item"><span class="activity-icon activity-blue" aria-hidden="true"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 8v4m0 4h.01"/></svg></span><div><p>Заявка INC-1048 назначена вам</p><small>10:42</small></div></div>
              <div class="activity-item"><span class="activity-icon activity-violet" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="m14.5 6.5 3-3a5 5 0 0 1-6.7 6.7L5 16a2.1 2.1 0 0 0 3 3l5.8-5.8a5 5 0 0 0 6.7-6.7l-3 3-3-3Z"/></svg></span><div><p>Изменён статус SR-1045</p><small>10:18</small></div></div>
              <div class="activity-item"><span class="activity-icon activity-green" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z"/><path d="m4.5 7.5 7.5 4.3 7.5-4.3M12 11.8V21"/></svg></span><div><p>Актив LT-00231 выдан со склада</p><small>09:47</small></div></div>
            </div>
          </section>
          <section class="sla-panel"><div class="sla-heading"><div><p>SLA сегодня</p><strong>94,2%</strong></div><span class="clock-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg></span></div><div class="progress-track"><span></span></div><div class="sla-foot"><span>Цель 90%</span><span>+2,4% к вчера</span></div></section>
          @if (isAdmin) {
            <section class="admin-note"><h2>Администратору</h2><p>Отчёты <strong>24</strong></p><p>Новые заявки <strong>5</strong></p></section>
          }
        </aside>
      </div>
    </section>
  `,
    styles: [
        `
      :host { display: block; }
      .dashboard { width: min(100%, 1440px); margin: 0 auto; padding: 28px 32px 36px; }
      .welcome-row { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; margin-bottom: 25px; }
      .date-line { margin: 0 0 5px; color: #8a94a3; font-size: 12px; }
      h1 { margin: 0; color: #171d29; font-size: 24px; line-height: 1.3; font-weight: 700; }
      .primary-action, .secondary-action { display: inline-flex; align-items: center; justify-content: center; gap: 8px; border-radius: 7px; font: inherit; cursor: pointer; white-space: nowrap; }
      .primary-action { min-height: 40px; padding: 0 15px; border: 1px solid #2563eb; background: #2563eb; color: #fff; font-size: 12px; font-weight: 650; box-shadow: 0 2px 4px #2563eb24; }
      .primary-action:hover { background: #1d55cb; }
      .primary-action span { font-size: 19px; font-weight: 400; line-height: 1; }
      .metrics-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; margin-bottom: 25px; }
      .metric-card, .queue-panel, .activity-panel { background: #fff; border: 1px solid #e4e8ee; border-radius: 9px; box-shadow: 0 1px 2px #0f172a08; }
      .metric-card { min-height: 116px; padding: 16px; display: grid; grid-template-columns: 1fr auto; align-content: start; row-gap: 8px; }
      .metric-label { display: block; margin-bottom: 8px; color: #697586; font-size: 11px; }
      .metric-card strong { display: block; color: #202734; font-size: 24px; line-height: 1.1; font-weight: 700; }
      .metric-icon { width: 32px; height: 32px; display: grid; place-items: center; border-radius: 8px; }
      .metric-icon svg, .secondary-action svg, .search-box svg, .activity-icon svg, .clock-icon svg { width: 17px; height: 17px; fill: none; stroke: currentColor; stroke-width: 1.7; stroke-linecap: round; stroke-linejoin: round; }
      .metric-icon.blue { background: #eff6ff; color: #2870dc; }
      .metric-icon.violet { background: #f4f0ff; color: #825bd6; }
      .metric-icon.amber { background: #fff7e8; color: #c78319; }
      .metric-icon.green { background: #eaf8f1; color: #20966a; }
      .metric-change { grid-column: 1 / -1; margin: 0; color: #119267; font-size: 10px; font-weight: 650; }
      .metric-change span { color: #9aa3b1; font-weight: 400; }
      .dashboard-grid { display: grid; grid-template-columns: minmax(0, 1fr) 288px; align-items: start; gap: 20px; }
      .queue-panel { min-width: 0; overflow: hidden; }
      .panel-heading, .activity-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
      .panel-heading { min-height: 72px; padding: 15px 18px; border-bottom: 1px solid #eef0f4; }
      h2 { margin: 0; color: #28303c; font-size: 13px; line-height: 1.4; font-weight: 650; }
      .panel-heading p, .activity-heading p { margin: 3px 0 0; color: #9aa3b1; font-size: 10px; }
      .secondary-action { min-height: 32px; padding: 0 10px; border: 1px solid #e1e5eb; background: #fff; color: #596475; font-size: 10px; font-weight: 550; }
      .secondary-action svg { width: 14px; height: 14px; }
      .secondary-action:hover { background: #f8f9fb; }
      .queue-tools { display: flex; gap: 10px; padding: 12px 16px; border-bottom: 1px solid #eef0f4; }
      .search-box { min-width: 0; height: 34px; flex: 1; display: flex; align-items: center; gap: 8px; padding: 0 10px; border: 1px solid #e2e6ed; border-radius: 6px; background: #f8f9fb; color: #9aa3b1; }
      .search-box svg { width: 15px; height: 15px; flex: 0 0 auto; }
      .search-box input { width: 100%; min-width: 0; padding: 0; border: 0; outline: 0; background: transparent; color: #394252; font-size: 10px; }
      .search-box input::placeholder { color: #a0a8b4; }
      .queue-tools select { min-width: 112px; height: 34px; padding: 0 9px; border: 1px solid #e2e6ed; border-radius: 6px; background: #fff; color: #596475; font-size: 10px; }
      .table-scroll { width: 100%; overflow-x: auto; }
      table { width: 100%; min-width: 620px; border-collapse: collapse; text-align: left; }
      th { height: 34px; padding: 0 10px; border-bottom: 1px solid #eef0f4; color: #9aa3b1; font-size: 9px; font-weight: 650; text-transform: uppercase; }
      th:first-child { padding-left: 18px; }
      th:last-child { width: 32px; }
      td { height: 63px; padding: 8px 10px; border-bottom: 1px solid #f1f3f6; color: #586273; font-size: 10px; white-space: nowrap; }
      td:first-child { padding-left: 18px; }
      tbody tr:last-child td { border-bottom: 0; }
      tbody tr:hover { background: #fafbfd; }
      .ticket-title { display: grid; grid-template-columns: auto minmax(0, 1fr); align-items: baseline; column-gap: 8px; row-gap: 3px; }
      .ticket-id { color: #3270cb; font-size: 9px; font-weight: 650; }
      .ticket-title strong { overflow: hidden; color: #394252; font-size: 10px; font-weight: 600; text-overflow: ellipsis; }
      .ticket-title small { grid-column: 2; overflow: hidden; color: #9aa3b1; font-size: 9px; text-overflow: ellipsis; }
      .tag { display: inline-flex; align-items: center; min-height: 20px; padding: 0 7px; border-radius: 5px; font-size: 9px; font-weight: 600; }
      .priority-critical { background: #fff0f0; color: #c84c4c; }
      .priority-high { background: #fff2e8; color: #c66c30; }
      .priority-medium { background: #fff8e6; color: #a97a18; }
      .priority-low { background: #f1f3f6; color: #687385; }
      .status-working { background: #f1edff; color: #7154bf; }
      .status-new { background: #edf5ff; color: #3270cb; }
      .status-waiting { background: #fff7e8; color: #a97a18; }
      .status-escalation { background: #fff0f0; color: #c84c4c; }
      .sla-warn { color: #c66c30; font-weight: 650; }
      .row-more { width: 25px; height: 25px; padding: 0; border: 0; border-radius: 5px; background: transparent; color: #8d97a5; font-size: 17px; line-height: 1; cursor: pointer; }
      .row-more:hover { background: #f1f3f6; }
      .queue-footer { min-height: 42px; display: flex; align-items: center; justify-content: space-between; padding: 0 18px; border-top: 1px solid #eef0f4; color: #9aa3b1; font-size: 9px; }
      .queue-footer button { padding: 5px 0 5px 8px; border: 0; background: transparent; color: #3270cb; font: inherit; font-weight: 600; cursor: pointer; }
      .right-rail { display: flex; flex-direction: column; gap: 16px; }
      .activity-panel { padding: 16px; }
      .activity-heading { margin-bottom: 19px; }
      .activity-list { display: flex; flex-direction: column; gap: 17px; }
      .activity-item { display: flex; align-items: flex-start; gap: 10px; }
      .activity-icon { width: 28px; height: 28px; flex: 0 0 28px; display: grid; place-items: center; border-radius: 50%; }
      .activity-icon svg { width: 14px; height: 14px; }
      .activity-blue { background: #eff6ff; color: #2870dc; }
      .activity-violet { background: #f4f0ff; color: #825bd6; }
      .activity-green { background: #eaf8f1; color: #20966a; }
      .activity-item p { margin: 1px 0 3px; color: #596475; font-size: 10px; line-height: 1.5; }
      .activity-item small { color: #a0a8b4; font-size: 9px; }
      .sla-panel { padding: 17px; border-radius: 9px; background: #202936; color: #fff; }
      .sla-heading { display: flex; align-items: center; justify-content: space-between; margin-bottom: 15px; }
      .sla-heading p { margin: 0 0 5px; color: #aab3c0; font-size: 10px; }
      .sla-heading strong { font-size: 22px; line-height: 1; }
      .clock-icon { width: 32px; height: 32px; display: grid; place-items: center; border-radius: 7px; background: #ffffff18; color: #fff; }
      .clock-icon svg { width: 17px; height: 17px; }
      .progress-track { height: 5px; overflow: hidden; border-radius: 6px; background: #ffffff20; }
      .progress-track span { display: block; width: 94%; height: 100%; border-radius: inherit; background: #60a5fa; }
      .sla-foot { display: flex; justify-content: space-between; gap: 8px; margin-top: 10px; color: #aab3c0; font-size: 9px; }
      .admin-note { padding: 15px 16px; border: 1px solid #e4e8ee; border-radius: 9px; background: #fff; }
      .admin-note p { display: flex; justify-content: space-between; margin: 12px 0 0; color: #697586; font-size: 10px; }
      .admin-note p strong { color: #303846; }
      @media (max-width: 1120px) { .dashboard-grid { grid-template-columns: minmax(0, 1fr) 260px; gap: 14px; } .dashboard { padding-right: 24px; padding-left: 24px; } .metrics-grid { gap: 10px; } }
      @media (max-width: 900px) { .dashboard-grid { grid-template-columns: minmax(0, 1fr); } .right-rail { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); align-items: start; } .admin-note { grid-column: 1 / -1; } }
      @media (max-width: 620px) { .dashboard { padding: 22px 16px; } .welcome-row { align-items: flex-start; flex-direction: column; margin-bottom: 20px; } h1 { font-size: 22px; } .metrics-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; margin-bottom: 18px; } .metric-card { min-height: 105px; padding: 13px; } .metric-card strong { font-size: 22px; } .dashboard-grid { gap: 14px; } .panel-heading { padding: 13px; } .queue-tools { flex-direction: column; padding: 11px 12px; } .queue-tools select { width: 100%; } .right-rail { grid-template-columns: minmax(0, 1fr); } .admin-note { grid-column: auto; } }
    `,
    ],
})
export class DashboardComponent implements OnInit {
    isAdmin = false;

    constructor(private readonly authService: AuthService) { }

    ngOnInit(): void {
        this.isAdmin = this.authService.isAdmin();
    }
}
