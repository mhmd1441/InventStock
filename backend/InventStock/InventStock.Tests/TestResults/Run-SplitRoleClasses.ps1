$ErrorActionPreference = 'Stop'
$project = 'backend\InventStock\InventStock.Tests\InventStock.Tests.csproj'
$classes = @(
  'InventStock.Tests.Api.CustomerApiTests',
  'InventStock.Tests.Api.PricingApiTests',
  'InventStock.Tests.Api.SaleApiTests',
  'InventStock.Tests.Infrastructure.PostgresSaleTests',
  'InventStock.Tests.Infrastructure.PostgresSaleReturnTests',
  'InventStock.Tests.Infrastructure.PostgresSaleExchangeTests',
  'InventStock.Tests.Infrastructure.PostgresCustomerTests',
  'InventStock.Tests.Infrastructure.PostgresCashTransferTests',
  'InventStock.Tests.Infrastructure.PostgresCashReconciliationTests',
  'InventStock.Tests.Infrastructure.PostgresSupplierPaymentTests',
  'InventStock.Tests.Infrastructure.PostgresLegacySaleReturnTests',
  'InventStock.Tests.Infrastructure.ReferenceUpgradeTests',
  'InventStock.Tests.Infrastructure.PostgresCatalogueTests'
)
$results = @()
foreach ($class in $classes) {
  $safe = $class.Split('.')[-1]
  $out = Join-Path $PSScriptRoot ("split-role-" + $safe + '.stdout.log')
  $err = Join-Path $PSScriptRoot ("split-role-" + $safe + '.stderr.log')
  Remove-Item -LiteralPath $out,$err -ErrorAction SilentlyContinue
  $arguments = @('test',$project,'--no-build','--no-restore','-c','Release','--filter',"FullyQualifiedName~$class",'-m:1','--logger',"trx;LogFileName=split-role-$safe.trx")
  $process = Start-Process -FilePath dotnet -ArgumentList $arguments -WorkingDirectory (Get-Location) -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -WindowStyle Hidden
  if (-not $process.WaitForExit(180000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    $results += [pscustomobject]@{ Class=$class; Status='timeout'; ExitCode=$null }
    continue
  }
  $results += [pscustomobject]@{ Class=$class; Status=if($process.ExitCode -eq 0){'passed'}else{'failed'}; ExitCode=$process.ExitCode }
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'split-role-summary.json')