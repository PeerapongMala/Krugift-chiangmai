# Krugift-chiangmai

ระบบคะแนนวิชาคณิต: ครูกรอกหรือ import คะแนนจาก Excel ส่วนนักเรียนเข้ามาดูคะแนนของตัวเองและท้วงคะแนนได้

> 🚧 กำลังพัฒนา · เสร็จแล้ว: scaffold + DB schema/migration แรก (API มีแค่ `/api/health`) · ยังไม่มี: login, `Dockerfile`, `docker-compose.yml`, `.env.example`
> ⚠️ API รัน migration ตอน start ถ้ายังไม่ได้ตั้ง `ConnectionStrings:Default` ด้วย user-secrets (ค่า default ชี้ไปที่ postgres บน localhost) `dotnet run` จะ error

## Stack
- **Backend:** .NET 10 Minimal API + EF Core (PostgreSQL)
- **Frontend:** React + Vite + TypeScript + Tailwind + shadcn/ui (ใช้ **Bun**)
- **DB:** PostgreSQL ใช้ Neon ตอน dev บนเครื่องที่ไม่มี Docker และตอน production
- **Auth:** Google OAuth + รหัสนักเรียน/รหัสส่วนตัว
- **Deploy:** Docker image เดียว → Render

## โครงโปรเจกต์
```
src/Api/            ASP.NET Core API (เสิร์ฟหน้าเว็บที่ build แล้วด้วย)
src/Api.Tests/      xUnit
web/                React (Vite + Bun)
Dockerfile          build web + api เป็น image เดียว
docker-compose.yml  app + postgres สำหรับเครื่องที่มี Docker
dotnet-tools.json   local tools (dotnet-ef)
.env.example        ตัวอย่าง env
```

---

## วิธีรัน — แบบที่ 1: ไม่มี Docker (รันบนเครื่องโดยตรง)

### ต้องติดตั้ง
| เครื่องมือ | คำสั่งติดตั้ง (Windows) |
|---|---|
| .NET SDK 10 | `winget install Microsoft.DotNet.SDK.10` |
| Bun | `winget install Oven-sh.Bun` |
| Git | `winget install Git.Git` |

DB ไม่ต้องลง ใช้ Neon (สมัครฟรีที่ neon.tech แล้วสร้าง branch `dev`)

### ตั้งค่าครั้งแรก
```bash
git clone https://github.com/PeerapongMala/Krugift-chiangmai.git
cd Krugift-chiangmai

dotnet tool restore          # ลง dotnet-ef ตามเวอร์ชันใน dotnet-tools.json
cd web && bun install && cd ..
```

ตั้งค่า secret ด้วย user-secrets (ค่าจะเก็บในเครื่อง ไม่เข้า git):
```bash
cd src/Api
dotnet user-secrets set "ConnectionStrings:Default" "<Neon connection string>"
dotnet user-secrets set "Google:ClientId" "<client id>"
dotnet user-secrets set "Google:ClientSecret" "<client secret>"
dotnet user-secrets set "TEACHER_EMAILS" "teacher@gmail.com"
```

### รัน (เปิด 2 terminal)
```bash
# terminal 1 — API (migration รันอัตโนมัติตอน start)
dotnet run --project src/Api

# terminal 2 — หน้าเว็บ
cd web && bun run dev
```
เปิดเว็บที่ http://localhost:5173 (Vite ส่ง `/api` ต่อไปให้ API)

---

## วิธีรัน — แบบที่ 2: มี Docker

### ต้องติดตั้ง
- Docker Desktop และ Git เท่านั้น (ไม่ต้องลง .NET, Bun หรือ PostgreSQL)

### รัน
```bash
git clone https://github.com/PeerapongMala/Krugift-chiangmai.git
cd Krugift-chiangmai
cp .env.example .env         # แล้วแก้ค่า Google / TEACHER_EMAILS ใน .env
docker compose up --build
```
เปิดเว็บที่ http://localhost:8080 · DB เป็น postgres ใน compose ไม่ต้องใช้ Neon

อยากได้ hot reload แต่ใช้ DB จาก Docker:
```bash
docker compose up postgres   # รันแค่ DB
# แล้วรัน API + web ตามแบบที่ 1 โดยตั้ง ConnectionStrings:Default ให้ชี้ไปที่ localhost:5432
```

---

## คำสั่งที่ใช้บ่อย
| งาน | คำสั่ง |
|---|---|
| สร้าง migration | `dotnet ef migrations add <ชื่อ> --project src/Api` |
| อัปเดต DB ด้วยมือ | `dotnet ef database update --project src/Api` |
| รันเทสต์ backend | `dotnet test` |
| build หน้าเว็บ | `cd web && bun run build` |
| เพิ่ม component shadcn | `cd web && bunx shadcn@latest add button` |

## ข้อควรระวัง
- **ห้าม commit** connection string, Google secret, `.env` หรือไฟล์ Excel ที่มีข้อมูลนักเรียนจริง
- คะแนนเป็นข้อมูลส่วนบุคคลของนักเรียน ถ้า repo เป็น public ไฟล์ backup ต้องเข้ารหัสทุกครั้ง
