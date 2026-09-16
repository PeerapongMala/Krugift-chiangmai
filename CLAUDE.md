# CLAUDE.md

ระบบคะแนนวิชาคณิตของโรงเรียน: ครูกรอกหรือ import คะแนนจาก Excel ส่วนนักเรียนเข้ามาดูคะแนนของตัวเองและท้วงคะแนนได้
ผู้ใช้ 100-300 คน ครู 1 คน (อนาคตอาจเพิ่ม) · ใช้งานจริง · ดูแลคนเดียว · deploy ฟรี

- แผนเต็ม, schema, API, ลำดับ milestone: **`docs/PLAN.md`** (อ่านก่อนเริ่มงานใหม่)
- วิธีรันทั้งแบบมีและไม่มี Docker: `README.md`

## Stack
- **API:** .NET 10 Minimal API + EF Core 10 (Npgsql) · `backend/src/Api` · อ่าน/เขียน Excel ด้วย ClosedXML
- **Tests:** xUnit **v3** (รันเป็น .exe) · `backend/tests/Api.Tests`
- **Web:** React 19 + Vite 8 + TypeScript + Tailwind v4 + shadcn/ui (style base-nova ใช้ Base UI) + TanStack Query + react-router v8 · `frontend/` · **ใช้ Bun ไม่ใช้ npm**
- **DB:** PostgreSQL บน Neon (เลือก Neon แทน Supabase เพราะ Supabase ฟรีจะ pause project ถ้าไม่มีคนใช้ 7 วัน ซึ่งเจอแน่ช่วงปิดเทอม)
- **Deploy:** Docker image เดียว (build web แล้ว copy ไปไว้ใน `wwwroot` ของ API) → Render ฟรี

## คำสั่ง
```bash
dotnet tool restore                          # dotnet-ef (local tool, ต้องรันจาก root)
dotnet build ; dotnet test                   # unit test (ฟังก์ชันบริสุทธิ์)
python backend/tests/e2e/api_tests.py        # e2e + snapshot 219 เคส (ต้องรัน API ก่อน)
python backend/tests/e2e/api_tests.py --update   # บันทึก snapshot ใหม่เมื่อเปลี่ยนโดยตั้งใจ
dotnet run --project backend/src/Api         # http://localhost:5080, migrate + seed ครูตอน start
dotnet ef migrations add <Name> --project backend/src/Api -o Data/Migrations
cd frontend && bun install && bun run dev    # http://localhost:5173, proxy /api → 5080
cd frontend && bun run build ; bun run lint  # lint = oxlint
cd frontend && bunx --bun shadcn@latest add <component> -y
```
Secrets ใช้ `dotnet user-secrets` (`--project backend/src/Api`) ห้ามใส่ใน appsettings: `ConnectionStrings:Default`, `Google:ClientId`, `Google:ClientSecret`, `TEACHER_EMAILS` (คั่นด้วย comma · **ใช้ตอน bootstrap เท่านั้น** คนแรกในลิสต์เป็นเจ้าของ · ถ้ามีครูในตารางแล้วจะไม่ทำอะไร หลังจากนั้นเพิ่ม/ลบครูผ่านหน้า `/teacher/staff`)

## โครงสร้าง
```
backend/src/Api/Program.cs      DI, middleware, migrate + SeedFirstOwner ตอน start
backend/src/Api/Data/           Entities.cs (ทุก entity), AppDbContext.cs, Migrations/
backend/src/Api/Auth/           AuthSetup.cs (cookie/Google/policy/rate limit)
backend/src/Api/Common/         TeacherScope (กัน IDOR), ScoreWriter (บังคับเขียน audit), Validate, Problems, Limits
backend/src/Api/Import/         นำเข้า Excel: SheetReader (ด่านระดับไฟล์), Cells (อ่าน/ดักทีละช่อง), StudentSheetParser, ScoreSheetParser, ImportTemplates · ไม่แตะ DB
backend/src/Api/Endpoints/      *Endpoints.cs — extension MapXxx() ต่อ feature
backend/tests/Api.Tests/        xUnit v3 (ฟังก์ชันบริสุทธิ์ + parser/reader ของ import)
backend/tests/e2e/              api_tests.py + snapshot.txt (ยิง HTTP จริง)
frontend/src/lib/               api.ts (fetch + ApiError/NetworkError), auth.ts, keys.ts (route + query key), validate.ts, mascots.ts
frontend/src/components/        ใช้ร่วมกันหลายหน้า (AppLayout, ClassroomTabs, ScoreList, AppealStatusBadge, Modal, ConfirmDialog, Field, FormError, QueryState, PageHeader, Mascot, Avatar, Credit)
frontend/src/components/ui/     shadcn (โค้ดของเรา แก้ได้)
frontend/src/pages/             1 ไฟล์ต่อ 1 หน้า
```

