import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { OverviewData } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-overview',
    standalone: true,
    imports: [CommonModule, RouterLink],
    template: `
    <section class="page">
      <header class="page-heading">
        <div><p class="eyebrow">{{ today }}</p><h1>Обзор</h1></div>
        <a class="primary-button" routerLink="/tickets" [queryParams]="{ create: true }"><span aria-hidden="true">+</span> Новая заявка</a>
      </header>

      @if (error) { <p class="notice error">{{ error }}</p> }
      @if (data; as overview) {
        <div class="metric-grid">
          @for (metric of overview.metrics; track metric.label) {
            <article class="metric"><span>{{ metric.label }}</span><strong>{{ metric.value }}</strong><small>{{ metric.change }} за неделю</small></article>
          }
        </div>
        <div class="content-grid">
          <section class="panel performance">
            <header class="panel-header"><div><h2>Производительность Service Desk</h2><p>Поступило и решено за неделю</p></div></header>
            <div class="chart">
              @for (point of overview.series; track point.label) {
                <div class="chart-day">
                  <div class="bar-pair"><span class="bar incoming" [style.height.%]="point.incoming"></span><span class="bar resolved" [style.height.%]="point.resolved"></span></div>
                  <small>{{ point.label }}</small>
                </div>
              }
            </div>
            <footer class="legend"><span><i class="incoming-dot"></i>Поступило</span><span><i class="resolved-dot"></i>Решено</span></footer>
          </section>
          <section class="panel quick-panel">
            <header class="panel-header"><div><h2>Быстрый переход</h2><p>Рабочие разделы</p></div></header>
            <a routerLink="/tickets"><span>Заявки</span><b>→</b></a>
            <a routerLink="/assets"><span>Реестр активов</span><b>→</b></a>
            <a routerLink="/service-catalog"><span>Каталог услуг</span><b>→</b></a>
            <a routerLink="/escalations"><span>Эскалации</span><b>{{ overview.activeEscalations }}</b></a>
          </section>
        </div>
        <section class="panel recent-panel">
          <header class="panel-header"><div><h2>Последние заявки</h2><p>Текущая очередь Service Desk</p></div><a routerLink="/tickets">Все заявки <span aria-hidden="true">→</span></a></header>
          <div class="table-scroll"><table><thead><tr><th>Заявка</th><th>Приоритет</th><th>Статус</th><th>SLA</th></tr></thead>
            <tbody>@for (ticket of overview.recentTickets; track ticket.id) {
              <tr><td><strong>{{ ticket.id }}</strong><span>{{ ticket.title }}</span><small>{{ ticket.requester }} · {{ ticket.team }}</small></td><td><span class="tag" [class.critical]="ticket.priority === 'Критический'" [class.high]="ticket.priority === 'Высокий'">{{ ticket.priority }}</span></td><td>{{ ticket.status }}</td><td>{{ ticket.slaRemaining }}</td></tr>
            }</tbody>
          </table></div>
        </section>
      } @else if (!error) { <p class="notice">Загрузка данных...</p> }
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; }
      .page { max-width:1440px; margin:auto; padding:28px 32px 40px; }
      .page-heading,.panel-header { display:flex; align-items:center; justify-content:space-between; gap:16px; }
      .page-heading { margin-bottom:22px; }
      .eyebrow,.panel-header p { margin:0 0 5px; color:#8b95a3; font-size:12px; }
      h1 { margin:0; font-size:24px; } h2 { margin:0; font-size:14px; }
      .primary-button { display:inline-flex; align-items:center; gap:8px; min-height:40px; padding:0 14px; border-radius:7px; background:#2563eb; color:white; text-decoration:none; font-size:12px; font-weight:650; }
      .primary-button span { font-size:20px; font-weight:400; }
      .metric-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:13px; margin-bottom:20px; }
      .metric,.panel { border:1px solid #e3e7ed; border-radius:8px; background:#fff; }
      .metric { display:flex; flex-direction:column; gap:8px; padding:16px; }
      .metric span,.metric small { color:#788392; font-size:11px; }
      .metric strong { color:#222b38; font-size:25px; }
      .metric small { color:#18825f; }
      .content-grid { display:grid; grid-template-columns:minmax(0,1fr) 290px; gap:16px; margin-bottom:18px; }
      .performance,.quick-panel,.recent-panel { padding:18px; }
      .panel-header { margin-bottom:18px; }
      .panel-header p { margin:4px 0 0; font-size:11px; }
      .chart { height:210px; display:flex; align-items:stretch; gap:14px; padding:8px 10px 0; border-bottom:1px solid #edf0f4; }
      .chart-day { min-width:0; flex:1; display:flex; flex-direction:column; align-items:center; justify-content:flex-end; gap:8px; }
      .chart-day small { color:#919baa; font-size:10px; }
      .bar-pair { width:100%; height:180px; display:flex; align-items:flex-end; justify-content:center; gap:4px; }
      .bar { width:min(32%,24px); min-height:5px; border-radius:4px 4px 0 0; }
      .incoming { background:#cbdcf7; } .resolved { background:#3475d1; }
      .legend { display:flex; gap:18px; padding-top:14px; color:#778291; font-size:10px; }
      .legend span { display:flex; align-items:center; gap:6px; }
      .legend i { width:8px; height:8px; border-radius:50%; } .incoming-dot { background:#cbdcf7; } .resolved-dot { background:#3475d1; }
      .quick-panel > a { min-height:42px; display:flex; align-items:center; justify-content:space-between; border-top:1px solid #eff1f4; color:#505b69; text-decoration:none; font-size:12px; }
      .quick-panel > a b { color:#3475d1; font-weight:600; }
      .panel-header > a { color:#3475d1; font-size:11px; text-decoration:none; white-space:nowrap; }
      .table-scroll { overflow-x:auto; }
      table { width:100%; border-collapse:collapse; text-align:left; }
      th { padding:9px 10px; border-bottom:1px solid #edf0f4; color:#9099a6; font-size:9px; text-transform:uppercase; }
      td { padding:11px 10px; border-bottom:1px solid #f0f2f5; color:#5d6877; font-size:11px; white-space:nowrap; }
      td:first-child { min-width:260px; white-space:normal; }
      td:first-child strong { margin-right:8px; color:#3475d1; font-size:10px; }
      td:first-child span { color:#394352; font-weight:600; }
      td:first-child small { display:block; margin:4px 0 0 0; color:#9aa3b1; font-size:10px; }
      .tag { display:inline-flex; padding:4px 7px; border-radius:4px; background:#f1f3f6; color:#687385; font-size:10px; }
      .tag.critical { background:#fff0f0; color:#c84c4c; } .tag.high { background:#fff2e8; color:#c66c30; }
      .notice { padding:14px; color:#697586; font-size:12px; } .error { color:#a93232; }
      @media(max-width:900px) { .content-grid { grid-template-columns:minmax(0,1fr); } .quick-panel { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); column-gap:16px; } .quick-panel .panel-header { grid-column:1/-1; } }
      @media(max-width:620px) { .page { padding:22px 16px 30px; } .page-heading { align-items:flex-start; flex-direction:column; } .metric-grid { grid-template-columns:repeat(2,minmax(0,1fr)); gap:9px; } .metric { padding:13px; } .metric strong { font-size:22px; } .chart { gap:6px; padding-right:0; padding-left:0; } .quick-panel { grid-template-columns:minmax(0,1fr); } .quick-panel .panel-header { grid-column:auto; } .performance,.quick-panel,.recent-panel { padding:14px; } }
    `],
})
export class OverviewComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    data: OverviewData | null = null;
    error = '';
    readonly today = new Intl.DateTimeFormat('ru-RU', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date());

    ngOnInit(): void {
        this.workspace.getOverview().subscribe({ next: data => this.data = data, error: () => this.error = 'Не удалось загрузить обзор.' });
    }
}