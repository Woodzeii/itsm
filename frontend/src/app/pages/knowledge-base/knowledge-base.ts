import { Component } from '@angular/core';

@Component({
    selector: 'app-knowledge-base',
    standalone: true,
    template: `
    <section class="page">
      <header>
        <p class="eyebrow">Рабочее пространство</p>
        <h1>База знаний</h1>
      </header>
      <div class="empty-state">
        <span class="icon" aria-hidden="true">i</span>
        <h2>Раздел находится в наполнении</h2>
        <p>Материалы базы знаний появятся здесь позже.</p>
      </div>
    </section>
  `,
    styles: [`
      :host { display: block; color: #202734; }
      .page { max-width: 1100px; margin: auto; padding: 28px 32px 40px; }
      .eyebrow { margin: 0 0 5px; color: #8b95a3; font-size: 11px; }
      h1 { margin: 0; font-size: 24px; }
      .empty-state { display: grid; justify-items: center; margin-top: 24px; padding: 64px 24px; border: 1px solid #e3e7ed; border-radius: 8px; background: #fff; text-align: center; }
      .icon { display: grid; width: 40px; height: 40px; place-items: center; border-radius: 50%; background: #eef4ff; color: #3475d1; font-weight: 700; }
      h2 { margin: 16px 0 6px; font-size: 17px; }
      .empty-state p { margin: 0; color: #778291; font-size: 13px; }
    `],
})
export class KnowledgeBaseComponent { }
