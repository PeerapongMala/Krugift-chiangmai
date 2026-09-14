# แผน: ระบบคะแนนคณิต Krugift-chiangmai

## Context
เพื่อน (ครู) อยากให้ครูกรอกหรือ import คะแนนจาก Excel ได้ และให้เด็กเข้ามาดูคะแนนของตัวเอง พร้อมท้วงคะแนนได้
- ผู้ใช้ 100-300 คน ห้องละ ~40 คน ตอนนี้มีครู 1 คน (อนาคตอาจเพิ่ม)
- ทำให้ฟรี ใช้งานจริง ทำคนเดียว
- Greenfield: repo `github.com/PeerapongMala/Krugift-chiangmai` ยังว่าง
- เครื่องมีแล้ว: .NET SDK 10.0.400, Node 24, git — **ไม่มี Docker**

## Stack (ตกลงแล้ว)
.NET 10 Minimal API + EF Core (Npgsql, `dotnet-ef` เป็น local tool 10.0.12 ใน `dotnet-tools.json` ที่ root ของ repo (ติดตั้งแล้ว)) · **Bun** (package manager + รัน script ฝั่ง web: `bun install`, `bun run dev`, `bunx shadcn`, Dockerfile stage web ใช้ `oven/bun`) · React + Vite + TS + Tailwind + shadcn/ui + TanStack Query + react-router · PostgreSQL (Neon ฟรี) · ClosedXML · Google OAuth (cookie auth) · Docker image เดียว → Render ฟรี

## Requirement checklist (กันตกหล่น)
- [ ] เว็บ responsive: ครูใช้คอม/iPad, เด็กใช้คอม/มือถือ/iPad
- [ ] ครูล็อกอิน Google (อีเมลต้องอยู่ใน whitelist)
- [ ] เทอม → ห้อง → รายการคะแนน (ชื่อ + คะแนนเต็ม) → คะแนน แยกตามเทอม
- [ ] ครูเพิ่ม/แก้คะแนนทีละช่อง, เพิ่ม/แก้/ลบรายการ, เพิ่ม/แก้นักเรียน
- [ ] Import Excel ทีละห้องตาม template (มีปุ่มดาวน์โหลด) → preview + แสดงจุดผิด → ยืนยัน
- [ ] ไม่มีน้ำหนักคะแนนหรือเกรด แสดง ได้/เต็ม + ผลรวม
- [ ] ระบุเด็กด้วยรหัสนักเรียน (unique ทั้งโรงเรียน ใช้ต่อข้ามเทอม)
- [ ] เด็ก: ครั้งแรกล็อกอิน Google + กรอก รหัสนักเรียน + รหัสส่วนตัว → ผูกบัญชี / ไม่มี Gmail ใช้ รหัสนักเรียน + รหัสส่วนตัว ล็อกอินได้เลย
- [ ] ครูรีเซ็ตรหัสส่วนตัวได้ + พิมพ์ใบแจกรหัสได้
- [ ] เด็กเห็นเฉพาะคะแนนของตัวเอง (server ดึง studentId จาก cookie claim เท่านั้น)
- [ ] ท้วงคะแนนเป็นรายการ → ครูตอบ → badge บอกว่ามีข้อความใหม่ทั้งสองฝั่ง
- [ ] ประวัติการแก้คะแนน (audit log)
- [ ] Security: rate limit ที่หน้า login, lock บัญชีหลังใส่รหัสผิดหลายครั้ง, จำกัดขนาดไฟล์ upload, HTTPS, cookie HttpOnly + SameSite, antiforgery
- [ ] เผื่อครูหลายคนด้วย `TeacherId` บน Term
- [ ] Backup DB

**ไม่ทำใน v1:** LINE OA, realtime (ใช้วิธี refresh หรือ refetch แทน), น้ำหนักคะแนน/เกรด, หลายโรงเรียน, ปรับตัวอ่าน Excel ให้ตรงไฟล์จริงของเพื่อน (ทำเมื่อได้ไฟล์ตัวอย่าง)

