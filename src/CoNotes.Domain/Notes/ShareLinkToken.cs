namespace CoNotes.Domain.Notes;

/// <summary>
/// 分享連結的憑證值。刻意用不透明的字串包裝, 不假設底層編碼方式(GUID、亂數 byte 的
/// base64url 等)——「怎麼產生一個不可猜測的值」是 Infrastructure/Application 層的技術決策,
/// Domain 只在乎「這是一個非空字串」。
/// </summary>
public sealed record ShareLinkToken(string Value)
{
    public string Value { get; } = string.IsNullOrEmpty(Value)
        ? throw new ArgumentException("ShareLinkToken 的值不能是 null 或空字串!", nameof(Value))
        : Value;
}
