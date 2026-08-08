Bước 1:
dotnet user-secrets init --project ".\CafeChain\CafeChain.csproj"
dotnet user-secrets init --project ".\CafeChain.PrintBridge\CafeChain.PrintBridge.csproj"
Bước 2:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Server=(localdb)\\MSSQLLocalDB;Database=CafeChain;Trusted_Connection=True;TrustServerCertificate=True;' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "Email:Password" 'gmmshoizpplowahh' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "PayOS:ClientId" 'd7e8cdb0-6f44-42b1-bb9b-369388df1318' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "PayOS:ApiKey" '18732267-93a4-4337-8d72-810fadd84f5d' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "PayOS:ChecksumKey" '43b347dbf4b22f3a770c26281f3de0553927cbda254eea1f9f842c8c37c2da23' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "Cloudinary:ApiKey" '245282662417859' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "Cloudinary:ApiSecret" 'V2QuJfhxdcmxdunhW1ltveBdOt8' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "PrintBridge:ApiKey" 'pb-secret-key-change-in-production-2026' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "Jwt:Key" 'CafeChain-POS-JWT-Secret-Key-Change-In-Production-2026-Min32Chars!' --project ".\CafeChain\CafeChain.csproj"

dotnet user-secrets set "Pexels:ApiKey" 'eSwM9YFMFPxyzsOfRKeb7H9gqvFLWECJHnpaeNGxE36IwmXWrSHq5LAj' --project ".\CafeChain\CafeChain.csproj"
Bước 3:
dotnet user-secrets set "PrintBridge:ApiKey" 'pb-secret-key-change-in-production-2026' --project ".\CafeChain.PrintBridge\CafeChain.PrintBridge.csproj"