## โครง repo — `D:\Peerapong\Projects\Krugift-chiangmai`
```
backend/src/Api/     ASP.NET Core (Program.cs, Data/, Endpoints/, Import/, Auth/)
backend/tests/       xUnit — เทสต์ import parser + การสร้าง/ตรวจรหัส
frontend/            Vite React TS (components/ui = shadcn, pages/, components/)
Dockerfile           multi-stage: build frontend → copy ไป Api/wwwroot → dotnet publish
docker-compose.yml   postgres:18 + app (สำหรับเครื่องที่มี Docker)
.env.example         ตัวอย่าง env ที่ต้องตั้ง (ไม่มีค่าจริง)
.gitignore  README.md  (README อธิบายวิธีรันทั้ง 2 แบบ)
```

### รันได้ 2 แบบจากโค้ดชุดเดียวกัน
โค้ดไม่รู้ว่ารันใน Docker หรือไม่ ทุกค่าที่ต่างกันอ่านจาก env / `appsettings` (`ConnectionStrings__Default`, `Google__ClientId`, `Google__ClientSecret`, `TEACHER_EMAILS`)

| | ไม่มี Docker (เครื่องนี้) | มี Docker (เครื่องอื่น) |
|---|---|---|
| DB | Neon dev branch (หรือลง PostgreSQL บน Windows ก็ได้) | `postgres` ใน compose |
| รัน | `dotnet run --project backend/src/Api` + `cd frontend && bun run dev` (Vite proxy `/api`) | `docker compose up --build` → เปิดที่ `localhost:8080` |
| Hot reload | ได้ทั้ง 2 ฝั่ง | ไม่มี (compose ใช้ลองแบบใกล้ production) · อยากได้ hot reload ให้ใช้ `docker compose up postgres` แล้วรัน dotnet/npm บนเครื่องแทน |

- App รัน migration อัตโนมัติตอน start (`db.Database.Migrate()`) ใช้ได้ทั้ง 2 แบบ และใช้บน Render ด้วย
- `Dockerfile` ตัวเดียวกันใช้ทั้งใน compose และบน Render
- บนเครื่องนี้ยังเช็ค Dockerfile ไม่ได้ Render จะ build ให้ตอน milestone 8 ถ้าพังจะแก้ตอนนั้น

## DB schema (EF Core, ใช้ migration)
- `Teacher(Id, Email unique, Name)`
- `Term(Id, TeacherId, Name "1/2569", CreatedAt)`
- `Classroom(Id, TermId, Name "ม.2/1")`
- `Student(Id, StudentCode unique, FirstName, LastName, GoogleSub? unique, CodeHash, FailedAttempts, LockedUntil?)`
- `Enrollment(ClassroomId, StudentId, No)` PK คู่
- `AssessmentItem(Id, ClassroomId, Name, MaxScore, SortOrder)`
- `Score(ItemId, StudentId, Value decimal?)` PK คู่ มี check constraint `0 <= Value`
- `ScoreAudit(Id, ItemId, StudentId, OldValue, NewValue, TeacherId, At)`
- `Appeal(Id, ItemId, StudentId, Status Open|Answered|Closed, UnreadByTeacher, UnreadByStudent, CreatedAt)`
- `AppealMessage(Id, AppealId, FromTeacher bool, Body, At)`

รหัสส่วนตัว: สุ่ม 8 ตัวอักษร จากชุดตัวอักษรที่ไม่ชวนสับสน (ไม่มี 0/O/1/l) เก็บเป็น hash ด้วย `PasswordHasher` แสดง plaintext **ครั้งเดียว** ตอนสร้างหรือรีเซ็ต แล้วให้ครูพิมพ์ใบแจก
`// ponytail: ครูทำใบหาย = กดรีเซ็ต แทนการเก็บ plaintext`

