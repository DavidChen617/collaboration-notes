namespace CoNotes.Domain.Billing;

public interface ILicenseCodeRepository
{
    Task AddAsync(LicenseCode licenseCode, CancellationToken ct);
    Task<LicenseCode?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// 前端在 PayPal 導回確認頁時, 用來輪詢「這筆訂單的 webhook 是否已經處理完、code 是否已經產生」——
    /// webhook 送達是非同步的, 使用者導回的當下不保證 code 已經存在。
    /// </summary>
    Task<LicenseCode?> GetByPayPalOrderIdAsync(string payPalOrderId, CancellationToken ct);

    /// <summary>
    /// 持久化一個已經在記憶體中呼叫過 <see cref="LicenseCode.Redeem"/> 的 Aggregate, 用條件式
    /// UPDATE(只在目前尚未被兌換時才更新)原子地落地, 回傳是否真的成功。這是唯一能保證「兩個
    /// 併發請求兌換同一組 code, 只有一個成功」的地方——單靠讀取 Aggregate 後在記憶體判斷,
    /// 在兩個請求都讀到「尚未兌換」的競態下無法防止雙重兌換。只有回傳 true 時, 這個
    /// Aggregate 已經產生的 Domain Event 才會被追蹤、之後隨交易提交一併發布。
    /// </summary>
    Task<bool> TryPersistRedemptionAsync(LicenseCode licenseCode, CancellationToken ct);
}
