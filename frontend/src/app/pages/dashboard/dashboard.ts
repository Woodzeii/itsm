import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-dashboard',
    standalone: true,
    template: `
    <section class="page">
      <header class="header">
        <div>
          <p class="eyebrow">ITSM</p>
          <h1>Главная панель</h1>
        </div>
      </header>

      <div class="stats">
        <article class="stat-card">
          <span>Открытые задачи</span>
          <strong>12</strong>
        </article>
        <article class="stat-card">
          <span>В работе</span>
          <strong>7</strong>
        </article>
        <article class="stat-card">
          <span>Просрочено</span>
          <strong>3</strong>
        </article>
      </div>

      @if (isAdmin) {
        <section class="admin-panel">
          <h2>Административные функции</h2>
          <div class="admin-grid">
            <article class="admin-card">
              <span>Отчёты</span>
              <strong>24</strong>
            </article>
            <article class="admin-card">
              <span>Сводка по SLA</span>
              <strong>96%</strong>
            </article>
            <article class="admin-card">
              <span>Новые заявки</span>
              <strong>5</strong>
            </article>
          </div>
        </section>
      }
    </section>
  `,
    styles: [
        `
      .page {
        display: flex;
        flex-direction: column;
        gap: 24px;
      }

      .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
      }

      .eyebrow {
        margin: 0 0 6px;
        color: #4f46e5;
        font-size: 0.8rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }

      h1 {
        margin: 0;
        font-size: 2rem;
      }

      .stats,
      .admin-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 18px;
      }

      .stat-card,
      .admin-card {
        background: #ffffff;
        border: 1px solid #e5e7eb;
        border-radius: 16px;
        padding: 20px;
        box-shadow: 0 4px 12px rgba(15, 23, 42, 0.04);
      }

      .stat-card span,
      .admin-card span {
        display: block;
        color: #6b7280;
        font-size: 0.9rem;
        margin-bottom: 10px;
      }

      .stat-card strong,
      .admin-card strong {
        font-size: 2rem;
        color: #111827;
      }

      .admin-panel {
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        border-radius: 16px;
        padding: 20px;
      }

      .admin-panel h2 {
        margin: 0 0 16px;
      }
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
