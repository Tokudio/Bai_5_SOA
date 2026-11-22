"""Offline structural and accounting-fixture checks; NOT a .NET execution test."""
from pathlib import Path
from decimal import Decimal
import json
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[1]
services = {
    "Demo.ProductService": (5181, "products.db"),
    "Demo.OrderService": (5182, "orders.db"),
    "ReportingService": (5183, "reporting.db"),
}
for svc, (port, db) in services.items():
    d = root / "src" / svc
    cfg = json.loads((d / "appsettings.json").read_text(encoding="utf-8"))
    assert f":{port}" in cfg["Urls"]
    assert db in cfg["DatabasePath"]
    assert ET.parse(d / (svc + ".csproj")).find(".//TargetFramework").text == "net8.0"
    print("PASS", svc, "port", port, "DB", db)
report = root / "src" / "ReportingService"
for endpoint in ('summary', 'products', 'orders', 'sync'):
    assert f'"{endpoint}"' in (report / "Controllers" / "ReportsController.cs").read_text(encoding="utf-8")
assert 'products/{productId:int}' in (report / "Controllers" / "CostsController.cs").read_text(encoding="utf-8")
assert 'GetFromJsonAsync' in (report / "Services" / "SyncService.cs").read_text(encoding="utf-8")
assert 'SuccessfulStatuses' in (report / "Services" / "ReportQueryService.cs").read_text(encoding="utf-8")
print("PASS endpoint map and HTTP data access present")

costs = {1: Decimal(15000000), 2: Decimal(180000), 3: Decimal(450000), 4: Decimal(800000)}
products = {1: (8, Decimal(19000000)), 2: (35, Decimal(300000)),
            3: (20, Decimal(750000)), 4: (15, Decimal(1200000))}
orders = [
    ('Completed', [(1, 1, Decimal(19000000)), (2, 2, Decimal(300000))]),
    ('Paid', [(3, 2, Decimal(750000)), (4, 1, Decimal(1200000))]),
    ('Pending', [(2, 1, Decimal(300000))]),
    ('Cancelled', [(1, 1, Decimal(19000000))]),
]
successful = [items for status,items in orders if status in ('Paid','Completed','Delivered')]
lines = [item for items in successful for item in items]
stock = sum(v[0] for v in products.values())
sold = sum(qty for _, qty, _ in lines)
revenue = sum(qty * unit_price for _, qty, unit_price in lines)
cost = sum(qty * costs[pid] for pid, qty, unit_price in lines)
profit = revenue - cost
assert (len(products), stock, sold, revenue, cost, profit) == \
    (4, 78, 6, Decimal(22300000), Decimal(17060000), Decimal(5240000))
print('PASS offline fixture arithmetic: stock=78 sold=6 revenue=22,300,000 cost=17,060,000 profit=5,240,000')
for script in ['START_ALL_WINDOWS.ps1','TEST_API_WINDOWS.ps1']:
    assert (root/'scripts'/script).exists()
print('PASS Windows start and integration test scripts exist')
