# 使用 .NET SDK 镜像
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# 复制各个项目文件
COPY ./BulkyWeb/BulkyWeb.csproj ./BulkyWeb/
COPY ./Bulky.DataAccess/Bulky.DataAccess.csproj ./Bulky.DataAccess/
COPY ./Bulky.Models/Bulky.Models.csproj ./Bulky.Models/
COPY ./Bulky.Utility/Bulky.Utility.csproj ./Bulky.Utility/

# 恢复 NuGet 包
RUN dotnet restore ./BulkyWeb/BulkyWeb.csproj

# 复制源代码
COPY . .

# 构建项目
RUN dotnet publish ./BulkyWeb/BulkyWeb.csproj -c Release -o /app/publish

# 使用 .NET ASP.NET 运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BulkyWeb.dll"]
