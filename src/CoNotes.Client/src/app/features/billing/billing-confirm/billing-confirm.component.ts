import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { BillingService } from '../billing.service';

const POLL_INTERVAL_MS = 1000;
const MAX_POLL_ATTEMPTS = 20;

@Component({
  selector: 'app-billing-confirm',
  imports: [RouterLink],
  template: `
    <h1>確認付款</h1>

    @if (code(); as licenseCode) {
      <p>付款完成！你的 license code：</p>
      <input type="text" readonly [value]="licenseCode" />
      <p><a routerLink="/billing/redeem">前往兌換</a></p>
    } @else if (errorMessage()) {
      <p role="alert">{{ errorMessage() }}</p>
      <a routerLink="/billing/plans">返回訂閱方案</a>
    } @else {
      <p>{{ statusMessage() }}</p>
    }
  `,
  styles: [],
})
export class BillingConfirmComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly billingService = inject(BillingService);

  protected readonly code = signal('');
  protected readonly errorMessage = signal('');
  protected readonly statusMessage = signal('正在確認付款狀態...');

  constructor() {
    // PayPal 核准後導回時, 會在 return_url 後面帶上 ?token={orderId}&PayerID=...
    const orderId = this.route.snapshot.queryParamMap.get('token');
    if (!orderId) {
      this.errorMessage.set('找不到付款訂單，請重新走一次購買流程。');
      return;
    }

    this.billingService.confirmOrder(orderId).subscribe({
      next: () => {
        this.statusMessage.set('付款處理中，正在等待 license code 產生...');
        this.pollForLicenseCode(orderId, 0);
      },
      error: () => this.errorMessage.set('PayPal 付款尚未完成，請重新走一次購買流程。'),
    });
  }

  private pollForLicenseCode(orderId: string, attempt: number): void {
    this.billingService.getLicenseCodeForOrder(orderId).subscribe({
      next: ({ code }) => this.code.set(code),
      error: () => {
        if (attempt >= MAX_POLL_ATTEMPTS) {
          this.errorMessage.set('等待 license code 逾時，請稍後至兌換頁重新查詢，或聯絡系統管理者。');
          return;
        }
        setTimeout(() => this.pollForLicenseCode(orderId, attempt + 1), POLL_INTERVAL_MS);
      },
    });
  }
}