## กฎของโปรเจกต์
- **Security (ห้ามลัด):**
  - endpoint ของนักเรียนต้องดึง studentId จาก cookie claim (`User.UserId()`) เท่านั้น ห้ามรับ id จาก URL หรือ body
  - endpoint ของครูต้องเช็คว่า Term เป็นของ `TeacherId` ที่ล็อกอินอยู่ — ใช้ `db.TermsOf(user)` / `db.FindClassroom(user, id)` จาก `Common/TeacherScope.cs` ห้ามเรียก `db.Terms` ตรง ๆ
  - ครูมี 2 ยศ: **Owner** เห็นข้อมูลของครูทุกคน + จัดการรายชื่อครูได้ · **Teacher** เห็นเฉพาะเทอมของตัวเอง (`TeacherScope` จัดการให้แล้ว)
  - endpoint ใน `/api/staff` ต้องเช็คยศจาก **DB ซ้ำ** ไม่เชื่อ claim อย่างเดียว เพราะ cookie อยู่ได้ 30 วัน คนที่เพิ่งโดนลดยศจะยังถือ claim เดิม
  - login endpoint และ `/api/public/*` ต้องมี `.RequireRateLimiting(AuthSetup.LoginLimit)`
  - นักเรียนล็อกอินด้วย **Google เท่านั้น** แล้วผูกกับรหัสนักเรียนครั้งแรก (ไม่มีรหัสส่วนตัวจากครู) · ครู unlink ได้
  - **ดูคะแนนด่วนไม่ต้องล็อกอิน** (`/scores`): ห้อง dropdown + เลขที่ dropdown + รหัสนักเรียนพิมพ์ · **ห้ามมี dropdown ชื่อ** (หน้าสาธารณะจะรั่วรายชื่อเด็ก) · ผิดช่องไหนตอบข้อความเดียวกัน · ไม่สร้าง cookie · ครูปิดได้รายภาคเรียน
  - API ตอบ JSON เท่านั้น (ใช้ SameSite=Lax + JSON กัน CSRF แทน antiforgery)
- **Import Excel = all-or-nothing:** preview ไม่เขียนอะไร · commit ตรวจซ้ำกับข้อมูลล่าสุด ผิดแม้จุดเดียวไม่บันทึกอะไรเลย · ผ่านหมดเขียนใน transaction เดียว + audit · รวบรวมจุดผิดทุกจุด (แถว/คอลัมน์) ในรอบเดียว · ช่องคะแนนว่าง = ไม่เปลี่ยนคะแนนเดิม · กฎดักใหม่ใส่ที่ `Import/Cells.cs` หรือ parser **พร้อมเทสต์ทุกครั้ง**
- **Error:** ใช้ `Results.Problem("ข้อความภาษาไทย", statusCode: …)` แล้ว `api()` ฝั่งเว็บจะเอา `detail` ไปแสดงให้ผู้ใช้เอง
- **คะแนนห้ามเกินคะแนนเต็ม:** เช็คในโค้ด เพราะ check constraint ข้ามตารางไม่ได้ · **แก้คะแนนทุกครั้งต้องเขียน `ScoreAudit`**
- **เวลา:** เก็บเป็น UTC (`DateTime.UtcNow`)
- **UI:** ข้อความภาษาไทย, ต้องใช้ได้ที่ความกว้าง 400px (มือถือ) และบน iPad
- **คำบนหน้าจอ (ใช้ให้ตรงกันทุกหน้า รวมข้อความ error จาก server):** สอบถามคะแนน / คำถาม (ไม่ใช้ "ท้วง") · สถานะ ฝั่งครู รอตอบ / ตอบแล้ว · ฝั่งนักเรียน รอครูตอบ / ครูตอบแล้ว · ทั้งคู่ เสร็จสิ้น · ผู้ดูแลระบบ / ครู (ไม่ใช้ เจ้าของ, ยศ) · เชื่อมบัญชี Google (ไม่ใช้ ผูก) · ไฟล์ตัวอย่าง (ไม่ใช้ template) · ย้ายออกจากห้อง (ไม่ใช้ เอาออก) · เข้าสู่ระบบ (ไม่ใช้ เซสชัน)
- **Component:** แยกเป็นไฟล์เมื่อใช้ ≥ 2 ที่
- **ห้าม commit:** secret, `.env`, ไฟล์ Excel/CSV ที่มีข้อมูลนักเรียนจริง (มีใน .gitignore แล้ว)
- **Commit:** แยกตาม feature/layer ให้ย้อนแก้ง่าย (conventional commits: `feat(api)`, `feat(web)`, `fix`, `chore`)
- ไม่ทำใน v1: LINE OA, realtime, น้ำหนักคะแนน/เกรด, หลายโรงเรียน

