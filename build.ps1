cd dotnet-openapi-generator

$versions = @("5.0", "6.0", "7.0")

foreach ($i in $versions) {
   Write-Host "Building for dotnet $i"
   dotnet pack -c Release -p:TargetFrameworkVersion=$i
}

cd ..