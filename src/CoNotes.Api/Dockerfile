# 從 repo 根目錄建置(context 需要包含 Directory.Build.props/Directory.Packages.props):
#   docker build -f src/CoNotes.Api/Dockerfile -t conotes-api .
#
# SDK image tag 跟 global.json 的版本(10.0.400)手動保持一致——浮動的 `10.0` tag
# 目前解析到的是 10.0.302, global.json 沒設 rollForward 時 dotnet CLI 會直接拒絕
# 執行(SDK resolution 失敗), 不是「用比較舊的版本頂替」這種隱性風險, 兩邊沒對齊
# 建置會直接壞掉。
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/CoNotes.Api/CoNotes.Api.csproj
RUN dotnet publish src/CoNotes.Api/CoNotes.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "CoNotes.Api.dll"]