## Gotchas
- `shadcn add` บางครั้งสร้าง `import { cn } from "cn"` ต้องแก้เป็น `"@/lib/utils"` ทุกครั้งหลัง add
- `bun` อยู่ที่ `~/.bun/bin` ถ้า shell ยังหาไม่เจอให้เพิ่มเข้า PATH
- ปุ่ม Google ต้องเป็น `<a href="/api/auth/google">` ใช้ fetch ไม่ได้ เพราะ OAuth ต้อง redirect ทั้งหน้า
- `/api/auth/dev-login?email=` มีเฉพาะ Development ใช้ทดสอบเป็นครูโดยไม่ต้องตั้ง Google
- API สั่ง `Migrate()` ตอน start ถ้ายังไม่ได้ตั้ง connection string แอปจะ crash ทันที
- **migration เพิ่มคอลัมน์ non-null ต้องเช็ค `defaultValue` ทุกครั้ง** EF ใส่ค่า default ของ CLR (`""`, `false`) ไม่ใช่ค่าที่ตั้งใน C# · เคยพลาดแล้ว 2 ครั้ง (Role เป็น `""`, PublicScores เป็น `false`)
- `Api.csproj` ตั้ง `UseAppHost=false` เพราะ Smart App Control บล็อก `Api.exe` ที่ไม่ได้เซ็น · ห้ามเอาออก
- บางครั้ง Smart App Control บล็อก `Api.dll` ที่เพิ่ง build (`0x800711C7`) ให้ build ใหม่ให้ได้ hash ใหม่แล้วรัน dll ตรง: `dotnet build --no-incremental -p:Deterministic=false` แล้ว `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet bin/Debug/net10.0/Api.dll` (จากโฟลเดอร์ `backend/src/Api`)
- `dotnet build` ขึ้น `MSB3027 file is locked by .NET Host` = API ตัวเก่ายังรันอยู่ ปิดก่อนแล้ว build ใหม่
- เทสต์ API ที่มีภาษาไทยห้ามใช้ curl บนเครื่องนี้ (console แปลงเป็น `?`) ใช้ `backend/tests/e2e/api_tests.py`
- e2e รันซ้ำติดกันต้องเว้น ~65 วินาที เพราะ `/api/public/scores` ใช้ rate limit ร่วมกับ login
- เครื่องนี้ไม่มี openpyxl · e2e สร้าง .xlsx เองด้วย `zipfile` (ฟังก์ชัน `xlsx()` ใน `api_tests.py`)
- เทสต์ต้องเป็น **xUnit v3** (`<OutputType>Exe</OutputType>`) + `global.json` ตั้ง `test.runner = "Microsoft.Testing.Platform"` — **ห้ามย้อนกลับไป xunit v2 + Microsoft.NET.Test.Sdk** เพราะ Smart App Control ของ Windows บล็อก `testhost` ตอนโหลด dll ด้วย reflection (`FileLoadException 0x800711C7`) v3 คอมไพล์เป็น .exe รันตรงจึงผ่าน
- ภาพคาปิบาร่าใน `frontend/public/capybara/` มาจาก Freepik license ฟรี **ต้องคงเครดิต** (`components/Credit.tsx`) ไว้ ถ้าเอาออกต้องอัปเป็น Premium ก่อน

## สถานะ (อัปเดตทุกครั้งที่จบ milestone)
- [x] M1 Scaffold
- [x] M2 DB schema + migration (รันบน Neon แล้ว)
- [x] M3 Auth: Google OAuth + claim ด้วยรหัสนักเรียน + ครู 2 ยศ (Owner/Teacher) · ทดสอบ runtime แล้ว
- [x] M4 หน้าครู: ภาคเรียน/ห้อง/นักเรียน/รายการ + ตารางคะแนน + audit + optimistic concurrency · เมนู responsive · ดูคะแนนด่วนไม่ต้องล็อกอิน
- [x] M5 Import Excel แยก 2 แบบ (รายชื่อนักเรียน / คะแนน) · template ของเราเอง + preview + all-or-nothing · **ครูจะส่ง template จริงมา ได้แล้วค่อยปรับ parser/template ให้ตรง**
- [x] M6 หน้านักเรียน: คะแนนของฉัน (`/api/me/scores` ดึง studentId จาก cookie เท่านั้น)
- [x] M7 สอบถามคะแนน (โค้ดใช้ชื่อ Appeal): นักเรียนกด "สอบถาม" ข้างรายการ → thread คุยกับครู · badge ข้อความใหม่บนเมนู (poll ทุก 1 นาที ไม่มี realtime) · ปิดแล้วตอบต่อไม่ได้ เปิดเรื่องใหม่ได้
- [ ] M8 Dockerfile/compose/.env.example + Render + backup (ผู้ใช้ขอพักไว้ก่อน)

**ต่อไป:** ปรับ import ตาม template จริงของครูเมื่อได้ไฟล์ → M8 deploy เมื่อผู้ใช้สั่ง
