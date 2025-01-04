using System.Security.Claims;
using System.Security.Cryptography.Xml;
using System.Text;

using System.Web;
using System.Xml;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;


namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("customer")]
    [Authorize]
    public class CartController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly ECPayPaymentClient _ecpaySettings;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork,IEmailSender emailSender, IOptions<ECPayPaymentClient> ecpaySettings)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _ecpaySettings = ecpaySettings.Value;
        }

        
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            HttpContext.Session.SetString("UserId", userId);

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;


            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST() //傳遞的資料由[BindProperty]
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product");
                
            ShoppingCartVM.OrderHeader.OrderDate = System.DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

            //使用此條會造成ADD時，違反PK值重複
            //ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            //調用應用程序用戶應該:
            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            //此段靠 [BindProperty] 映射
            //ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            //ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            //ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            //ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            //ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            //ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;


            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            //檢查使用者是否屬於公司，來決定訂單處理流程
            if(applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                // it is a regular customer 
                //付款狀態待處理
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                //狀態待定
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            }
            else
            {
                //it is a company user
                //付款狀態延遲付款
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                //狀態已批准
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }

            ShoppingCartVM.OrderHeader.SessionId = "";

            //EF 會創建一個實體物件
             _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count
                };

                 _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            //if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            //{
            //    // it is a regular customer account and we need to capture payment
            //    //stripe logic
            //    var domain = "https://localhost:7001/";
            //    var options = new SessionCreateOptions
            //    {
            //        SuccessUrl = domain + $"customer/cart/OrderConfrimation?id={ShoppingCartVM.OrderHeader.Id}",
            //        CancelUrl = domain + "customer/cart/index",
            //        LineItems = new List<SessionLineItemOptions>(),
            //        Mode = "payment"
            //    };

            //    foreach(var item in ShoppingCartVM.ShoppingCartList)
            //    {
            //        var sessionLineItem = new SessionLineItemOptions
            //        {
            //            PriceData = new SessionLineItemPriceDataOptions
            //            {
            //                UnitAmount = (long)(item.Price * 100),
            //                Currency = "usd",
            //                ProductData = new SessionLineItemPriceDataProductDataOptions
            //                {
            //                    Name = item.Product.Title
            //                }
            //            },
            //            Quantity = item.Count
            //        };
            //        //如果購物車有幾樣添加的項目就會有幾樣
            //        options.LineItems.Add(sessionLineItem);
            //    }


            //    var service = new SessionService();
            //    Session session = service.Create(options);

            //    _unitOfWork.OrderHeader
            //        .UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id,session.Id,session.PaymentIntentId);
            //    _unitOfWork.Save();

            //    Response.Headers.Add("Location", session.Url);
            //    return new StatusCodeResult(303);
            //}

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                var itemNames = new List<string>();
                double totalPrice = 0;
                foreach (var item in ShoppingCartVM.ShoppingCartList)
                {
                    //itemNames.Add($"{item.Product.Title.Replace(" ", "%20")}x{item.Count}"); // 商品名稱與數量
                    itemNames.Add($"{item.Product.Title}x{item.Count}"); // 商品名稱與數量
                    totalPrice += item.Price * item.Count; // 計算總價
                }

                var domain = "https://dff8-27-147-8-70.ngrok-free.app/";

                HttpContext.Session.SetString("id", ShoppingCartVM.OrderHeader.Id.ToString());

                // 初始化支付請求的參數
                var paymentRequest = new Dictionary<string, string>
                {
                    { "MerchantID", _ecpaySettings.MerchantID },
                    { "MerchantTradeNo", ("Order" + DateTime.Now.Ticks).Substring(0, 20) }, // 訂單編號
                    { "MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") },
                    { "TotalAmount", totalPrice.ToString("F0") }, // 訂單總金額
                    { "TradeDesc", "訂單描述" },
                    { "ItemName", string.Join("#", itemNames) }, // 商品名稱
                    { "PaymentType","aio" },
                    { "EncryptType","1" },
                    { "ChoosePayment","ALL"},
                    { "ReturnURL",  domain +   $"customer/cart/HandleECPayReturn"}, // 付款完成後的回調 URL
                    { "ClientBackURL", domain + $"customer/cart/index"},
                    { "OrderResultURL", domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}" } // 付款結果的 URL
                };

                // 計算並加入 Hash
                string hashString = BuildHashString(paymentRequest);
                paymentRequest.Add("CheckMacValue", hashString);

                string paymentFormHtml = $@"
                    <form id='paymentForm' action='{_ecpaySettings.ServiceURL}' method='POST'>
                        <input type='hidden' name='MerchantID' value='{paymentRequest["MerchantID"]}' />
                        <input type='hidden' name='MerchantTradeNo' value='{paymentRequest["MerchantTradeNo"]}' />
                        <input type='hidden' name='MerchantTradeDate' value='{paymentRequest["MerchantTradeDate"]}' />
                        <input type='hidden' name='TotalAmount' value='{paymentRequest["TotalAmount"]}' />
                        <input type='hidden' name='TradeDesc' value='{paymentRequest["TradeDesc"]}' />
                        <input type='hidden' name='ItemName' value='{paymentRequest["ItemName"]}' />
                        <input type='hidden' name='PaymentType' value='aio' />
                        <input type='hidden' name='EncryptType' value='1' />
                        <input type='hidden' name='ChoosePayment' value='ALL'/>
                        <input type='hidden' name='ReturnURL' value='{paymentRequest["ReturnURL"]}' />
                        <input type='hidden' name='ClientBackURL' value='{paymentRequest["ClientBackURL"]}'>
                        <input type='hidden' name='CheckMacValue' value='{paymentRequest["CheckMacValue"]}' />
