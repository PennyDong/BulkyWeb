using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.Utility
{
    public class ECPayPaymentClient
    {
        public string MerchantID { get; set; }
        public string HashKey { get; set; }
        public string HashIV { get; set; }
        public string ServiceURL { get; set; }
    }
}
