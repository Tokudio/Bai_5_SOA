# BÀI TẬP SOA — REPORTING SERVICE (.NET 8)

**Tên:** Dịch vụ báo cáo sản phẩm và đơn hàng  
**Mục tiêu:** Thống kê hàng tồn, số lượng bán, doanh thu, chi phí và lợi nhuận.  
**Kiến trúc:** 3 dịch vụ chạy độc lập bằng HTTP, mỗi dịch vụ sử dụng database SQLite riêng.  
**Nền tảng:** ASP.NET Core Web API (.NET 8), Entity Framework Core 8, Swagger, SQLite.

> **Phần nộp chính:** `src/ReportingService/`. Hai project `Demo.ProductService` và `Demo.OrderService` được kèm theo để cô có thể chạy thử ngay, không phụ thuộc code của người khác. Chúng mô phỏng API sản phẩm/đơn hàng của bài tập trước. Khi ghép vào project có sẵn, cấu hình địa chỉ API thật và điều chỉnh DTO cho khớp JSON trả về.

## 1. Các yêu cầu đã thực hiện

| Yêu cầu | Cách triển khai |
|---|---|
| Dịch vụ báo cáo độc lập | `ReportingService` chạy ở `http://localhost:5183` |
| Cơ sở dữ liệu riêng | `data/reporting.db` (cùng với `products.db`, `orders.db` đều là các file DB tách biệt) |
| Tồn kho | Đọc `StockQuantity` từ Product API, lưu thành snapshot riêng |
| Số lượng bán | Tổng `Quantity` của đơn Paid / Completed / Delivered |
| Doanh thu | Tổng `Quantity × UnitPrice` trên những đơn thành công |
| Chi phí | Giá vốn hàng bán = tổng `Quantity × UnitCostAtSale` |
| Lợi nhuận | Doanh thu − giá vốn |
| Theo sản phẩm | `GET /api/reports/products` và `/products/{id}` |
| Theo đơn hàng | `GET /api/reports/orders` và `/orders/{id}` |
| Khoảng ngày | `?from=2026-11-18&to=2026-11-19` |
| Đồng bộ giữa service | `POST /api/reports/sync` lấy sản phẩm/đơn hàng qua HTTP |
| Không đếm trùng | Đồng bộ lần nữa cập nhật snapshot theo ID, không nhân đôi doanh thu |
| API tài liệu | Swagger của từng service |

### Sơ đồ kiến trúc

```text
 +--------------------------+          +------------------------+
 | Demo.ProductService      |          | Demo.OrderService      |
 | http://localhost:5181    |          | http://localhost:5182  |
 | DB data/products.db      |          | DB data/orders.db      |
 +-------------+------------+          +------------+-----------+
               | GET /api/products                  | GET /api/orders
               +----------------+  +---------------+
                                |  |
                   +------------v--v------------+
                   | ReportingService            |
                   | http://localhost:5183       |
                   | DB data/reporting.db        |
                   | POST /api/reports/sync      |
                   | GET /api/reports/...        |
                   +----------------------------+
```

**SOA:** Dịch vụ báo cáo không truy cập trực tiếp DB của Product/Order. Nó gọi API HTTP và lưu **bản sao dữ liệu (read model / snapshot)** trên database của chính nó. Nếu dịch vụ nguồn tắt, các báo cáo đã đồng bộ vẫn truy vấn được. Để lấy dữ liệu mới, chạy lại `POST /api/reports/sync`.

## 2. Yêu cầu máy

- Windows 10/11; .NET **8 SDK** (`dotnet --version`).
- Internet ở **lần đầu** để NuGet restore `Microsoft.EntityFrameworkCore.Sqlite` và `Swashbuckle.AspNetCore`.
- Không cần SQL Server, Docker hoặc RabbitMQ để chạy ZIP demo này.
- Đảm bảo các cổng `5181`, `5182`, `5183` chưa bị ứng dụng khác sử dụng.

**Visual Studio:** Có thể mở `SOA_Reporting_Submission.sln` để xem 3 project và code. Chạy trực tiếp theo 3 terminal như bên dưới để chúng hoạt động ở 3 cổng riêng.

## 3. Chạy nhanh trên Windows

Giải nén ZIP vào ví dụ `D:\Workspace\SOA_Reporting_Submission`.

**Cách A (tự mở ba terminal, dễ kiểm soát):**

Terminal 1 — Product API:

```powershell
cd D:\Workspace\SOA_Reporting_Submission\src\Demo.ProductService
dotnet run --no-launch-profile
```

Terminal 2 — Order API:

```powershell
cd D:\Workspace\SOA_Reporting_Submission\src\Demo.OrderService
dotnet run --no-launch-profile
```

Terminal 3 — Reporting API:

```powershell
cd D:\Workspace\SOA_Reporting_Submission\src\ReportingService
dotnet run --no-launch-profile
```

