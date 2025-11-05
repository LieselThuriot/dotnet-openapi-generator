cd dotnet-openapi-generator

$versions = @("8.0", "9.0")
$postfix = "-preview.17"

foreach ($i in $versions) {
   Write-Host "Building for dotnet $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=$i -p:openapi-generator-version-string=$i.0$postfix
}

$versions = @("2.0", "2.1")

foreach ($i in $versions) {
   Write-Host "Building for dotnet standard $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=9.0 -p:openapi-generator-version-string=$i.0$postfix -p:openapi-generator-netstandard=2.0
}

cd ..