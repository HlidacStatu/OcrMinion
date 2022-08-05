FROM mcr.microsoft.com/dotnet/runtime:6.0.7-alpine3.16 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build
WORKDIR /src
COPY ["OcrMinion/OcrMinion.csproj", "OcrMinion/"]
RUN dotnet restore "OcrMinion/OcrMinion.csproj"
COPY . .
WORKDIR "/src/OcrMinion"
RUN dotnet build "OcrMinion.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OcrMinion.csproj" -c Release -o /app/publish

FROM base AS final
RUN apk add --no-cache -U tesseract-ocr tesseract-ocr-data-ces tesseract-ocr-data-slk tesseract-ocr-data-deu
WORKDIR /app
COPY --from=publish /app/publish .
#RUN chmod +x start.sh
#CMD ["./start.sh"]
CMD ["dotnet","OcrMinion.dll"]