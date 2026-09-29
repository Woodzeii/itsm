import { Component } from '@angular/core';

@Component({
    selector: 'app-admin',
    standalone: true,
    template: `
    <section class="page">
      <h1>Админка</h1>
      <p>Заглушка для административной панели.</p>
    </section>
  `,
    styles: [
        `
      .page {
        min-height: 100vh;
        display: flex;
        flex-direction: column;
        justify-content: center;
        align-items: center;
        background: #fdf2f8;
        color: #1f2937;
        font-family: Arial, sans-serif;
      }

      h1 {
        margin: 0 0 12px;
        font-size: 2rem;
      }

      p {
        margin: 0;
        font-size: 1rem;
        color: #4b5563;
      }
    `,
    ],
})
export class AdminComponent { }
