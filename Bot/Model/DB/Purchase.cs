public class PurchaseResult
{
    public PurchaseStatus Status { get; set; }
    public int RemainingPoints { get; set; }
}

public enum PurchaseStatus
{
    Success,
    UserNotFound,
    TitleNotFound,
    AlreadyOwned,
    InsufficientPoints,
    NotForSale
}