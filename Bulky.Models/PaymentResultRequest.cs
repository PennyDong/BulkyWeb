using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.Models
{
    public class PaymentResultRequest
    {
        public string MerchantID { get; set; }
        public string MerchantTradeNo { get; set; }
        public string TradeNo { get; set; }
        public int RtnCode { get; set; }
        public string RtnMsg { get; set; }
        public string TradeAmt { get; set; }
        public string PaymentDate { get; set; }
        public string PaymentType { get; set; }
        public string CheckMacValue { get; set; }
    }
}