## Auth
- Cookie auth + `AddGoogle` แยก role จาก claim `teacher` / `student`
- Google callback: อีเมลอยู่ในตาราง Teacher → role teacher / `GoogleSub` ผูกกับ Student แล้ว → role student / ไม่เข้าเงื่อนไขไหนเลย → ไปหน้า `/claim`
- `POST /api/auth/claim` (ต้องล็อกอิน Google ไว้แล้ว): รหัสนักเรียน + รหัสส่วนตัว → ผูก GoogleSub
- `POST /api/auth/code-login`: รหัสนักเรียน + รหัสส่วนตัว (เส้นทางสำหรับเด็กที่ไม่มี Gmail)
- ทั้ง 2 เส้นทาง: `RateLimiter` แบบ fixed window ต่อ IP + ผิด 5 ครั้งล็อก 15 นาที
- Bootstrap ครูคนแรกจาก env `TEACHER_EMAILS`

## API (สรุป)
- Teacher: `/api/terms` CRUD · `/api/terms/{id}/classrooms` CRUD · `/api/classrooms/{id}` (items + students + scores) · `PUT /api/scores` (แก้ทีละช่อง + เขียน audit) · `/api/classrooms/{id}/items` CRUD · `/api/classrooms/{id}/students` เพิ่ม/แก้ · `POST /api/students/{id}/reset-code`
- Import: `GET /api/import/template` · `POST /api/classrooms/{id}/import/preview` (≤2MB, .xlsx) · `POST .../import/commit`
- Student: `GET /api/me/terms` · `GET /api/me/scores?termId=`
- Appeals: `POST /api/appeals` (student) · `GET /api/appeals` (ครูเห็นทั้งหมด / เด็กเห็นของตัวเอง) · `POST /api/appeals/{id}/messages` · `PATCH /api/appeals/{id}` (ปิดเรื่อง)
- ทุก endpoint ของครูต้องเช็คว่า Term เป็นของ TeacherId ที่ล็อกอินอยู่

## Excel template
แถว 1 เป็นหัวตาราง: `รหัสนักเรียน | ชื่อ | นามสกุล | ควิซ1 (10) | สอบกลางภาค (30) | ...` ตัวเลขในวงเล็บคือคะแนนเต็ม
Parser (`backend/src/Api/Import/ScoreSheetParser.cs`) เป็นฟังก์ชันที่ไม่แตะ DB: รับ stream คืน rows + errors (แถว/คอลัมน์ + ข้อความ) เช่น รหัสซ้ำ, คะแนนเกินเต็ม, ช่องไม่ใช่ตัวเลข, ไม่มีหัวคะแนนเต็ม
Commit: upsert นักเรียนตามรหัส, สร้างรายการที่ยังไม่มี, เขียนคะแนนทับของเดิม + บันทึก audit, นักเรียนใหม่ได้รหัสส่วนตัวแล้วแสดงหน้าใบแจกรหัส

## Frontend pages
`/login` · `/claim` · `/student` (เลือกเทอม → การ์ดคะแนนแต่ละรายการ + ผลรวม + ปุ่มท้วง) · `/teacher` (เทอม/ห้อง) · `/teacher/classrooms/:id` (ตารางคะแนนแก้ได้, import, จัดการรายการ, พิมพ์ใบรหัส) · `/teacher/appeals` · `/appeals/:id` (thread ใช้ร่วมกันทั้งสองฝั่ง)
Shared components: `ScoreTable` (prop `editable`), `AppealThread`, `ExcelImport`, `AppLayout` — แยกเป็น component เฉพาะส่วนที่ใช้ตั้งแต่ 2 ที่ขึ้นไป
Badge ข้อความใหม่: TanStack Query refetch ทุก 60 วินาที + refetch ตอนกลับมาที่แท็บ

