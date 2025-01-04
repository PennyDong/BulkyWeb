using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.Models
{
    public class PaymentRequest
    {
        public string MerchantID { get; set; }
        public string HashKey { get; set; }
        public string HashIV { get; set; }
        public string ServiceURL { get; set; }
        public string MerchantTradeNo { get; set; }
        public string MerchantTradeDate { get; set; }
        public string PaymentType { get; set; } = "aio";
        public string TotalAmount { get; set; }
        public string TradeDesc { get; set; }
        public string ItemName { get; set; }
        public string ReturnURL { get; set; }
        public string OrderResultURL { get; set; }
        public string ChoosePayment { get; set; } = "ALL";
    }
}
