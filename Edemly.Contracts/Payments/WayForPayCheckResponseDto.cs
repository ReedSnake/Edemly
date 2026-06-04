namespace Edemly.Contracts.Payments
{
    public class WayForPayCheckResponseDto
    {
        public string? merchantAccount { get; set; }
        public string? orderReference { get; set; }
        public string? merchantSignature { get; set; }
        public string? amount { get; set; }
        public string? currency { get; set; }
        public string? transactionStatus { get; set; } // "Approved", "Pending", "Declined"

        public int? reasonCode { get; set; }

        public string? reason { get; set; }
        public string? email { get; set; }
        public string? phone { get; set; }
        public long createdDate { get; set; }
        public long processingDate { get; set; }

        public string? cardPan { get; set; }
        public string? cardType { get; set; }
        public string? issuerBankCountry { get; set; }
        public string? issuerBankName { get; set; }
        public decimal? fee { get; set; }
        public string? paymentSystem { get; set; }
    }
}