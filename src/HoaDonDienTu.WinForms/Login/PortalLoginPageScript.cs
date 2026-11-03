namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Đoạn JavaScript tĩnh chạy trong trang cổng Thuế để người dùng thấy ngay form đăng nhập: đóng cửa sổ thông báo hiện ra lần đầu,
    /// bấm mục "Đăng nhập" của trang chủ để mở form, phủ trắng phần trang chủ phía sau và đặt con trỏ vào ô tên đăng nhập.
    /// Form đăng nhập là hộp thoại của chính trang chủ (không có địa chỉ riêng) nên trang vẫn phải tải; đoạn này chỉ che phần dư.
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
  var loginText = 'Đăng nhập';
  var attempts = 0;
  var clickedAt = 0;
  var timer = setInterval(function () {
    attempts++;
    var user = document.getElementById('username');
    if (user) { clearInterval(timer); user.focus(); return; }
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
