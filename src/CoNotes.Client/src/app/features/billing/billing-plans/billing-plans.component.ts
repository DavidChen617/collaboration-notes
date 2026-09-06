import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { BillingService } from '../billing.service';
import { PlanTier } from '../billing.model';

@Component({
  selector: 'app-billing-plans',
  imports: [RouterLink],
  template: `
    <h1>訂閱方案</h1>

    <a routerLink="/billing/redeem">已經有 license code？點此兌換</a>

    @if (errorMessage()) {
      <p role="alert">{{ errorMessage() }}</p>
    }

    <section>
      <h2>Pro — US$5.00</h2>
      <p>可以為自己的筆記產生分享連結、邀請其他人共編。</p>
      <button type="button" [disabled]="isCreatingOrder()" (click)="purchase('Pro')">購買 Pro</button>
    </section>

    <section>
      <h2>ProMax — US$10.00</h2>
      <p>包含 Pro 的所有功能，另外可以在筆記聊天室使用 @AI 助理。</p>
      <button type="button" [disabled]="isCreatingOrder()" (click)="purchase('ProMax')">購買 ProMax</button>
    </section>
  `,
  styles: [],
})
export class BillingPlansComponent {
  private readonly billingService = inject(BillingService);

  protected readonly isCreatingOrder = signal(false);
  protected readonly errorMessage = signal('');

  protected purchase(planTier: PlanTier): void {
    this.isCreatingOrder.set(true);
    this.errorMessage.set('');

    this.billingService.createOrder(planTier).subscribe({
      next: ({ approvalUrl }) => {
        globalThis.location.href = approvalUrl;
      },
      error: () => {
        this.isCreatingOrder.set(false);
        this.errorMessage.set('建立訂單失敗，請稍後再試。');
      },
    });
  }
}
