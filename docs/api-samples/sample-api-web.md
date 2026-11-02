1. API login
https://hoadondientu.gdt.gov.vn/api/security-taxpayer/authenticate
curl --url 'https://hoadondientu.gdt.gov.vn/api/security-taxpayer/authenticate' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi-VN,vi;q=0.9,fr-FR;q=0.8,fr;q=0.7,en-US;q=0.6,en;q=0.5' \
  -H 'action;' \
  -H 'content-type: application/json' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /' \
  -H 'origin: https://hoadondientu.gdt.gov.vn' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/' \
  -H 'request-id: 6dd705c3-b218-4460-8a94-22590efb2717' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36' \
  --data-raw '{"username":"0109213133","password":"<PASSWORD>","cvalue":"<CAPTCHA>","ckey":"<CAPTCHA_KEY>"}'
  
  response:
  {
    "token": "<JWT>"
}


2. API tra cứu hóa đơn bán ra
https://hoadondientu.gdt.gov.vn/api/query/invoices/sold?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59
https://hoadondientu.gdt.gov.vn/api/query/invoices/sold?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;tthai==1
https://hoadondientu.gdt.gov.vn/api/query/invoices/sold?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nmmst==1112232234;tthai==6;ttxly==0;khmshdon==1;shdon==323;khhdon==121;unhiem==1;nmcmnd==036402421225

Các tham số:
2.1 Ngày lập hóa đơn (*): tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59
2.2 MST Người mua: nmmst==1112232234
2.3 CCCD Người mua: nmcmnd==036402421225
2.4 Trạng thái hóa đơn  (*): 
  tthai: trạng thái hóa đơn: giá trị được lấy từ dropdownlist sau
  <div id="5eb513f8-034e-4394-b4f6-aeba1571e111" style="overflow: auto; transform: translateZ(0px);"><ul role="listbox" class="ant-select-dropdown-menu  ant-select-dropdown-menu-root ant-select-dropdown-menu-vertical" tabindex="0"><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Tất cả</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item ant-select-dropdown-menu-item-selected" aria-selected="true" style="user-select: none;">Hóa đơn mới</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn thay thế</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn điều chỉnh</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị thay thế</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị điều chỉnh</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị hủy</li></ul></div>
  Tất cả: không cần truyền
  Hóa đơn mới: 1
  Hóa đơn thay thế: 2
  Hóa đơn điều chỉnh: 3
  Hóa đơn đã bị thay thế: 4
  Hóa đơn đã bị điều chỉnh: 5
  Hóa đơn đã bị hủy: 6
2.5 Kết quả kiểm tra  (*): ttxly==0
Giá trị tham số này lấy từ dropdownlist sau
<div id="3fc5c409-4c37-4034-dccd-4991a493baff" style="overflow: auto; transform: translateZ(0px);"><ul role="listbox" class="ant-select-dropdown-menu  ant-select-dropdown-menu-root ant-select-dropdown-menu-vertical" tabindex="0"><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Tất cả</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Cục Thuế đã nhận</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Đang tiến hành kiểm tra điều kiện cấp mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">CQT từ chối hóa đơn theo từng lần phát sinh</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đủ điều kiện cấp mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn không đủ điều kiện cấp mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item ant-select-dropdown-menu-item-selected" aria-selected="true" style="user-select: none;">Đã cấp mã hóa đơn</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Cục Thuế đã nhận không mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Đã kiểm tra định kỳ HĐĐT không có mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền</li></ul></div>
- Tất cả: Bỏ trống
- Cục Thuế đã nhận: ttxly==0
- Đang tiến hành kiểm tra điều kiện cấp mã: ttxly==1
- Cơ quan thuế từ chối hóa đơn theo lần phát sinh: ttxly==2
- Hóa đơn đủ điều kiện cấp mã: ttxly==3
- Hóa đơn không đủ điều kiện cấp mã: ttxly==4
- Đã cấp mã hóa đơn: ttxly==5
- Cục Thuế đã nhận không mã: ttxly==6
- Đã kiểm tra định kì HĐĐT không có mã: ttxly==7
- Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền: ttxly==8

