using System;
using System.Collections.Generic;
using System.Text;

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

        // ВИПРАВЛЕНО: reasonCode - це число, а не рядок
        public int? reasonCode { get; set; }

        public string? reason { get; set; }
        public string? email { get; set; }
        public string? phone { get; set; }
        public long createdDate { get; set; }
        public long processingDate { get; set; }

        // Додаткові поля, які може повернути WayForPay
        public string? cardPan { get; set; }
        public string? cardType { get; set; }
        public string? issuerBankCountry { get; set; }
        public string? issuerBankName { get; set; }
        public decimal? fee { get; set; }
        public string? paymentSystem { get; set; }
    }
}