## ลำดับการทำ (หยุดให้ดูทุก milestone)
1. **Scaffold:** clone repo ลง `D:\Peerapong\Projects\Krugift-chiangmai` → `dotnet new web` + `dotnet new xunit` + `npm create vite` + Tailwind + shadcn init, `.gitignore` (ห้าม commit `appsettings.Development.json`, `.env`, `*.xlsx`)
2. **DB:** entities + DbContext + migration แรก → รันกับ Neon dev branch
3. **Auth:** Google + teacher whitelist + claim + code-login + rate limit/lockout
4. **ครู:** เทอม/ห้อง/รายการ/นักเรียน CRUD + ตารางคะแนนแก้ได้ + audit
5. **Import:** template + parser (+ เทสต์) + preview/commit + ใบแจกรหัส
6. **เด็ก:** หน้าดูคะแนนตามเทอม
7. **ท้วงคะแนน:** thread + badge
8. **Deploy:** Dockerfile + docker-compose.yml + .env.example + README วิธีรัน 2 แบบ → Render (เชื่อม GitHub) + Neon production branch + env vars + backup

## Backup
Neon ฟรีมี point-in-time restore แต่ย้อนได้แค่ช่วงสั้น เลยเพิ่ม GitHub Action รายวัน: `pg_dump` → **เข้ารหัสด้วย gpg (passphrase เก็บใน GitHub Secret)** → เก็บเป็น artifact 30 วัน
เหตุผล: repo นี้อ่านได้โดยไม่ต้อง auth น่าจะเป็น public และคะแนนเด็กเป็นข้อมูลส่วนบุคคลของผู้เยาว์ ห้ามเก็บแบบไม่เข้ารหัส

## สิ่งที่ผู้ใช้ต้องทำเอง (ผมทำแทนไม่ได้)
- สมัคร Neon แล้วส่ง connection string (dev + prod branch) มา **ใส่ในไฟล์ local หรือ env เท่านั้น ห้ามวางในแชทถ้าเลี่ยงได้**
- สร้าง Google OAuth Client ใน Google Cloud Console (ผมจะเขียนขั้นตอนให้ตอนถึง milestone 3)
- สมัคร Render แล้วเชื่อม GitHub (milestone 8)
- ถามเพื่อน: อีเมล Google ของครู + ขอไฟล์ Excel ตัวอย่าง (ถ้าได้)

## Verification
- `dotnet test` — parser: ไฟล์ถูก / รหัสซ้ำ / คะแนนเกินเต็ม / ไม่ใช่ตัวเลข / ไม่มีหัวคะแนนเต็ม · รหัสส่วนตัว: hash แล้ว verify ผ่าน, ผิด 5 ครั้งถูกล็อก
- `dotnet build` + `npm run build` ผ่านทุก milestone
- Manual E2E (ใช้ Playwright MCP ได้): ครูล็อกอิน → สร้างเทอม/ห้อง → import template → แก้คะแนน 1 ช่อง → ดู audit
  → เด็กล็อกอินด้วยรหัส → เห็นคะแนน → ท้วง → ครูเห็น badge → ตอบ → เด็กเห็น badge
- **Security check:** ใช้ cookie ของเด็ก A ยิง API ของครู → ต้องได้ 403 / แก้ termId หรือ id ใน request ต้องไม่เห็นข้อมูลของคนอื่น / ใส่รหัสผิด 6 ครั้ง → ถูกล็อก / upload ไฟล์ 5MB หรือไฟล์ที่ไม่ใช่ .xlsx → ถูกปฏิเสธ
- ทดสอบที่ความกว้าง 400px (มือถือ) และ 768px (iPad)
- หลัง deploy: เปิดเว็บบน Render ครบทุก flow ข้างบนอีกรอบ + ทดลอง restore backup ลง Neon branch ใหม่ 1 ครั้ง

Commit: ผมจะถามก่อน commit/push ทุก milestone
