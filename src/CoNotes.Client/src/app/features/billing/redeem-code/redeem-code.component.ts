import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { BillingService } from '../billing.service';

@Component({
  selector: 'app-redeem-code',
  imports: [FormsModule],
  template: `
    <h1>兌換 license code</h1>

    <form (ngSubmit)="redeem()">
      <label>
        License code
        <input type="text" [(ngModel)]="code" name="code" required />
      </label>
      <button type="submit">兌換</button>
    </form>

    @if (successMessage()) {
      <p>{{ successMessage() }}</p>
    }
    @if (errorMessage()) {
      <p role="alert">{{ errorMessage() }}</p>
    }
  `,
  styles: [],
})
export class RedeemCodeComponent {
  private readonly billingService = inject(BillingService);

  protected code = '';
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');

  protected redeem(): void {
    this.successMessage.set('');
    this.errorMessage.set('');

    this.billingService.redeemLicenseCode(this.code).subscribe({
      next: ({ planTier }) => this.successMessage.set(`兌換成功！你的訂閱等級已更新為 ${planTier}。`),
      error: () => this.errorMessage.set('兌換失敗：這組 code 無效或已經被兌換過。'),
    });
  }
}
