export type PlanTier = 'Free' | 'Pro' | 'ProMax';

export interface CreateOrderResult {
  orderId: string;
  approvalUrl: string;
}

export interface RedeemLicenseCodeResult {
  planTier: PlanTier;
}