**Cách B (mở ba cửa sổ tự động):** mở PowerShell trong thư mục gốc của ZIP, chạy:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\START_ALL_WINDOWS.ps1
```

Đợi từng cửa sổ hiện `Now listening on: http://localhost:...`.

### Swagger

- Product API: <http://localhost:5181/swagger>
- Order API: <http://localhost:5182/swagger>
- **Reporting API: <http://localhost:5183/swagger>**

## 4. Cách chạy báo cáo (4 bước)

Sau khi cả ba API đang chạy:

**Bước 1 — Đồng bộ dữ liệu nguồn:**

```powershell
Invoke-RestMethod -Method Post http://localhost:5183/api/reports/sync
```

**Bước 2 — Thiết lập giá vốn.** Product API cũ chỉ có `Price` (giá bán), **không có giá nhập**. Vì vậy ReportingService cho phép nhập giá vốn riêng; nếu chưa nhập, `cost` và `profit` của các đơn được ghi nhận sẽ là `null`, **không tự giả định giá vốn = 0**.

```powershell
$base = 'http://localhost:5183/api/reports/costs/products'
Invoke-RestMethod -Method Put "$base/1" -ContentType 'application/json' -Body '{"unitCost":15000000}'
Invoke-RestMethod -Method Put "$base/2" -ContentType 'application/json' -Body '{"unitCost":180000}'
Invoke-RestMethod -Method Put "$base/3" -ContentType 'application/json' -Body '{"unitCost":450000}'
Invoke-RestMethod -Method Put "$base/4" -ContentType 'application/json' -Body '{"unitCost":800000}'
```

> Khi đặt giá vốn sau đồng bộ, dịch vụ điền giá vốn vào **các dòng đơn chưa từng có giá vốn**. Trong nghiệp vụ thực tế, giá vốn phải được chốt vào thời điểm bán, không nên gán hồi tố bằng giá nhập hôm nay.

**Bước 3 — Đồng bộ lại (tùy chọn):**

```powershell
Invoke-RestMethod -Method Post http://localhost:5183/api/reports/sync
```

**Bước 4 — Xem báo cáo:**

```powershell
Invoke-RestMethod http://localhost:5183/api/reports/summary
Invoke-RestMethod http://localhost:5183/api/reports/products
Invoke-RestMethod http://localhost:5183/api/reports/products/2
Invoke-RestMethod http://localhost:5183/api/reports/orders
Invoke-RestMethod http://localhost:5183/api/reports/orders/1
Invoke-RestMethod 'http://localhost:5183/api/reports/summary?from=2026-11-19&to=2026-11-19'
```

## 5. Kết quả mong đợi với dữ liệu mẫu

Dữ liệu mẫu từ hai service nguồn gồm 4 sản phẩm và 4 đơn hàng:

- Đơn #1: Completed; Laptop ×1 + Chuột ×2 → doanh thu 19.600.000 VND.
- Đơn #2: Paid; Bàn phím ×2 + Tai nghe ×1 → doanh thu 2.700.000 VND.
- Đơn #3: Pending; Chuột ×1 → **không** tính vào doanh thu.
- Đơn #4: Cancelled; Laptop ×1 → **không** tính vào doanh thu.

| Chỉ tiêu tổng | Kết quả mẫu |
|---|---:|
| Tổng số sản phẩm | 4 |
| Tổng hàng tồn theo Product API | 78 |
| Tổng đơn hàng | 4 |
| Đơn hàng thành công | 2 |
| Tổng lượng bán | 6 |
| Doanh thu | **22.300.000 VND** |
| Chi phí giá vốn | **17.060.000 VND** |
| Lợi nhuận gộp | **5.240.000 VND** |

| Sản phẩm | Tồn kho | Đã bán | Doanh thu | Chi phí | Lợi nhuận |
|---|---:|---:|---:|---:|---:|
| Laptop Dell Inspiron | 8 | 1 | 19.000.000 | 15.000.000 | 4.000.000 |
| Chuột không dây | 35 | 2 | 600.000 | 360.000 | 240.000 |
| Bàn phím cơ | 20 | 2 | 1.500.000 | 900.000 | 600.000 |
| Tai nghe | 15 | 1 | 1.200.000 | 800.000 | 400.000 |

### Kiểm thử tự động trên máy Windows (chạy HTTP thật)

