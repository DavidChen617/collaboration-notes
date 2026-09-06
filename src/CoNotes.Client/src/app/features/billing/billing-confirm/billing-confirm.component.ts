import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { BillingService } from '../billing.service';

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
      <p>正在確認付款狀態...</p>
    }
  `,
  styles: [],
})
export class BillingConfirmComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly billingService = inject(BillingService);

  protected readonly code = signal('');
  protected readonly errorMessage = signal('');

  constructor() {
    // PayPal 核准後導回時, 會在 return_url 後面帶上 ?token={orderId}&PayerID=...
    const orderId = this.route.snapshot.queryParamMap.get('token');
    if (!orderId) {
      this.errorMessage.set('找不到付款訂單，請重新走一次購買流程。');
      return;
    }

    this.billingService.confirmOrder(orderId).subscribe({
      next: ({ code }) => this.code.set(code),
      error: () => this.errorMessage.set('PayPal 付款尚未完成，請重新走一次購買流程。'),
    });
  }
}
