# CLAUDE.md

**Math Aj.Gift** — ระบบคะแนนวิชาคณิตของโรงเรียน: ครูนำเข้าคะแนนจากไฟล์ Excel ของตัวเอง ส่วนนักเรียนเข้ามาดูคะแนนของตัวเองและสอบถามครูได้
ผู้ใช้ 100-300 คน ครู 1 คน (อนาคตอาจเพิ่ม) · **ขึ้นใช้งานจริงแล้วที่ https://krugift-chiangmai.onrender.com** · ดูแลคนเดียว · deploy ฟรี
ชื่อที่ผู้ใช้เห็นอยู่ที่ `frontend/src/lib/app.ts` ที่เดียว (ยกเว้น `<title>` ใน `index.html` ที่ import ไม่ได้) · ชื่อ repo กับโดเมนยังเป็น krugift-chiangmai ตามเดิม

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
dotnet build -c Release -p:Deterministic=false ; dotnet test -c Release   # unit test (Release เพราะ Smart App Control บล็อก Debug)
python backend/tests/e2e/api_tests.py        # e2e + snapshot 272 เคส (ต้องรัน API ก่อน)
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
backend/src/Api/Import/         อ่านไฟล์ครู: BookReader (อ่านทุกชีท + เติมช่อง merge) → TeacherBookParser (หาหัวตาราง/จับคู่คอลัมน์/ปัดคะแนน) · SheetReader + Cells เป็นตัวช่วยอ่านระดับไฟล์และระดับช่อง · ImportTemplates สร้างไฟล์ "ส่งออก" · ไม่แตะ DB
backend/src/Api/Endpoints/      *Endpoints.cs — extension MapXxx() ต่อ feature
backend/tests/Api.Tests/        xUnit v3 (ฟังก์ชันบริสุทธิ์ + parser/reader ของ import)
backend/tests/e2e/              api_tests.py + snapshot.txt (ยิง HTTP จริง)
frontend/src/lib/               api.ts (fetch + ApiError/NetworkError), auth.ts, keys.ts (route + query key), validate.ts, mascots.ts, app.ts (ชื่อแอป), useSubmit.ts, useTermName.ts
frontend/src/components/        ใช้ร่วมกันหลายหน้า (AppLayout, TermTabs, ClassroomTabs, TabToolbar, Rows, Meta, ScoreList, ExportButton, AppealStatusBadge, Modal, ConfirmDialog, Field, FormError, QueryState, PageHeader, Mascot, Avatar, Credit)
frontend/src/components/ui/     shadcn (โค้ดของเรา แก้ได้)
frontend/src/pages/             1 ไฟล์ต่อ 1 หน้า
```

## กฎของโปรเจกต์
- **Security (ห้ามลัด):**
  - endpoint ของนักเรียนต้องดึง studentId จาก cookie claim (`User.UserId()`) เท่านั้น ห้ามรับ id จาก URL หรือ body
  - endpoint ของครูต้องเช็คว่า Term เป็นของ `TeacherId` ที่ล็อกอินอยู่ — ใช้ `db.TermsOf(user)` / `db.FindClassroom(user, id)` จาก `Common/TeacherScope.cs` ห้ามเรียก `db.Terms` ตรง ๆ
  - ครูมี 2 ยศ: **Owner** เห็นข้อมูลของครูทุกคน + จัดการรายชื่อครูได้ · **Teacher** เห็นเฉพาะเทอมของตัวเอง (`TeacherScope` จัดการให้แล้ว)
  - endpoint ใน `/api/staff` ต้องเช็คยศจาก **DB ซ้ำ** ไม่เชื่อ claim อย่างเดียว เพราะ cookie อยู่ได้ 30 วัน คนที่เพิ่งโดนลดยศจะยังถือ claim เดิม
  - login endpoint ต้องมี `.RequireRateLimiting(AuthSetup.LoginLimit)` (10 ครั้ง/นาที/IP)
  - `/api/public/*` ใช้ `AuthSetup.PublicLimit` (600 ครั้ง/นาที/IP กัน flood) **ห้ามใช้ LoginLimit** เพราะทั้งโรงเรียนออกเน็ต IP เดียวกัน เด็กทั้งห้องเปิดพร้อมกันจะโดนบล็อกทั้งที่กรอกถูก · การกันคนเดารหัสอยู่ที่ `Common/LookupLockout.cs` ซึ่ง **นับเฉพาะครั้งที่กรอกผิด** (ผิดครบ 10 ครั้ง/นาที/IP → 429 · กรอกถูกล้างยอดทิ้ง)
  - นักเรียนล็อกอินด้วย **Google เท่านั้น** แล้วผูกกับรหัสนักเรียนครั้งแรก (ไม่มีรหัสส่วนตัวจากครู) · ครู unlink ได้
  - **ดูคะแนนด่วนไม่ต้องล็อกอิน** (`/scores`): ห้อง dropdown + เลขที่ dropdown + รหัสนักเรียนพิมพ์ · **ห้ามมี dropdown ชื่อ** (หน้าสาธารณะจะรั่วรายชื่อเด็ก) · ผิดช่องไหนตอบข้อความเดียวกัน · ไม่สร้าง cookie · ครูปิดได้รายภาคเรียน
  - API ตอบ JSON เท่านั้น (ใช้ SameSite=Lax + JSON กัน CSRF แทน antiforgery)
- **นำเข้ามีทางเดียว = ไฟล์ครูทั้งภาคเรียน** (ตัดสินใจ 2026-09-20): ครูมีไฟล์แบบเดียวคือไฟล์ระดับภาคเรียน/ชั้นปี ชีทละห้อง · การนำเข้า Excel ระดับห้อง (รายชื่อ/คะแนน) กับ template ของเราเองถูกถอดออกแล้ว · **ส่งออก** เหลือปุ่มเดียว `GET /api/classrooms/{id}/export` ได้ไฟล์ 2 ชีท (นักเรียน + คะแนน) ขาออกอย่างเดียว นำเข้ากลับไม่ได้
- **ครูตั้งชื่อรายการเองได้ แล้วนำเข้าซ้ำไม่เกิดของซ้ำ:** `AssessmentItem.SourceKey` เก็บชื่อตามหัวตารางในไฟล์ (เช่น "จำนวนเต็ม สอบ 1") ส่วน `Name` คือชื่อที่ครูตั้งเอง (เช่น "สอบ 1 (จำนวนเต็ม)") · ตอนนำเข้าจับคู่ด้วย `SourceKey` ก่อน ถ้าว่าง (ข้อมูลเก่า) ค่อย fallback ไปที่ `Name` แล้วเติม `SourceKey` ให้ · **นำเข้าไม่เขียนทับ `Name`** แต่ยังอัปเดตคะแนนเต็มกับธง TeacherOnly ตามไฟล์
- **รายการคะแนนจัดการที่ระดับภาคเรียนเท่านั้น** (`/teacher/terms/:termId/items` · `Endpoints/TermItemEndpoints.cs`): เพิ่ม / แก้ชื่อ / แก้คะแนนเต็ม / ซ่อน-แสดง / ลบ / ปัดคะแนน ทำทีเดียวทุกห้อง · รวมรายการที่ชื่อตรงกันของทุกห้องเป็นแถวเดียว ค่าที่ไม่ตรงกันทุกห้องส่ง `null` (เว็บแสดงว่าคละกัน) · ระดับห้องเหลือแค่ดูรายการ (GET) กับลบรายการเดี่ยว · แท็บของห้องเหลือ นักเรียน / ตารางคะแนน
- **นำเข้าไฟล์ครูจริง (`/api/terms/{id}/import/book`)**: ไฟล์เดียวหลายชีท ชีทละห้อง · หาหัวตารางเอง (แถวที่มี เลขที่ + รหัสนักเรียน) ไม่ฟิกซ์เลขแถว · ชื่อห้องอ่านจากหัวเรื่อง "ชั้นมัธยมศึกษาปีที่ 1/1" → ม.1/1 · ชีทที่ไม่มีหัวตาราง (เช่น คะแนนรวม) ข้ามเงียบ ไม่ใช่จุดผิด · หยุดอ่านที่ท้ายตาราง เพราะครูเขียนหมายเหตุไว้ใต้รายชื่อ · คะแนนที่เป็นสูตรปัดเหลือ 2 ตำแหน่งแบบปัดครึ่งขึ้นให้ตรงกับที่ Excel แสดง (8.125 → 8.13) · **ถ้าค่าจริงเกินคะแนนเต็มที่หัวเขียนไว้ ให้ตัดลงมาเท่าคะแนนเต็ม** (ครูสั่ง 2026-09-20 ทุกห้องจะได้มีคะแนนเต็มเท่ากัน) · **คอลัมน์หารมีหลายอันติดกัน (H หาร 45 · I หาร 40) ใช้อันสุดท้าย** (ครูยืนยัน 2026-09-20) · คอลัมน์ที่มีหัวของตัวเอง (เอกสาร แบบฝึกหัด) ไม่ใช่คอลัมน์หาร แต่หัวที่ merge คร่อมทั้งสองคอลัมน์ (Midterm M4:N5) ยังนับเป็นคู่เดียวกัน · นำเข้าซ้ำได้ (จับคู่จากรหัสนักเรียนและชื่อรายการ) และนำเข้าคนละภาคเรียนได้
- **นำเข้าไฟล์ครู = all-or-nothing:** preview ไม่เขียนอะไร · commit ตรวจซ้ำกับข้อมูลล่าสุด ผิดแม้จุดเดียวไม่บันทึกอะไรเลย · ผ่านหมดเขียนใน transaction เดียว + audit · รวบรวมจุดผิดทุกจุด (แถว/คอลัมน์) ในรอบเดียว · ช่องคะแนนว่าง = ไม่เปลี่ยนคะแนนเดิม · กฎดักใหม่ใส่ที่ `Import/Cells.cs` หรือ parser **พร้อมเทสต์ทุกครั้ง**
- **ซ่อนรายการคะแนนรายอัน:** `AssessmentItem.TeacherOnly` = เห็นเฉพาะครู · ครูสลับด้วยสวิตช์ "นักเรียนเห็น" ในหน้ารายการคะแนนของภาคเรียน (`PATCH /api/terms/{id}/items/visibility` ส่ง `name` + `visible` · มีผลทุกห้องพร้อมกัน) · รายการที่ซ่อนไม่โผล่ใน `/api/me/scores` และ `/api/public/scores` และนักเรียนสอบถามไม่ได้ · คะแนนรวมนับเฉพาะรายการที่เห็นและมีคะแนนแล้ว · คะแนนดิบจากไฟล์ครูถูกตั้งซ่อนให้อัตโนมัติตอนนำเข้า
- **1 "ภาคเรียน" ในระบบ = 1 วิชา + 1 ชั้น + 1 ภาคเรียนจริง** (ตกลงกับผู้ใช้ 2026-09-20): ชื่อที่ครูตั้งสะท้อนทั้งสามอย่าง เช่น "คณิตศาสตร์เพิ่มเติม 1 (ภาคเรียนที่ 1/2569)" · อนาคตครูอาจสอนหลายวิชา (คณิตพื้นฐาน, คณิตเพิ่มเติม 2) = สร้างภาคเรียนเพิ่มอีกอัน ไม่ต้องแก้โครงสร้าง · ระบบมีแค่ ภาคเรียน → ห้อง ไม่มีระดับ "ชั้น" หรือ "วิชา" ของตัวเอง · ไฟล์ครู 1 ไฟล์ = 1 ชั้น (ม.1 ทั้ง 8 ห้อง) พอขึ้น ม.2 ให้สร้างภาคเรียนใหม่แล้วนำเข้าอีกไฟล์ · คำสั่งระดับภาคเรียน (ซ่อน/แสดง, ปัดคะแนน, ดูคะแนนด่วน) จึงมีผลเท่ากับทั้งชั้นพอดี · **ถ้าวันหนึ่งมีหลายชั้นปนในภาคเรียนเดียว ต้องเพิ่มตัวกรองห้องในหน้า `/teacher/terms/:termId/items` ก่อน** ไม่งั้นครูจะเผลอสั่งข้ามชั้น
- **ปัดคะแนนเป็นจำนวนเต็ม** (`Common/Rounding.cs`): ปัดครึ่งขึ้น 7.33→7, 7.5→8 และห้ามเกินคะแนนเต็ม (ถ้าปัดขึ้นแล้วเกิน ใช้ `Math.Floor(คะแนนเต็ม)`) · ปัดแล้วเก็บค่าที่ปัดลง DB ตามที่ครูเลือก · ปัดเฉพาะรายการที่นักเรียนเห็น คะแนนดิบต้องคงค่าตามไฟล์เสมอ · ทำได้ 2 ทาง: ติ๊กตอนนำเข้าไฟล์ครู (`round=true`) หรือกดปุ่มในหน้ารายการคะแนนทั้งภาคเรียน (`GET/POST /api/terms/{id}/round-scores` · เขียน `ScoreAudit` ทุกช่องใน transaction เดียว)
- **Error:** ใช้ `Results.Problem("ข้อความภาษาไทย", statusCode: …)` แล้ว `api()` ฝั่งเว็บจะเอา `detail` ไปแสดงให้ผู้ใช้เอง
- **คะแนนห้ามเกินคะแนนเต็ม:** เช็คในโค้ด เพราะ check constraint ข้ามตารางไม่ได้ · **แก้คะแนนทุกครั้งต้องเขียน `ScoreAudit`**
- **กุญแจเข้ารหัส cookie เก็บใน DB** (`AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("krugift")` ใน `Program.cs` · ตาราง `DataProtectionKeys`): ค่า default ของ ASP.NET เก็บเป็นไฟล์ใน container ซึ่ง Render สร้างใหม่ทุก deploy ทำให้ cookie ของทุกคนถอดรหัสไม่ออกและหลุดออกจากระบบพร้อมกัน · **ห้ามเปลี่ยน `SetApplicationName`** เพราะ ASP.NET จะมองว่าเป็นคนละแอปแล้วไม่ใช้กุญแจเดิม · คอลัมน์ `Xml` ต้องเป็น `text` (convention ของเราจำกัด string ไว้ 200 ตัว ต้อง `SetMaxLength(null)` ทับ)
- **เวลา:** เก็บเป็น UTC (`DateTime.UtcNow`)
- **UI:** ข้อความภาษาไทย, ต้องใช้ได้ที่ความกว้าง 400px (มือถือ) และบน iPad
- **คำบนหน้าจอ (ใช้ให้ตรงกันทุกหน้า รวมข้อความ error จาก server):** สอบถามคะแนน / คำถาม (ไม่ใช้ "ท้วง") · สถานะ ฝั่งครู รอตอบ / ตอบแล้ว · ฝั่งนักเรียน รอครูตอบ / ครูตอบแล้ว · ทั้งคู่ เสร็จสิ้น · ผู้ดูแลระบบ / ครู (ไม่ใช้ เจ้าของ, ยศ) · เชื่อมบัญชี Google (ไม่ใช้ ผูก) · นำเข้า / ส่งออก (ไม่ใช้ นำออก) · ย้ายออกจากห้อง (ไม่ใช้ เอาออก) · เข้าสู่ระบบ (ไม่ใช้ เซสชัน)
- **Component:** แยกเป็นไฟล์เมื่อใช้ ≥ 2 ที่
- **ห้าม commit:** secret, `.env`, ไฟล์ Excel/CSV ที่มีข้อมูลนักเรียนจริง (มีใน .gitignore แล้ว)
- **ลบภาคเรียนทั้งก้อน** (`POST /api/terms/{id}/delete-all`): ทางออกเมื่อนำเข้าผิด · ต้องพิมพ์ชื่อภาคเรียนให้ตรงถึงจะลบ · ลบ appeal → score/audit → item → enrollment → classroom → term ใน transaction เดียว (ไม่ลบตัวนักเรียน)
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
- Smart App Control บล็อก `Api.dll` แบบ Debug บ่อยขึ้นเรื่อย ๆ (`0x800711C7`) จน `dotnet test` รัน 0 เทสต์และ API ไม่ขึ้น · **ทางที่ได้ผลชัวร์สุดคือใช้ Release**: `dotnet build -c Release -p:Deterministic=false` · `dotnet test -c Release` · รัน API ด้วย `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet bin/Release/net10.0/Api.dll` (จากโฟลเดอร์ `backend/src/Api`)
- `dotnet build` ขึ้น `MSB3027 file is locked by .NET Host` = API ตัวเก่ายังรันอยู่ ปิดก่อนแล้ว build ใหม่
- เทสต์ API ที่มีภาษาไทยห้ามใช้ curl บนเครื่องนี้ (console แปลงเป็น `?`) ใช้ `backend/tests/e2e/api_tests.py`
- e2e รันซ้ำติดกันต้องเว้น ~65 วินาที เพราะ **endpoint ล็อกอิน** ยังจำกัด 10 ครั้ง/นาที/IP (เคสฝั่ง `/api/public/*` ไม่ติดแล้วหลังเปลี่ยนไปใช้ PublicLimit) · ถ้า e2e ตายกลางคัน ภาคเรียน "E2E ..." จะค้างใน DB ทำให้รอบถัดไปสร้างชื่อซ้ำไม่ได้ ต้อง `delete-all` ทิ้งก่อน
- เครื่องนี้ไม่มี openpyxl · e2e สร้าง .xlsx เองด้วย `zipfile` (ฟังก์ชัน `xlsx()` ใน `api_tests.py`)
- เทสต์ต้องเป็น **xUnit v3** (`<OutputType>Exe</OutputType>`) + `global.json` ตั้ง `test.runner = "Microsoft.Testing.Platform"` — **ห้ามย้อนกลับไป xunit v2 + Microsoft.NET.Test.Sdk** เพราะ Smart App Control ของ Windows บล็อก `testhost` ตอนโหลด dll ด้วย reflection (`FileLoadException 0x800711C7`) v3 คอมไพล์เป็น .exe รันตรงจึงผ่าน
- ภาพคาปิบาร่าใน `frontend/public/capybara/` มาจาก Freepik license ฟรี **ต้องคงเครดิต** (`components/Credit.tsx`) ไว้ ถ้าเอาออกต้องอัปเป็น Premium ก่อน

## สถานะ (อัปเดตทุกครั้งที่จบ milestone)
- [x] M1 Scaffold
- [x] M2 DB schema + migration (รันบน Neon แล้ว)
- [x] M3 Auth: Google OAuth + claim ด้วยรหัสนักเรียน + ครู 2 ยศ (Owner/Teacher) · ทดสอบ runtime แล้ว
- [x] M4 หน้าครู: ภาคเรียน/ห้อง/นักเรียน/รายการ + ตารางคะแนน + audit + optimistic concurrency · เมนู responsive · ดูคะแนนด่วนไม่ต้องล็อกอิน
- [x] M5 นำเข้าไฟล์ครูจริง (8 ชีท ชีทละห้อง) preview + all-or-nothing ที่ `/teacher/terms/:termId/import` · การนำเข้าแบบ template ระดับห้องถูกถอดออกแล้ว (2026-09-20)
- [x] M6 หน้านักเรียน: คะแนนของฉัน (`/api/me/scores` ดึง studentId จาก cookie เท่านั้น)
- [x] M7 สอบถามคะแนน (โค้ดใช้ชื่อ Appeal): นักเรียนกด "สอบถาม" ข้างรายการ → thread คุยกับครู · badge ข้อความใหม่บนเมนู (poll ทุก 1 นาที ไม่มี realtime) · ปิดแล้วตอบต่อไม่ได้ เปิดเรื่องใหม่ได้
- [x] M8 Dockerfile (3 stage: Bun build เว็บ → publish API → aspnet alpine) + compose + `.env.example` · build และรัน container ต่อ Neon ผ่านแล้ว · วิธีขึ้น Render กับการสำรองข้อมูลอยู่ใน `README.md`
- [x] M9 ขึ้น production จริงบน Render (2026-09-20): Google OAuth ใช้ได้จริงทั้งฝั่งครูและนักเรียน (redirect URI `https://krugift-chiangmai.onrender.com/api/signin-google`) · กุญแจเข้ารหัส cookie ย้ายไปเก็บใน DB แล้ว · หน้า `/privacy` ตามที่ Google ขอ

## ข้อมูลจริงในระบบ (ณ 2026-09-20)
1 ภาคเรียน "คณิตศาสตร์เพิ่มเติม 1 (ภาคเรียนที่ 1/2569)" · 8 ห้อง (ม.1/1-ม.1/8) · นักเรียน 353 คน · 8 รายการคะแนน (ที่นักเรียนเห็น 4 + คะแนนดิบซ่อน 4) · คะแนน 2,790 ช่อง **ปัดเป็นจำนวนเต็มแล้ว**
ตรวจแล้วว่าตรงกับชีท "คะแนนรวม" ของครูครบ 1,047 ช่อง และยิงหน้าดูคะแนนด่วนทีละคนครบ 177 คน (ห้อง 1-4) ผ่านหมด
ยังไม่มีข้อมูล: เศษส่วนฯ สอบ 2 กับ Final (ครูยังไม่ได้สอบ) · นำเข้าไฟล์ใหม่เมื่อไหร่ระบบจะสร้างรายการเพิ่มให้เอง

## วัดประสิทธิภาพแล้ว (2026-09-20)
Render ฟรี + Neon ฟรี รับได้สบาย: ยิง 353 คนพร้อมกันผ่าน `/api/public/scores` ผ่านหมดใน ~1 วินาที (~400 คำขอ/วินาที) · production ตอบใน ~0.3 วินาทีที่ 20 คนพร้อมกัน

**ต่อไป:** ยังไม่มีงานค้าง · ที่อาจทำต่อเมื่อผู้ใช้สั่ง — สำรองข้อมูลอัตโนมัติ (ตอนนี้พึ่ง PITR ของ Neon), ตัวกรองห้องในหน้ารายการคะแนนถ้าวันหนึ่งมีหลายชั้นในภาคเรียนเดียว
