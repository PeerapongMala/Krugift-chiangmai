# image เดียวจบ: build เว็บด้วย Bun → publish API → เอาเว็บไปไว้ใน wwwroot ให้ API เสิร์ฟเอง
# เว็บกับ API จึงอยู่โดเมนเดียวกัน ไม่ต้องตั้ง CORS และ cookie SameSite=Lax ใช้ได้ตามปกติ

# ---------- 1. เว็บ ----------
FROM oven/bun:1.4-alpine AS web
WORKDIR /web

# คัดลอกไฟล์ล็อกก่อน layer ติดตั้ง dependency จะได้ cache ไว้ตราบที่ dependency ไม่เปลี่ยน
COPY frontend/package.json frontend/bun.lock ./
RUN bun install --frozen-lockfile

COPY frontend/ ./
RUN bun run build

# ---------- 2. API ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS api
WORKDIR /src

COPY global.json ./
COPY backend/src/Api/Api.csproj backend/src/Api/
RUN dotnet restore backend/src/Api/Api.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Api/Api.csproj -c Release -o /app/publish

# ---------- 3. ตัวจริงที่รัน ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# ไม่รันด้วย root · image ของ .NET มี user ชื่อ app มาให้แล้ว
USER app

COPY --from=api /app/publish ./
COPY --from=web /web/dist ./wwwroot

ENV ASPNETCORE_ENVIRONMENT=Production

# Render และ PaaS อื่นส่งพอร์ตมาทาง $PORT ต้องผ่าน shell ตัวแปรถึงจะถูกแทนค่า
# exec ทำให้ dotnet เป็น PID 1 เอง สัญญาณ SIGTERM ตอนสั่งปิดจะถึงแอปจริง (ปิดแบบสุภาพ)
EXPOSE 8080
CMD ["sh", "-c", "exec dotnet Api.dll --urls http://+:${PORT:-8080}"]