Sau khi bật cả ba service, trong thư mục gốc ZIP chạy:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\TEST_API_WINDOWS.ps1
```

Script gọi **HTTP API thật**, tự đồng bộ, cập nhật giá vốn, kiểm tra doanh thu/chi phí/lợi nhuận, trạng thái Pending/Cancelled, báo cáo sản phẩm, lọc ngày và tính **idempotent** khi sync nhiều lần. Kết quả đúng sẽ hiển thị `ALL INTEGRATION CHECKS PASSED!`.

## 6. Mô tả API ReportingService

| Method | Endpoint | Ý nghĩa |
|---|---|---|
| GET | `/health` | Kiểm tra service sống |
| POST | `/api/reports/sync` | Lấy dữ liệu qua HTTP từ Product/Order API và lưu DB riêng |
| PUT | `/api/reports/costs/products/{productId}` | Đặt đơn giá vốn để tính chi phí/lợi nhuận |
| GET | `/api/reports/summary` | Tổng quan tồn kho, bán được, doanh thu, chi phí, lợi nhuận |
| GET | `/api/reports/products` | Báo cáo theo từng sản phẩm |
| GET | `/api/reports/products/{id}` | Chi tiết báo cáo một sản phẩm |
| GET | `/api/reports/orders` | Báo cáo theo từng đơn hàng (kể cả chưa thanh toán/hủy) |
| GET | `/api/reports/orders/{id}` | Chi tiết báo cáo một đơn |

`GET` list và `summary` chấp nhận `?from=YYYY-MM-DD&to=YYYY-MM-DD` (bao gồm hai ngày đầu/cuối).

**Quy tắc nghiệp vụ:**

1. Đơn `Paid`, `Completed`, `Delivered` mới ghi nhận doanh thu và số lượng bán. Trạng thái `Pending`, `Cancelled` không tính. Dữ liệu đặt hàng thực tế có thể cần sửa các trạng thái này.
2. `Revenue = Σ(Quantity × UnitPrice)` (giá bán tại thời điểm tạo đơn). `Cost = Σ(Quantity × UnitCostAtSale)`. `Profit = Revenue - Cost`.
3. `UnitCostAtSale` được lưu trong bảng chi tiết đơn báo cáo và giữ lại khi đồng bộ nhiều lần. Nếu chưa biết giá vốn của sản phẩm đã bán, trả `cost: null` / `profit: null`, kèm danh sách `productsMissingCost`.
4. `CurrentStockQuantity` là số lượng tồn kho **hiện tại theo Product API**, không phải tồn kho đầu kỳ trừ số bán của bộ lọc ngày.
5. Chi phí ở đây là **giá vốn hàng bán**, chưa tính vận chuyển, marketing, thuế, chiết khấu, hoàn trả và các chi phí vận hành.
6. Dịch vụ đồng bộ *theo yêu cầu* (push button/API), không tự cập nhật theo thời gian thực. Dữ liệu không thay đổi khi source API tạm thời không chạy.
7. Số tiền được xử lý trong C# bằng `decimal`, lưu SQLite dạng chuỗi chính xác, làm tròn 2 chữ số thập phân khi tổng hợp.

## 7. Ghép với ProductService.Api và OrderService.Api có sẵn

Trong `src/ReportingService/appsettings.json` đổi:

```json
"UpstreamServices": {
  "Products": "http://localhost:7013/",
  "Orders": "http://localhost:7064/"
}
```

Các URL trên **chỉ là ví dụ**, phải dùng đúng cổng thực tế của project cũ. Nếu API chạy HTTPS, điền `https://localhost:.../` đúng theo port. Khi đó cấu trúc JSON cần tương thích:

**Product GET `/api/products`:**

```json
[
  { "id": 1, "name": "Laptop", "price": 19000000, "stockQuantity": 8 }
]
```

**Order GET `/api/orders`:**

```json
[
  {
    "id": 1, "customerName": "Khach A", "status": "Completed",
    "createdAt": "2026-11-18T08:00:00Z", "totalAmount": 19000000,
    "items": [ { "id": 1, "productId": 1, "quantity": 1, "unitPrice": 19000000 } ]
  }
]
```

Nếu API cũ dùng DTO khác hoặc `GET /api/orders` chỉ trả metadata **không có `items`**, cần đổi `Contracts/UpstreamDtos.cs`, `Services/SyncService.cs` hoặc gọi endpoint detail của OrderService để lấy items. Đừng sửa database của ReportingService để trỏ trực tiếp sang database của 2 service kia.

## 8. Cấu trúc source

```text
SOA_Reporting_Submission/
  src/
    ReportingService/                 <-- DỊCH VỤ CẦN NỘP
      Program.cs
      Controllers/ReportsController.cs
      Controllers/CostsController.cs
      Contracts/{UpstreamDtos,ReportDtos}.cs
      Data/ReportingDbContext.cs
      Models/ReportEntities.cs
      Services/{SyncService,ReportQueryService}.cs
      appsettings.json
    Demo.ProductService/             <-- Dữ liệu mẫu: sản phẩm, DB riêng
    Demo.OrderService/               <-- Dữ liệu mẫu: đơn hàng, DB riêng
  scripts/
    START_ALL_WINDOWS.ps1
    TEST_API_WINDOWS.ps1
  tests/
    check_submission.py              <-- kiểm tra cấu trúc và phép tính offline
  SOA_Reporting_Submission.sln
  README.md
  .gitignore
```

