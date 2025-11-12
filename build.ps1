cd dotnet-openapi-generator

$versions = @("10.0", "9.0", "8.0")
$postfix = "-preview.17"

foreach ($i in $versions) {
   Write-Host "Building for dotnet $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=$i -p:openapi-generator-version-string=$i.0$postfix
}

$versions = @("2.1", "2.0")

foreach ($i in $versions) {
   Write-Host "Building for dotnet standard $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=10.0 -p:openapi-generator-version-string=$i.0$postfix -p:openapi-generator-netstandard=$i
}

cd ..