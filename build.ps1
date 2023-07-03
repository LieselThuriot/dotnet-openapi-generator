cd dotnet-openapi-generator

$versions = @("5.0", "6.0", "7.0")
$postfix = "-preview.11"

foreach ($i in $versions) {
   Write-Host "Building for dotnet $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=$i -p:openapi-generator-version-string=$i.0$postfix
}

$versions = @("2.0", "2.1")

foreach ($i in $versions) {
   Write-Host "Building for dotnet standard $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=6.0 -p:openapi-generator-version-string=$i.0$postfix -p:openapi-generator-netstandard=2.0
}

cd ..