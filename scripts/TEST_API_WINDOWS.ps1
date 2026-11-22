# API integration smoke test for Windows PowerShell 5.1 or PowerShell 7.
# Runs against REAL HTTP services / actual SQLite database, not mocked math.
$ErrorActionPreference = 'Stop'
$reportBase = 'http://localhost:5183'
function Check($ok, [string]$message) {
    if (-not $ok) { throw "TEST FAILED: $message" }
    Write-Host "PASS - $message" -ForegroundColor Green
}
function VerifyMoney($actual, $expected, [string]$message) {
    Check ([decimal]$actual -eq [decimal]$expected) "$message (expected $expected, got $actual)"
}
try {
    foreach ($port in @(5181, 5182, 5183)) {
        $healthy = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            try {
                $null = Invoke-RestMethod "http://localhost:$port/health" -TimeoutSec 2
                $healthy = $true
                break
            } catch { Start-Sleep -Seconds 1 }
        }
        Check $healthy "Service port $port responds to /health"
    }
    $sync = Invoke-RestMethod -Method Post -Uri "$reportBase/api/reports/sync"
    Check ($sync.productsSynced -eq 4 -and $sync.ordersSynced -eq 4 -and $sync.itemsSynced -eq 6) `
        'Sync 4 products, 4 orders and 6 source order lines via HTTP'

    $costs = @(
        @{ id = 1; cost = 15000000 },
        @{ id = 2; cost = 180000 },
        @{ id = 3; cost = 450000 },
        @{ id = 4; cost = 800000 }
    )
    foreach ($item in $costs) {
        $body = @{ unitCost = $item.cost } | ConvertTo-Json -Compress
        $null = Invoke-RestMethod -Method Put -Uri "$reportBase/api/reports/costs/products/$($item.id)" `
            -ContentType 'application/json' -Body $body
    }
    Write-Host 'PASS - 4 product purchase costs configured' -ForegroundColor Green
    $null = Invoke-RestMethod -Method Post -Uri "$reportBase/api/reports/sync"

    $summary = Invoke-RestMethod "$reportBase/api/reports/summary"
    Check ($summary.productCount -eq 4) 'Four products in reporting database'
    Check ($summary.currentStockQuantity -eq 78) 'Current stock sum = 78'
    Check ($summary.orderCount -eq 4 -and $summary.successfulOrderCount -eq 2) `
        'Only Paid and Completed orders count as sales'
    Check ($summary.totals.soldQuantity -eq 6) 'Sold quantity = 6'
    Check ($summary.totals.costsComplete -eq $true) 'All recognized sales have cost data'
    VerifyMoney $summary.totals.revenue 22300000 'Total revenue'
    VerifyMoney $summary.totals.cost 17060000 'Total cost of goods sold'
    VerifyMoney $summary.totals.profit 5240000 'Total profit'

    $products = Invoke-RestMethod "$reportBase/api/reports/products"
    Check ($products.Count -eq 4) 'Product report has 4 rows'
    $mouse = Invoke-RestMethod "$reportBase/api/reports/products/2"
    Check ($mouse.totals.soldQuantity -eq 2) 'Mouse quantity sold ignores Pending order'
    VerifyMoney $mouse.totals.profit 240000 'Mouse profit'

    $orders = Invoke-RestMethod "$reportBase/api/reports/orders"
    Check ($orders.Count -eq 4) 'Order report includes all order statuses'
    $pending = Invoke-RestMethod "$reportBase/api/reports/orders/3"
    Check ($pending.isSuccessful -eq $false -and $pending.totals.revenue -eq 0) `
        'Pending order is not recognized as revenue'
    $cancelled = Invoke-RestMethod "$reportBase/api/reports/orders/4"
    Check ($cancelled.isSuccessful -eq $false -and $cancelled.totals.revenue -eq 0) `
        'Cancelled order is not recognized as revenue'

    $nov19 = Invoke-RestMethod "$reportBase/api/reports/summary?from=2026-11-19&to=2026-11-19"
    VerifyMoney $nov19.totals.revenue 2700000 'Date filter from/to works'

    # Idempotency: the same source orders must not be counted twice.
    $null = Invoke-RestMethod -Method Post -Uri "$reportBase/api/reports/sync"
    $again = Invoke-RestMethod "$reportBase/api/reports/summary"
    VerifyMoney $again.totals.revenue 22300000 'Repeated synchronization does not double count revenue'

    Write-Host ''
    Write-Host 'ALL INTEGRATION CHECKS PASSED!' -ForegroundColor Cyan
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host 'Make sure the three API windows are still running. See README.md.'
    exit 1
}
