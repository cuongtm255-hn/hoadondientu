namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Đoạn JavaScript tĩnh chạy trong trang cổng Thuế để người dùng thấy ngay form đăng nhập: đóng cửa sổ thông báo hiện ra lần đầu,
    /// bấm mục "Đăng nhập" của trang chủ để mở form, phủ trắng phần trang chủ phía sau và đặt con trỏ vào ô tên đăng nhập.
    /// Form đăng nhập là hộp thoại của chính trang chủ (không có địa chỉ riêng) nên trang vẫn phải tải; đoạn này chỉ che phần dư.
    /// Khi hộp thoại đăng nhập bị đóng, đoạn này đặt <c>window.__hddtLoginClosed = true</c> để ứng dụng biết và đóng form đăng nhập theo.
    /// Chỉ thao tác giao diện: không đọc giá trị ô nhập, không gửi request nào, không đụng tới mật khẩu hay CAPTCHA.
    /// </summary>
    public static class PortalLoginPageScript
    {
        public const string Source = @"
(function () {
  if (window.__hddtLoginAssist) { return; }
  window.__hddtLoginAssist = true;
  var style = document.createElement('style');
  style.textContent = '.ant-modal-mask{background-color:#fff!important;opacity:1!important}';
  document.head.appendChild(style);
  // Người dùng bấm X đóng hộp thoại đăng nhập: đặt cờ để ứng dụng hỏi qua EvaluateScript (không dùng console hay cầu nối, không đọc ô nhập).
  function watchDialog() {
    var seen = false;
    var watcher = setInterval(function () {
      var field = document.getElementById('username');
      var visible = !!field && field.offsetParent !== null;
      if (visible) { seen = true; return; }
      if (seen) { window.__hddtLoginClosed = true; clearInterval(watcher); }
    }, 300);
  }
  var loginText = 'Đăng nhập';
  var attempts = 0;
  var clickedAt = 0;
  var timer = setInterval(function () {
    attempts++;
    var user = document.getElementById('username');
    if (user) { clearInterval(timer); user.focus(); watchDialog(); return; }
    var wraps = document.querySelectorAll('.ant-modal-wrap');
    var closed = false;
    for (var i = 0; i < wraps.length; i++) {
      var wrap = wraps[i];
      if (window.getComputedStyle(wrap).display === 'none') { continue; }
      var close = wrap.querySelector('.ant-modal-close');
      if (close) { close.click(); closed = true; }
    }
    if (closed) { return; }
    var now = Date.now();
    if (now - clickedAt < 2500) { return; }
    var items = document.querySelectorAll('.home-header-menu-item span');
    for (var j = 0; j < items.length; j++) {
      if ((items[j].textContent || '').trim() === loginText) { items[j].click(); clickedAt = now; break; }
    }
    if (attempts > 60) { clearInterval(timer); }
  }, 400);
})();";
    }
}
