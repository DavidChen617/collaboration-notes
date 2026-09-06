import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/app-config';
import { CreateOrderResult, PlanTier, RedeemLicenseCodeResult } from './billing.model';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/api/v1/billing`;

  createOrder(planTier: PlanTier): Observable<CreateOrderResult> {
    return this.http.post<CreateOrderResult>(`${this.baseUrl}/orders`, {
      planTier,
      returnUrl: `${globalThis.location.origin}/billing/confirm`,
      cancelUrl: `${globalThis.location.origin}/billing/plans`,
    });
  }

  /** 觸發 PayPal capture 本身;真的產生 code 是由 PayPal 送來的 webhook 觸發, 見 getLicenseCodeForOrder。 */
  confirmOrder(orderId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/orders/${orderId}/confirm`, null);
  }

  /** webhook 送達是非同步的, 呼叫這個 endpoint 輪詢 code 是否已經產生;還沒產生時 404。 */
  getLicenseCodeForOrder(orderId: string): Observable<{ code: string }> {
    return this.http.get<{ code: string }>(`${this.baseUrl}/orders/${orderId}/license-code`);
  }

  redeemLicenseCode(code: string): Observable<RedeemLicenseCodeResult> {
    return this.http.post<RedeemLicenseCodeResult>(`${this.baseUrl}/license-codes/redeem`, { code });
  }
}
