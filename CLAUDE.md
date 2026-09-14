# CLAUDE.md

ระบบคะแนนวิชาคณิตของโรงเรียน: ครูกรอกหรือ import คะแนนจาก Excel ส่วนนักเรียนเข้ามาดูคะแนนของตัวเองและท้วงคะแนนได้
ผู้ใช้ 100-300 คน ครู 1 คน (อนาคตอาจเพิ่ม) · ใช้งานจริง · ดูแลคนเดียว · deploy ฟรี

- แผนเต็ม, schema, API, ลำดับ milestone: **`docs/PLAN.md`** (อ่านก่อนเริ่มงานใหม่)
- วิธีรันทั้งแบบมีและไม่มี Docker: `README.md`

## Stack
- **API:** .NET 10 Minimal API + EF Core 10 (Npgsql) · `src/Api`
- **Tests:** xUnit · `src/Api.Tests`
- **Web:** React 19 + Vite 8 + TypeScript + Tailwind v4 + shadcn/ui (style base-nova ใช้ Base UI) + TanStack Query + react-router v8 · `web/` · **ใช้ Bun ไม่ใช้ npm**
- **DB:** PostgreSQL บน Neon (เลือก Neon แทน Supabase เพราะ Supabase ฟรีจะ pause project ถ้าไม่มีคนใช้ 7 วัน ซึ่งเจอแน่ช่วงปิดเทอม)
- **Deploy:** Docker image เดียว (build web แล้ว copy ไปไว้ใน `wwwroot` ของ API) → Render ฟรี

## คำสั่ง
```bash
dotnet tool restore                          # dotnet-ef (local tool, ต้องรันจาก root)
dotnet build ; dotnet test
dotnet run --project src/Api                 # http://localhost:5080, migrate + seed ครูตอน start
dotnet ef migrations add <Name> --project src/Api -o Data/Migrations
cd web && bun install && bun run dev         # http://localhost:5173, proxy /api → 5080
cd web && bun run build ; bun run lint       # lint = oxlint
cd web && bunx --bun shadcn@latest add <component> -y
```
Secrets ใช้ `dotnet user-secrets` (`--project src/Api`) ห้ามใส่ใน appsettings: `ConnectionStrings:Default`, `Google:ClientId`, `Google:ClientSecret`, `TEACHER_EMAILS` (คั่นด้วย comma)

## โครงสร้าง
```
src/Api/Program.cs              DI, middleware, migrate + SeedTeachers ตอน start
src/Api/Data/                   Entities.cs (ทุก entity), AppDbContext.cs, Migrations/
src/Api/Auth/                   AccessCode.cs (รหัสส่วนตัว + lockout), AuthSetup.cs (cookie/Google/policy/rate limit)
src/Api/Endpoints/              *Endpoints.cs — extension MapXxx() ต่อ feature
web/src/lib/                    api.ts (fetch + ApiError), auth.ts (useMe, homeOf)
web/src/components/             ใช้ร่วมกันหลายหน้า (AppLayout, RequireRole, AuthCard, CodeForm)
web/src/components/ui/          shadcn (โค้ดของเรา แก้ได้)
web/src/pages/                  1 ไฟล์ต่อ 1 หน้า
```

## กฎของโปรเจกต์
- **Security (ห้ามลัด):**
  - endpoint ของนักเรียนต้องดึง studentId จาก cookie claim (`User.UserId()`) เท่านั้น ห้ามรับ id จาก URL หรือ body
  - endpoint ของครูต้องเช็คว่า Term เป็นของ `TeacherId` ที่ล็อกอินอยู่
  - login endpoint ต้องมี `.RequireRateLimiting(AuthSetup.LoginLimit)`
  - API ตอบ JSON เท่านั้น (ใช้ SameSite=Lax + JSON กัน CSRF แทน antiforgery)
- **Error:** ใช้ `Results.Problem("ข้อความภาษาไทย", statusCode: …)` แล้ว `api()` ฝั่งเว็บจะเอา `detail` ไปแสดงให้ผู้ใช้เอง
- **คะแนนห้ามเกินคะแนนเต็ม:** เช็คในโค้ด เพราะ check constraint ข้ามตารางไม่ได้ · **แก้คะแนนทุกครั้งต้องเขียน `ScoreAudit`**
- **เวลา:** เก็บเป็น UTC (`DateTime.UtcNow`)
- **UI:** ข้อความภาษาไทย, ต้องใช้ได้ที่ความกว้าง 400px (มือถือ) และบน iPad
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

## สถานะ (อัปเดตทุกครั้งที่จบ milestone)
- [x] M1 Scaffold
- [x] M2 DB schema + migration แรก (ยังไม่ได้รันกับ DB จริง)
- [x] M3 Auth backend + หน้า login/claim (build/test ผ่าน **ยังไม่ได้ทดสอบ runtime** · รอ Neon)
- [ ] M4 หน้าครู: เทอม/ห้อง/รายการ/นักเรียน + ตารางคะแนน + audit
- [ ] M5 Import Excel + template + ใบแจกรหัส
- [ ] M6 หน้านักเรียน
- [ ] M7 ท้วงคะแนน
- [ ] M8 Dockerfile/compose/.env.example + Render + backup

**ต่อไป:** เมื่อตั้ง Neon connection string แล้ว → รัน API → ทดสอบ login ทุก flow (dev-login ครู, code-login นักเรียน, ผิด 5 ครั้งโดนล็อก, 401/403) → เริ่ม M4