2.6 Ký hiệu mẫu số hóa đơn: khmshdon==9 <Giá trị từ 1 -> 9>
2.7 Hóa đơn ủy nhiệm: unhiem==1 (Dạng checkbox, không check thì bỏ trống)
2.8: Số hóa đơn: hdon==323
2.9: Kí hiệu mẫu số hóa đơn: khhdon==121
  
  
curl --url 'https://hoadondientu.gdt.gov.vn/api/query/invoices/sold?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi' \
  -H 'action: T%C3%ACm%20ki%E1%BA%BFm%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)' \
  -H 'authorization: Bearer <JWT>' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /tra-cuu/tra-cuu-hoa-don' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don' \
  -H 'request-id: d45e9958-e1a3-43f5-8cf0-f50916b32127' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36'
  
  
  Response:
  {"datas":[{"nbmst":"0109213133","khmshdon":1,"khhdon":"C26TBN","shdon":40,"cqt":"0155","cttkhac":[],"dvtte":"VND","hdon":"01","hsgcma":"49a9028d-3325-4607-9bee-4f70725b02b8","hsgoc":"22b5e2de-8d98-464a-a45c-2bc6d36a3ca6","hthdon":1,"htttoan":9,"id":"d2240f37-51af-48d9-98f1-f01d4a0810ff","idtbao":null,"khdon":null,"khhdgoc":null,"khmshdgoc":null,"lhdgoc":null,"mhdon":"00D088D84B800445148C1670AD2129B93E","mtdiep":null,"mtdtchieu":"V0100684378DDBBBBFBFD744AB7B6AF13C7DD082F1B","nbdchi":"Thôn Long Châu San, Phường Chương Mỹ, Thành phố Hà Nội","chma":null,"chten":null,"nbhdktngay":null,"nbhdktso":null,"nbhdso":null,"nblddnbo":null,"nbptvchuyen":null,"nbstkhoan":null,"nbten":"CÔNG TY TNHH BAY NGAY","nbtnhang":null,"nbtnvchuyen":null,"nbttkhac":[],"ncma":"2026-10-31T08:04:40.728Z","ncnhat":"2026-10-31T08:04:40.737Z","ngcnhat":"tvan_buuchinhvt","nky":"2026-10-31T08:04:36Z","nmdchi":"Số 19, ngõ 37, phố Đông Các, Phường Đống Đa, Thành phố Hà Nội, Việt Nam","nmmst":"0101654633","nmstkhoan":null,"nmten":"CÔNG TY CỔ PHẦN CÔNG NGHỆ SINH HỌC THÚ Y","nmtnhang":null,"nmtnmua":null,"nmttkhac":[],"ntao":"2026-10-31T08:04:40.689Z","ntnhan":"2026-10-31T08:04:40.644Z","pban":"2.1.0","ptgui":1,"shdgoc":null,"tchat":1,"tdlap":"2026-10-30T17:00:00Z","tgia":1.0,"tgtcthue":2873316.0,"tgtthue":229865.0,"tgtttbchu":"Ba  triệu, một  trăm  linh  ba  nghìn, một  trăm  tám  mươi mốt  đồng","tgtttbso":3103181.0,"thdon":"Hóa đơn GTGT","thlap":202610,"thttlphi":[],"thttltsuat":[{"tsuat":"8%","thtien":2873316.0,"tthue":229865.28,"gttsuat":null}],"tlhdon":"Hóa đơn GTGT","ttcktmai":null,"tthai":1,"ttkhac":[],"tttbao":1,"ttttkhac":[],"ttxly":5,"tvandnkntt":"0100684378","mhso":null,"ladhddt":1,"mkhang":null,"nbsdthoai":"0813088088","nbdctdtu":"Hd.bnvlt@gmail.com","nbfax":null,"nbwebsite":null,"nbcks":"{\"Subject\":\"OID.0.9.2342.19200300.100.1.1=MST:0109213133, CN=CÔNG TY TNHH BAY NGAY, OU=CÔNG TY TNHH BAY NGAY, O=CÔNG TY TNHH BAY NGAY, S=Hà Nội, C=VN\",\"SerialNumber\":\"540118357B85C9CAD8A19C5FDE79BA00\",\"Issuer\":\"C=VN, O=MAT BAO CORPORATION, CN=MATBAO-CA\",\"NotAfter\":\"2027-05-02T10:07:25\",\"NotBefore\":\"2024-05-02T10:07:25\",\"SigningTime\":\"2026-10-31T15:04:36\"}","nmsdthoai":null,"nmdctdtu":null,"nmcmnd":null,"nmcks":null,"bhphap":0,"hddunlap":null,"gchdgoc":null,"tbhgtngay":null,"bhpldo":null,"bhpcbo":null,"bhpngay":null,"tdlhdgoc":null,"tgtphi":null,"unhiem":null,"mstdvnunlhdon":null,"tdvnunlhdon":null,"nbmdvqhnsach":null,"nbsqdinh":null,"nbncqdinh":null,"nbcqcqdinh":null,"nbhtban":null,"nmmdvqhnsach":null,"nmddvchden":null,"nmtgvchdtu":null,"nmtgvchdden":null,"nbtnban":null,"dcdvnunlhdon":null,"dksbke":null,"dknlbke":null,"thtttoan":"TM, CK","msttcgp":"0100684378","cqtcks":"{\"Subject\":\"CN=CỤC THUẾ,O=BỘ TÀI CHÍNH,L=Hà Nội,C=VN\",\"SerialNumber\":\"2A05F812673607B0\",\"Issuer\":\"CN=CA phục vụ các cơ quan Nhà nước G2, O=Ban Cơ yếu Chính phủ, C=VN\",\"NotAfter\":\"2030-03-12T16:36:53\",\"NotBefore\":\"2025-03-13T16:36:53\",\"SigningTime\":\"2026-10-31T15:04:40\"}","gchu":"","kqcht":null,"hdntgia":null,"tgtkcthue":null,"tgtkhac":null,"nmshchieu":null,"nmnchchieu":null,"nmnhhhchieu":null,"nmqtich":null,"ktkhthue":null,"nmstttoan":null,"nmttttoan":null,"mddkdoanh":null,"tddkdoanh":null,"dcddkdoanh":null,"nmmtinh":null,"nmttinh":null,"nmmxa":null,"nmtxa":null,"nbmstgoc":null,"lddctthe":null,"hdhhdvu":null,"qrcode":null,"ttmstten":null,"ladhddtten":null,"hdxkhau":null,"hdxkptquan":null,"hdgktkhthue":null,"hdonLquans":null,"tthdclquan":false,"pdndungs":null,"hdtbssrses":null,"hdTrung":null,"isHDTrung":null,"hdcttchinh":0,"dlhquan":null,"dlnhtmai":null,"bltphi":null,"tcsvhdChinh":null},{"nbmst":"0109213133","khmshdon":1,"khhdon":"C26TBN","shdon":38,"cqt":"0155","cttkhac":[],"dvtte":"VND","hdon":"01","hsgcma":"37c1d70d-cd05-4c3b-bee7-b2906faeea39","hsgoc":"eb782a6a-e0b5-4134-a501-93947fa3051c","hthdon":1,"htttoan":9,"id":"53b684dc-cdb4-4d4c-97bc-941da11108fc","idtbao":null,"khdon":null,"khhdgoc":null,"khmshdgoc":null,"lhdgoc":null,"mhdon":"0090F3E7D3EAC9473BA61E231EB1F5A54E","mtdiep":null,"mtdtchieu":"V010068437866EC78943A644D51B01F92AA8CE20093","nbdchi":"Thôn Long Châu San, Phường Chương Mỹ, Thành phố Hà Nội","chma":null,"chten":null,"nbhdktngay":null,"nbhdktso":null,"nbhdso":null,"nblddnbo":null,"nbptvchuyen":null,"nbstkhoan":null,"nbten":"CÔNG TY TNHH BAY NGAY","nbtnhang":null,"nbtnvchuyen":null,"nbttkhac":[],"ncma":"2026-10-30T03:27:36.705Z","ncnhat":"2026-10-30T03:27:36.712Z","ngcnhat":"tvan_buuchinhvt","nky":"2026-10-30T03:27:15Z","nmdchi":"Số 19, ngõ 37, phố Đông Các, Phường Đống Đa, Thành phố Hà Nội, Việt Nam","nmmst":"0101654633","nmstkhoan":null,"nmten":"CÔNG TY CỔ PHẦN CÔNG NGHỆ SINH HỌC THÚ Y","nmtnhang":null,"nmtnmua":null,"nmttkhac":[],"ntao":"2026-10-30T03:27:36.667Z","ntnhan":"2026-10-30T03:27:36.620Z","pban":"2.1.0","ptgui":1,"shdgoc":null,"tchat":1,"tdlap":"2026-10-29T17:00:00Z","tgia":1.0,"tgtcthue":2672019.0,"tgtthue":213762.0,"tgtttbchu":"Hai  triệu, tám  trăm  tám  mươi lăm  nghìn, bảy  trăm  tám  mươi mốt  đồng","tgtttbso":2885781.0,"thdon":"Hóa đơn GTGT","thlap":202610,"thttlphi":[],"thttltsuat":[{"tsuat":"8%","thtien":2672019.0,"tthue":213761.52,"gttsuat":null}],"tlhdon":"Hóa đơn GTGT","ttcktmai":null,"tthai":1,"ttkhac":[],"tttbao":1,"ttttkhac":[],"ttxly":5,"tvandnkntt":"0100684378","mhso":null,"ladhddt":1,"mkhang":null,"nbsdthoai":"0813088088","nbdctdtu":"Hd.bnvlt@gmail.com","nbfax":null,"nbwebsite":null,"nbcks":"{\"Subject\":\"OID.0.9.2342.19200300.100.1.1=MST:0109213133, CN=CÔNG TY TNHH BAY NGAY, OU=CÔNG TY TNHH BAY NGAY, O=CÔNG TY TNHH BAY NGAY, S=Hà Nội, C=VN\",\"SerialNumber\":\"540118357B85C9CAD8A19C5FDE79BA00\",\"Issuer\":\"C=VN, O=MAT BAO CORPORATION, CN=MATBAO-CA\",\"NotAfter\":\"2027-05-02T10:07:25\",\"NotBefore\":\"2024-05-02T10:07:25\",\"SigningTime\":\"2026-10-30T10:27:15\"}","nmsdthoai":null,"nmdctdtu":null,"nmcmnd":null,"nmcks":null,"bhphap":0,"hddunlap":null,"gchdgoc":null,"tbhgtngay":null,"bhpldo":null,"bhpcbo":null,"bhpngay":null,"tdlhdgoc":null,"tgtphi":null,"unhiem":null,"mstdvnunlhdon":null,"tdvnunlhdon":null,"nbmdvqhnsach":null,"nbsqdinh":null,"nbncqdinh":null,"nbcqcqdinh":null,"nbhtban":null,"nmmdvqhnsach":null,"nmddvchden":null,"nmtgvchdtu":null,"nmtgvchdden":null,"nbtnban":null,"dcdvnunlhdon":null,"dksbke":null,"dknlbke":null,"thtttoan":"TM, CK","msttcgp":"0100684378","cqtcks":"{\"Subject\":\"CN=CỤC THUẾ,O=BỘ TÀI CHÍNH,L=Hà Nội,C=VN\",\"SerialNumber\":\"2A05F812673607B0\",\"Issuer\":\"CN=CA phục vụ các cơ quan Nhà nước G2, O=Ban Cơ yếu Chính phủ, C=VN\",\"NotAfter\":\"2030-03-12T16:36:53\",\"NotBefore\":\"2025-03-13T16:36:53\",\"SigningTime\":\"2026-10-30T10:27:36\"}","gchu":"","kqcht":null,"hdntgia":null,"tgtkcthue":null,"tgtkhac":null,"nmshchieu":null,"nmnchchieu":null,"nmnhhhchieu":null,"nmqtich":null,"ktkhthue":null,"nmstttoan":null,"nmttttoan":null,"mddkdoanh":null,"tddkdoanh":null,"dcddkdoanh":null,"nmmtinh":null,"nmttinh":null,"nmmxa":null,"nmtxa":null,"nbmstgoc":null,"lddctthe":null,"hdhhdvu":null,"qrcode":null,"ttmstten":null,"ladhddtten":null,"hdxkhau":null,"hdxkptquan":null,"hdgktkhthue":null,"hdonLquans":null,"tthdclquan":false,"pdndungs":null,"hdtbssrses":null,"hdTrung":null,"isHDTrung":null,"hdcttchinh":0,"dlhquan":null,"dlnhtmai":null,"bltphi":null,"tcsvhdChinh":null},{"nbmst":"0109213133","khmshdon":1,"khhdon":"C26TBN","shdon":39,"cqt":"0155","cttkhac":[],"dvtte":"VND","hdon":"01","hsgcma":"eb569634-c101-48d6-a88f-3c07592ad7a3","hsgoc":"8c9ef49d-1362-436f-a26c-e2c47c750c53","hthdon":1,"htttoan":9,"id":"ec72e1a9-89cc-48ba-a6a1-f4bacc0ac040","idtbao":null,"khdon":null,"khhdgoc":null,"khmshdgoc":null,"lhdgoc":null,"mhdon":"00F567B47B3E354FC9A47D522398E46007","mtdiep":null,"mtdtchieu":"V0100684378DEA1AA41287C4EB29D13235384C319AE","nbdchi":"Thôn Long Châu San, Phường Chương Mỹ, Thành phố Hà Nội","chma":null,"chten":null,"nbhdktngay":null,"nbhdktso":null,"nbhdso":null,"nblddnbo":null,"nbptvchuyen":null,"nbstkhoan":null,"nbten":"CÔNG TY TNHH BAY NGAY","nbtnhang":null,"nbtnvchuyen":null,"nbttkhac":[],"ncma":"2026-10-30T04:13:03.609Z","ncnhat":"2026-10-30T04:13:03.618Z","ngcnhat":"tvan_buuchinhvt","nky":"2026-10-30T04:13:00Z","nmdchi":"58 Thoại Ngọc Hầu, Phường Tân Phú, Thành Phố Hồ Chí Minh","nmmst":"0303614739","nmstkhoan":null,"nmten":"CÔNG TY CP MAY VIỆT THỊNH","nmtnhang":null,"nmtnmua":null,"nmttkhac":[],"ntao":"2026-10-30T04:13:03.572Z","ntnhan":"2026-10-30T04:13:03.520Z","pban":"2.1.0","ptgui":1,"shdgoc":null,"tchat":1,"tdlap":"2026-10-29T17:00:00Z","tgia":1.0,"tgtcthue":1.1451006E7,"tgtthue":916080.0,"tgtttbchu":"Mười  hai  triệu, ba  trăm  sáu  mươi bảy  nghìn, không  trăm  tám  mươi sáu  đồng","tgtttbso":1.2367086E7,"thdon":"Hóa đơn GTGT","thlap":202610,"thttlphi":[],"thttltsuat":[{"tsuat":"8%","thtien":1.1451006E7,"tthue":916080.48,"gttsuat":null}],"tlhdon":"Hóa đơn GTGT","ttcktmai":null,"tthai":1,"ttkhac":[],"tttbao":1,"ttttkhac":[],"ttxly":5,"tvandnkntt":"0100684378","mhso":null,"ladhddt":1,"mkhang":null,"nbsdthoai":"0813088088","nbdctdtu":"Hd.bnvlt@gmail.com","nbfax":null,"nbwebsite":null,"nbcks":"{\"Subject\":\"OID.0.9.2342.19200300.100.1.1=MST:0109213133, CN=CÔNG TY TNHH BAY NGAY, OU=CÔNG TY TNHH BAY NGAY, O=CÔNG TY TNHH BAY NGAY, S=Hà Nội, C=VN\",\"SerialNumber\":\"540118357B85C9CAD8A19C5FDE79BA00\",\"Issuer\":\"C=VN, O=MAT BAO CORPORATION, CN=MATBAO-CA\",\"NotAfter\":\"2027-05-02T10:07:25\",\"NotBefore\":\"2024-05-02T10:07:25\",\"SigningTime\":\"2026-10-30T11:13:00\"}","nmsdthoai":null,"nmdctdtu":null,"nmcmnd":null,"nmcks":null,"bhphap":0,"hddunlap":null,"gchdgoc":null,"tbhgtngay":null,"bhpldo":null,"bhpcbo":null,"bhpngay":null,"tdlhdgoc":null,"tgtphi":null,"unhiem":null,"mstdvnunlhdon":null,"tdvnunlhdon":null,"nbmdvqhnsach":null,"nbsqdinh":null,"nbncqdinh":null,"nbcqcqdinh":null,"nbhtban":null,"nmmdvqhnsach":null,"nmddvchden":null,"nmtgvchdtu":null,"nmtgvchdden":null,"nbtnban":null,"dcdvnunlhdon":null,"dksbke":null,"dknlbke":null,"thtttoan":"CK","msttcgp":"0100684378","cqtcks":"{\"Subject\":\"CN=CỤC THUẾ,O=BỘ TÀI CHÍNH,L=Hà Nội,C=VN\",\"SerialNumber\":\"2A05F812673607B0\",\"Issuer\":\"CN=CA phục vụ các cơ quan Nhà nước G2, O=Ban Cơ yếu Chính phủ, C=VN\",\"NotAfter\":\"2030-03-12T16:36:53\",\"NotBefore\":\"2025-03-13T16:36:53\",\"SigningTime\":\"2026-10-30T11:13:03\"}","gchu":"","kqcht":null,"hdntgia":null,"tgtkcthue":null,"tgtkhac":null,"nmshchieu":null,"nmnchchieu":null,"nmnhhhchieu":null,"nmqtich":null,"ktkhthue":null,"nmstttoan":null,"nmttttoan":null,"mddkdoanh":null,"tddkdoanh":null,"dcddkdoanh":null,"nmmtinh":null,"nmttinh":null,"nmmxa":null,"nmtxa":null,"nbmstgoc":null,"lddctthe":null,"hdhhdvu":null,"qrcode":null,"ttmstten":null,"ladhddtten":null,"hdxkhau":null,"hdxkptquan":null,"hdgktkhthue":null,"hdonLquans":null,"tthdclquan":false,"pdndungs":null,"hdtbssrses":null,"hdTrung":null,"isHDTrung":null,"hdcttchinh":0,"dlhquan":null,"dlnhtmai":null,"bltphi":null,"tcsvhdChinh":null}],"total":3,"state":null,"time":133}
  
  
  3. API xem chi tiết:
  https://hoadondientu.gdt.gov.vn/api/query/invoices/detail?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1
  curl --url 'https://hoadondientu.gdt.gov.vn/api/query/invoices/detail?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi' \
  -H 'action: Xem%20h%C3%B3a%20%C4%91%C6%A1n%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)' \
  -H 'authorization: Bearer <JWT>' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /tra-cuu/tra-cuu-hoa-don' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don' \
  -H 'request-id: 9d3a46fc-3473-441a-b73d-8fcdb1efa14b' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36'
  
  Response:
  {"nbmst":"0109213133","khmshdon":1,"khhdon":"C26TBN","shdon":40,"cqt":"0155","cttkhac":[],"dvtte":"VND","hdon":"01","hsgcma":"49a9028d-3325-4607-9bee-4f70725b02b8","hsgoc":"22b5e2de-8d98-464a-a45c-2bc6d36a3ca6","hthdon":1,"htttoan":9,"id":"d2240f37-51af-48d9-98f1-f01d4a0810ff","idtbao":null,"khdon":null,"khhdgoc":null,"khmshdgoc":null,"lhdgoc":null,"mhdon":"00D088D84B800445148C1670AD2129B93E","mtdiep":null,"mtdtchieu":"V0100684378DDBBBBFBFD744AB7B6AF13C7DD082F1B","nbdchi":"Thôn Long Châu San, Phường Chương Mỹ, Thành phố Hà Nội","chma":null,"chten":null,"nbhdktngay":null,"nbhdktso":null,"nbhdso":null,"nblddnbo":null,"nbptvchuyen":null,"nbstkhoan":null,"nbten":"CÔNG TY TNHH BAY NGAY","nbtnhang":null,"nbtnvchuyen":null,"nbttkhac":[],"ncma":"2026-10-31T08:04:40.728Z","ncnhat":"2026-10-31T08:04:40.737Z","ngcnhat":"tvan_buuchinhvt","nky":"2026-10-31T08:04:36Z","nmdchi":"Số 19, ngõ 37, phố Đông Các, Phường Đống Đa, Thành phố Hà Nội, Việt Nam","nmmst":"0101654633","nmstkhoan":null,"nmten":"CÔNG TY CỔ PHẦN CÔNG NGHỆ SINH HỌC THÚ Y","nmtnhang":null,"nmtnmua":null,"nmttkhac":[],"ntao":"2026-10-31T08:04:40.689Z","ntnhan":"2026-10-31T08:04:40.644Z","pban":"2.1.0","ptgui":1,"shdgoc":null,"tchat":1,"tdlap":"2026-10-30T17:00:00Z","tgia":1.0,"tgtcthue":2873316.0,"tgtthue":229865.0,"tgtttbchu":"Ba  triệu, một  trăm  linh  ba  nghìn, một  trăm  tám  mươi mốt  đồng","tgtttbso":3103181.0,"thdon":"Hóa đơn GTGT","thlap":202610,"thttlphi":[],"thttltsuat":[{"tsuat":"8%","thtien":2873316.0,"tthue":229865.28,"gttsuat":null}],"tlhdon":"Hóa đơn GTGT","ttcktmai":null,"tthai":1,"ttkhac":[],"tttbao":1,"ttttkhac":[],"ttxly":5,"tvandnkntt":"0100684378","mhso":null,"ladhddt":1,"mkhang":null,"nbsdthoai":"0813088088","nbdctdtu":"Hd.bnvlt@gmail.com","nbfax":null,"nbwebsite":null,"nbcks":"{\"Subject\":\"OID.0.9.2342.19200300.100.1.1=MST:0109213133, CN=CÔNG TY TNHH BAY NGAY, OU=CÔNG TY TNHH BAY NGAY, O=CÔNG TY TNHH BAY NGAY, S=Hà Nội, C=VN\",\"SerialNumber\":\"540118357B85C9CAD8A19C5FDE79BA00\",\"Issuer\":\"C=VN, O=MAT BAO CORPORATION, CN=MATBAO-CA\",\"NotAfter\":\"2027-05-02T10:07:25\",\"NotBefore\":\"2024-05-02T10:07:25\",\"SigningTime\":\"2026-10-31T15:04:36\"}","nmsdthoai":null,"nmdctdtu":null,"nmcmnd":null,"nmcks":null,"bhphap":0,"hddunlap":null,"gchdgoc":null,"tbhgtngay":null,"bhpldo":null,"bhpcbo":null,"bhpngay":null,"tdlhdgoc":null,"tgtphi":null,"unhiem":null,"mstdvnunlhdon":null,"tdvnunlhdon":null,"nbmdvqhnsach":null,"nbsqdinh":null,"nbncqdinh":null,"nbcqcqdinh":null,"nbhtban":null,"nmmdvqhnsach":null,"nmddvchden":null,"nmtgvchdtu":null,"nmtgvchdden":null,"nbtnban":null,"dcdvnunlhdon":null,"dksbke":null,"dknlbke":null,"thtttoan":"TM, CK","msttcgp":"0100684378","cqtcks":"{\"Subject\":\"CN=CỤC THUẾ,O=BỘ TÀI CHÍNH,L=Hà Nội,C=VN\",\"SerialNumber\":\"2A05F812673607B0\",\"Issuer\":\"CN=CA phục vụ các cơ quan Nhà nước G2, O=Ban Cơ yếu Chính phủ, C=VN\",\"NotAfter\":\"2030-03-12T16:36:53\",\"NotBefore\":\"2025-03-13T16:36:53\",\"SigningTime\":\"2026-10-31T15:04:40\"}","gchu":"","kqcht":null,"hdntgia":null,"tgtkcthue":null,"tgtkhac":null,"nmshchieu":null,"nmnchchieu":null,"nmnhhhchieu":null,"nmqtich":null,"ktkhthue":null,"nmstttoan":null,"nmttttoan":null,"mddkdoanh":null,"tddkdoanh":null,"dcddkdoanh":null,"nmmtinh":null,"nmttinh":null,"nmmxa":null,"nmtxa":null,"nbmstgoc":null,"lddctthe":null,"hdhhdvu":[{"idhdon":"d2240f37-51af-48d9-98f1-f01d4a0810ff","id":"f17fb102-d5c2-4495-ab10-68cd15d09312","dgia":2873316.0,"dvtinh":"vé","ltsuat":"8%","sluong":1.0,"stbchu":null,"stckhau":null,"stt":1,"tchat":1,"ten":"Vé máy bay nội địa VCA-HAN","thtcthue":null,"thtien":2873316.0,"tlckhau":null,"tsuat":0.08,"tthue":null,"sxep":1,"ttkhac":[{"ttruong":"Amount","kdlieu":"numeric","dlieu":"3103181.28"},{"ttruong":"VATAmount","kdlieu":"numeric","dlieu":"229865.28"}],"dvtte":null,"tgia":null,"tthhdtrung":[],"mnnnghe":null}],"qrcode":"00020101021202000400260052005300540058005900600062009964000001100109213133020110306C26TBN04024005082026103106093103181.0","ttmstten":null,"ladhddtten":null,"hdxkhau":null,"hdxkptquan":null,"hdgktkhthue":null,"hdonLquans":null,"tthdclquan":false,"pdndungs":null,"hdtbssrses":null,"hdTrung":null,"isHDTrung":null,"hdcttchinh":0,"dlhquan":null,"dlnhtmai":null,"bltphi":null,"tcsvhdChinh":null}
  
  4. API In hóa đơn, thực chất gọi lại detail và render nội dung và gọi hàm in của trình duyệt
  5. API Export hóa đơn:
  https://hoadondientu.gdt.gov.vn/api/query/invoices/export-excel?sort=tdlap:desc&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59
  curl --url 'https://hoadondientu.gdt.gov.vn/api/query/invoices/export-excel?sort=tdlap:desc&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi' \
  -H 'action: Xu%E1%BA%A5t%20h%C3%B3a%20%C4%91%C6%A1n%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)' \
  -H 'authorization: Bearer <JWT>' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /tra-cuu/tra-cuu-hoa-don' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don' \
  -H 'request-id: 3c2048ac-98d0-454a-9b00-09d668967f0b' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36'
  Response: file excel
  6. API Export XML:
  https://hoadondientu.gdt.gov.vn/api/query/invoices/export-xml?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1
  curl --url 'https://hoadondientu.gdt.gov.vn/api/query/invoices/export-xml?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi' \
  -H 'action: Xu%E1%BA%A5t%20xml%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)' \
  -H 'authorization: Bearer <JWT>' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /tra-cuu/tra-cuu-hoa-don' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don' \
  -H 'request-id: d55b32f8-d7e2-41b0-9a80-da89ded372a6' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36'
  Response: file zip
  

  4. API tra cứu hóa đơn mua vào
  https://hoadondientu.gdt.gov.vn/api/query/invoices/purchase?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nbmst==1234567890;tthai==1;ttxly==6;khmshdon==8;shdon==123;khhdon==321;unhiem==1;nmcmnd==999999999999

  curl --url 'https://hoadondientu.gdt.gov.vn/api/query/invoices/purchase?sort=tdlap:desc&size=15&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nbmst==1234567890;tthai==1;ttxly==6;khmshdon==8;shdon==123;khhdon==321;unhiem==1;nmcmnd==999999999999' \
  -H 'accept: application/json, text/plain, */*' \
  -H 'accept-language: vi' \
  -H 'action: T%C3%ACm%20ki%E1%BA%BFm%20(h%C3%B3a%20%C4%91%C6%A1n%20mua%20v%C3%A0o)' \
  -H 'authorization: Bearer <JWT>' \
  -b '<COOKIES: jwt=<JWT>; JSESSIONID=<…>; cookie WAF/F5 …>' \
  -H 'end-point: /tra-cuu/tra-cuu-hoa-don' \
  -H 'priority: u=1, i' \
  -H 'referer: https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don' \
  -H 'request-id: 0cca8e2a-a837-4846-bcef-8cedc5b49116' \
  -H 'sec-ch-ua: "Not:A-Brand";v="8", "Chromium";v="156", "Google Chrome";v="156"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Windows"' \
  -H 'sec-fetch-dest: empty' \
  -H 'sec-fetch-mode: cors' \
  -H 'sec-fetch-site: same-origin' \
  -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36'


