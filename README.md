# Math Aj.Gift (Krugift-chiangmai)

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
backend/src/Api/     ASP.NET Core API (เสิร์ฟหน้าเว็บที่ build แล้วด้วย)
backend/tests/       xUnit
frontend/            React (Vite + Bun)
Dockerfile           build frontend + api เป็น image เดียว
docker-compose.yml   app + postgres สำหรับเครื่องที่มี Docker
dotnet-tools.json    local tools (dotnet-ef)
.env.example         ตัวอย่าง env
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
cd frontend && bun install && cd ..
```

ตั้งค่า secret ด้วย user-secrets (ค่าจะเก็บในเครื่อง ไม่เข้า git):
```bash
cd backend/src/Api
dotnet user-secrets set "ConnectionStrings:Default" "<Neon connection string>"
dotnet user-secrets set "Google:ClientId" "<client id>"
dotnet user-secrets set "Google:ClientSecret" "<client secret>"
dotnet user-secrets set "TEACHER_EMAILS" "teacher@gmail.com"
```

### รัน (เปิด 2 terminal)
```bash
# terminal 1 — API (migration รันอัตโนมัติตอน start)
dotnet run --project backend/src/Api

# terminal 2 — หน้าเว็บ
cd frontend && bun run dev
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
cp .env.example .env         # เติม connection string ของ Neon, Google, TEACHER_EMAILS
docker compose up --build
```
เปิดเว็บที่ http://localhost:8080 · image เดียวมีทั้งเว็บและ API (เว็บ build แล้วอยู่ใน `wwwroot`)

อยากลองกับ postgres ในเครื่องแทน Neon:
```bash
docker compose --profile local-db up --build
# ตั้งใน .env → ConnectionStrings__Default=Host=db;Database=krugift;Username=krugift;Password=krugift
```

---

## ขึ้น production (Render)

Render ฟรีให้ Docker ได้ 1 service ส่วน DB ใช้ Neon แยกต่างหาก

1. **Neon** สร้าง project แล้วคัดลอก connection string แบบ .NET (Npgsql)
2. **Google OAuth** ที่ console.cloud.google.com เพิ่ม redirect URI เป็น `https://<ชื่อ service>.onrender.com/api/signin-google`
   (path เป็น `/api/signin-google` ไม่ใช่ `/signin-google` ตามค่า `CallbackPath` ใน `Auth/AuthSetup.cs`)
3. **Render** → New → Web Service → เลือก repo นี้ → Runtime **Docker** (อ่าน `Dockerfile` ที่ราก repo เอง ไม่ต้องตั้ง build command)
4. ใส่ Environment variables ให้ครบตาม `.env.example`
   `ConnectionStrings__Default` · `Google__ClientId` · `Google__ClientSecret` · `TEACHER_EMAILS`
   (Render ส่ง `PORT` มาให้เอง ไม่ต้องตั้ง)
5. Deploy — ตอน start แอปรัน migration ให้อัตโนมัติ และสร้างครูคนแรกจาก `TEACHER_EMAILS` ถ้าตารางครูยังว่าง

**ข้อควรรู้ของแผนฟรี** service จะหลับเมื่อไม่มีคนใช้ ครั้งแรกที่เปิดหลังหลับจะช้าประมาณ 1 นาที · Neon ไม่ปิด project ถาวรแต่พัก compute ซึ่งปลุกเองใน 1-2 วินาที

### สำรองข้อมูล
- Neon มี point-in-time restore ในตัว (แผนฟรีย้อนได้ 1 วัน) ใช้กู้กรณีลบผิด
- อยากได้ไฟล์เก็บเอง: Neon Dashboard → Backups → Download หรือ `pg_dump "<connection string>" -Fc -f krugift-YYYYMMDD.dump` จากเครื่องที่มี PostgreSQL client
- ไฟล์ dump มีคะแนนและชื่อนักเรียนจริง **ห้ามวางไว้ในโฟลเดอร์ repo** และถ้าจะเก็บบน cloud ต้องเข้ารหัสก่อน

---

## คำสั่งที่ใช้บ่อย
| งาน | คำสั่ง |
|---|---|
| สร้าง migration | `dotnet ef migrations add <ชื่อ> --project backend/src/Api` |
| อัปเดต DB ด้วยมือ | `dotnet ef database update --project backend/src/Api` |
| รันเทสต์ backend | `dotnet test` |
| build หน้าเว็บ | `cd frontend && bun run build` |
| เพิ่ม component shadcn | `cd frontend && bunx shadcn@latest add button` |

## ข้อควรระวัง
- **ห้าม commit** connection string, Google secret, `.env` หรือไฟล์ Excel ที่มีข้อมูลนักเรียนจริง
- คะแนนเป็นข้อมูลส่วนบุคคลของนักเรียน ถ้า repo เป็น public ไฟล์ backup ต้องเข้ารหัสทุกครั้ง