<input type='hidden' name='OrderResultURL' value='{paymentRequest["OrderResultURL"]}' />
                    </form>
                    <script>
                        document.getElementById('paymentForm').submit();
                    </script>";

                //

                _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, paymentRequest["MerchantTradeNo"], "");
                _unitOfWork.Save();
                // 返回生成的 HTML
                return Content(paymentFormHtml, "text/html");

                
            };


            //return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
            return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
        
        }

        // 計算並生成 Hash 字串的方法（基於 ECPay 的 HashKey 和 HashIV）
        private string BuildHashString(Dictionary<string, string> parameters)
        {
            // 計算檢查碼（CheckMacValue）所需要的 Hash 字串
            var stringBuilder = new StringBuilder();

            // 只加入有值的參數並且按字母順序排序
            foreach (var param in parameters.OrderBy(p => p.Key))
            {
                if (!string.IsNullOrEmpty(param.Value))
                {
                    stringBuilder.Append(param.Key + "=" + param.Value + "&");
                }
            }

            // 移除最後一個 '&'
            string hashString = stringBuilder.ToString().TrimEnd('&');

            // 加入 HashKey 和 HashIV 進行加密
            hashString = "HashKey=" + _ecpaySettings.HashKey + "&" + hashString + "&HashIV=" + _ecpaySettings.HashIV;

            // 步驟 1: URL encode 字串
            string encodedString = HttpUtility.UrlEncode(hashString, Encoding.UTF8);

            // 步驟 2: 轉為小寫
            string lowerCaseString = encodedString.ToLower();

            // 使用 SHA256 算法進行加密
            using (var sha256 = System.Security.Cryptography.SHA256.Create()) // 更通用的寫法
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(lowerCaseString));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
            }
        }


        // 提取支付頁面的 URL（假設返回的格式是 HTML 或 JSON）
        private string ExtractPaymentUrl(string responseContent)
        {
            // 根據實際的 ECPay 回應格式解析支付頁面的 URL
            // 假設 ECPay 會返回一個 JSON 字串，其中包含支付頁面的 URL（可以根據實際返回格式進行調整）
            var responseJson = JsonConvert.DeserializeObject<dynamic>(responseContent);
            return responseJson?.PaymentUrl; // 假設返回的欄位為 PaymentUrl
        }

        [HttpPost]
        [AllowAnonymous]
        [ActionName("HandleECPayReturn")]
        public IActionResult HandleECPayReturn([FromForm] PaymentResultRequest result)
        {
            // 檢查檢查碼
            if (result == null)
            {
                return Content("0|CheckMacValue error");
            }

            //// 處理訂單邏輯
            //UpdateOrderStatus(result);

            // 回應綠界
            return Content("1|OK");
        }


        [HttpPost]
        [AllowAnonymous]
        public IActionResult OrderResult([FromForm] PaymentResultRequest result)
        {

            var OrderHeaderId = HttpContext.Session.GetString("id");
            var UserId = HttpContext.Session.GetString("UserId");

            // 檢查用戶是否已登錄
            if (User.Identity.IsAuthenticated)
            {
                // 檢查 Session 中的 UserId 是否與當前用戶一致
                if (UserId == User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
                {
                    // 如果用戶已登錄並且 UserId 匹配，則繼續處理
                    return RedirectToAction(nameof(OrderConfirmation), new { id = OrderHeaderId });
                }
                else
                {
                    // 如果 Session 中的 UserId 與當前用戶的 UserId 不匹配，則可能是異常情況
                    return RedirectToAction("Login", "Account"); // 重定向到登入頁面
                }
            }
            else
            {
                // 如果用戶未登錄，則重定向到登入頁面
                return RedirectToAction("Login", "Account");
            }


            //return RedirectToAction(nameof(OrderConfirmation), new { id = OrderHeaderId });
        }


        [AllowAnonymous]
        public IActionResult OrderConfirmation(int id)
        {

            var userId = HttpContext.Session.GetString("UserId");

            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");

            //if(orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            //{
            //    var service = new SessionService();
            //    Session session = service.Get(orderHeader.SessionId);

            //    if(session.PaymentStatus.ToLower() == "paid")
            //    {
            //        _unitOfWork.OrderHeader.UpdateStripePaymentID(id,session.Id,session.PaymentIntentId);
            //        _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
            //        _unitOfWork.Save();
            //    }

            //}
            HttpContext.Session.Clear();

            _emailSender.SendEmailAsync(orderHeader.ApplicationUser.Email, "New Order Confirmation",
                $"<p> New Order Created - {orderHeader.Id}</p>");

            List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
                .GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();

            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();

            return View(id);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId,tracked:true);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
            if (cartFromDb.Count <= 1)
            {
                HttpContext.Session
                .SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                //remove that from cart
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            HttpContext.Session
                .SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}