Các tham số:
4.1 Ngày lập hóa đơn (*): tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59
4.2 MST Người mua: nmmst==1112232234
4.3 CCCD Người mua: nmcmnd==036402421225
4.4 Trạng thái hóa đơn  (*): 
  tthai: trạng thái hóa đơn: giá trị được lấy từ dropdownlist sau
  <div id="5eb513f8-034e-4394-b4f6-aeba1571e111" style="overflow: auto; transform: translateZ(0px);"><ul role="listbox" class="ant-select-dropdown-menu  ant-select-dropdown-menu-root ant-select-dropdown-menu-vertical" tabindex="0"><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Tất cả</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item ant-select-dropdown-menu-item-selected" aria-selected="true" style="user-select: none;">Hóa đơn mới</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn thay thế</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn điều chỉnh</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị thay thế</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị điều chỉnh</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Hóa đơn đã bị hủy</li></ul></div>
  Tất cả: không cần truyền
  Hóa đơn mới: 1
  Hóa đơn thay thế: 2
  Hóa đơn điều chỉnh: 3
  Hóa đơn đã bị thay thế: 4
  Hóa đơn đã bị điều chỉnh: 5
  Hóa đơn đã bị hủy: 6
4.5 Kết quả kiểm tra  (*): ttxly==0
Giá trị tham số này lấy từ dropdownlist sau
<div id="17234b61-a8a0-48dc-bc10-0334573eedf1" style="overflow: auto; transform: translateZ(0px);"><ul role="listbox" class="ant-select-dropdown-menu  ant-select-dropdown-menu-root ant-select-dropdown-menu-vertical" tabindex="0"><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Đã cấp mã hóa đơn</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item ant-select-dropdown-menu-item-selected" aria-selected="true" style="user-select: none;">Cục Thuế đã nhận không mã</li><li role="option" unselectable="on" class="ant-select-dropdown-menu-item" aria-selected="false" style="user-select: none;">Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền</li></ul></div>
- Đã cấp mã hóa đơn: ttxly==5
- Cục Thuế đã nhận không mã: ttxly==6
- Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền: ttxly==8

4.6 Ký hiệu mẫu số hóa đơn: khmshdon==9 <Giá trị từ 1 -> 9>
4.7 Hóa đơn ủy nhiệm: unhiem==1 (Dạng checkbox, không check thì bỏ trống)
4.8: Số hóa đơn: hdon==323
4.9: Kí hiệu mẫu số hóa đơn: khhdon==121