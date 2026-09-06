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

  confirmOrder(orderId: string): Observable<{ code: string }> {
    return this.http.post<{ code: string }>(`${this.baseUrl}/orders/${orderId}/confirm`, null);
  }

  redeemLicenseCode(code: string): Observable<RedeemLicenseCodeResult> {
    return this.http.post<RedeemLicenseCodeResult>(`${this.baseUrl}/license-codes/redeem`, { code });
  }
}
