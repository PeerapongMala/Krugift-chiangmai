import { useQueryClient } from '@tanstack/react-query'
import { FileUp } from 'lucide-react'
import { useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ClassroomTabs } from '@/components/ClassroomTabs'
import { FormError } from '@/components/FormError'
import { PageHeader } from '@/components/PageHeader'
import { Button, buttonVariants } from '@/components/ui/button'
import { api, ApiError } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { cn } from '@/lib/utils'

/** ต้องตรงกับ Limits.ImportMaxBytes ฝั่ง backend */
const IMPORT_MAX_BYTES = 2 * 1024 * 1024
const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

type Kind = 'students' | 'scores'

type ImportError = { row: number | null; column: string | null; message: string }
type ImportChange = { row: number; label: string; detail: string }
type Preview = {
  errorCount: number
  errors: ImportError[]
  summary: string[] | null
  hasChanges: boolean
  changes: ImportChange[]
  changeCount: number
}

const KINDS: Record<Kind, { title: string; howTo: string; back: (classroomId: number) => string; backLabel: string; doneLabel: string }> = {
  students: {
    title: 'นำเข้ารายชื่อนักเรียนจาก Excel',
    howTo: 'คอลัมน์: เลขที่ · รหัสนักเรียน · ชื่อ · นามสกุล',
    back: routes.classroom,
    backLabel: '← นักเรียนในห้อง',
    doneLabel: 'ไปดูรายชื่อนักเรียน',
  },
  scores: {
    title: 'นำเข้าคะแนนจาก Excel',
    howTo: 'หัวคอลัมน์คะแนน: ชื่อรายการ (คะแนนเต็ม) เช่น สอบกลางภาค (20)',
    back: routes.classroomScores,
    backLabel: '← ตารางคะแนน',
    doneLabel: 'ไปดูตารางคะแนน',
  },
}

const isKind = (value: string | undefined): value is Kind => value === 'students' || value === 'scores'

/** ตรวจก่อนส่งให้รู้ผลทันที · ตัวกันจริงคือ ImportEndpoints + SheetReader ฝั่ง backend */
function checkFile(file: File | null): string | null {
  if (!file) return 'กรุณาเลือกไฟล์ Excel (.xlsx)'
  if (!file.name.toLowerCase().endsWith('.xlsx'))
    return 'รองรับเฉพาะไฟล์ .xlsx · ถ้าเป็น .xls หรือ .csv ให้เปิดใน Excel แล้วเลือก Save As เป็น Excel Workbook (.xlsx)'
  if (file.size === 0) return 'ไฟล์ว่างเปล่า'
  if (file.size > IMPORT_MAX_BYTES) return `ไฟล์ใหญ่เกิน ${IMPORT_MAX_BYTES / 1024 / 1024} MB`
  return null
}

/** นำเข้า Excel แบบ all-or-nothing · /teacher/classrooms/:classroomId/import/:kind */
export default function ImportExcel() {
  const { classroomId, kind } = useParams()
  if (!isKind(kind)) return <p className="text-sm text-muted-foreground">ไม่พบหน้านี้</p>

  // key: สลับไปอีกแบบแล้วเริ่มใหม่หมด ไฟล์และผลตรวจของอีกแบบจะได้ไม่ค้าง
  return <ImportForm key={kind} classroomId={Number(classroomId)} kind={kind} />
}

function ImportForm({ classroomId, kind }: { classroomId: number; kind: Kind }) {
  const info = KINDS[kind]
  const queryClient = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [imported, setImported] = useState<string[] | null>(null)
  const checking = useSubmit()
  const saving = useSubmit()
  const busy = checking.busy || saving.busy
  const base = `/classrooms/${classroomId}/import/${kind}`

  function upload<T>(step: 'preview' | 'commit', selected: File) {
    const body = new FormData()
    body.append('file', selected)
    return api<T>(`${base}/${step}`, { method: 'POST', body })
  }

  /** ล้างช่องเลือกไฟล์ · ครูแก้ไฟล์แล้วเลือกไฟล์ชื่อเดิมซ้ำ เบราว์เซอร์จะได้อ่านเนื้อหาใหม่จริง */
  function clearFile() {
    setFile(null)
    if (fileInput.current) fileInput.current.value = ''
  }

  function choose(selected: File | null) {
    setFile(selected)
    setPreview(null)
    setImported(null)
    checking.setError('')
    saving.setError('')
  }

  function show(result: Preview) {
    setPreview(result)
    if (result.errorCount > 0) clearFile()
  }

  async function check(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!checking.check(checkFile(file)) || !file) return
    const selected = file
    await checking.run(async () => show(await upload<Preview>('preview', selected)))
  }

  async function commit() {
    if (!file) return
    const selected = file
    const ok = await saving.run(async () => {
      try {
        setImported((await upload<{ summary: string[] }>('commit', selected)).summary)
      } catch (err) {
        // ข้อมูลในระบบเปลี่ยนไปหลังตรวจ ตรวจซ้ำให้เลย ครูจะได้เห็นจุดผิดล่าสุด
        if (err instanceof ApiError && err.status === 409) show(await upload<Preview>('preview', selected))
        throw err
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: qk.classroom(classroomId) }),
        // จำนวนนักเรียนบนหน้ารายการห้องต้องอัปเดตตาม
        queryClient.invalidateQueries({ queryKey: qk.terms }),
      ])
    })
    if (ok) {
      setPreview(null)
      clearFile()
    }
  }

  return (
    <>
      <Link to={info.back(classroomId)} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        {info.backLabel}
      </Link>

      <PageHeader title={info.title} />

      <ClassroomTabs classroomId={classroomId} active={kind} />

      <div className="grid gap-4 md:grid-cols-2">
        <section aria-labelledby="import-template" className="rounded-lg border bg-card p-4">
          <h2 id="import-template" className="font-medium">
            1. ดาวน์โหลดไฟล์ตัวอย่าง
          </h2>
          <p className="mt-1 mb-3 text-sm text-muted-foreground">{info.howTo}</p>
          {/* ข้อมูลปัจจุบันของห้องใส่ไว้ให้แล้ว แก้แล้วอัปโหลดกลับได้เลย */}
          <a href={`/api${base}/template`} download className={buttonVariants({ variant: 'outline' })}>
            ดาวน์โหลดไฟล์ตัวอย่าง (.xlsx)
          </a>
        </section>

        <section aria-labelledby="import-upload" className="rounded-lg border bg-card p-4">
          <h2 id="import-upload" className="font-medium">
            2. อัปโหลดไฟล์เพื่อตรวจ
          </h2>
          <form onSubmit={check} noValidate className="mt-3 grid gap-3">
            {/* ซ่อน input ของเบราว์เซอร์ (ข้อความในนั้นเป็นภาษาอังกฤษ แก้ไม่ได้) แล้วใช้ label เป็นปุ่มแทน */}
            <input
              id="import-file"
              ref={fileInput}
              type="file"
              accept={`.xlsx,${XLSX_TYPE}`}
              onChange={(e) => choose(e.target.files?.[0] ?? null)}
              className="sr-only"
            />
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault()
                choose(e.dataTransfer.files?.[0] ?? null)
              }}
              className="grid gap-2 rounded-xl border border-dashed p-4 text-center"
            >
              <label htmlFor="import-file" className={cn(buttonVariants({ variant: 'outline' }), 'justify-self-center')}>
                <FileUp aria-hidden="true" />
                เลือกไฟล์ Excel
              </label>
              <p className="text-sm break-all">{file ? file.name : 'ลากไฟล์มาวางตรงนี้ก็ได้ · .xlsx ไม่เกิน 2 MB'}</p>
            </div>
            <FormError message={checking.error} />
            <Button type="submit" disabled={busy} className="justify-self-start">
              {checking.busy ? 'กำลังตรวจ...' : 'ตรวจไฟล์'}
            </Button>
          </form>
        </section>
      </div>

      {preview && preview.errorCount > 0 && <ErrorTable preview={preview} />}

      {preview?.summary && (
        <section aria-labelledby="import-ready" className="mt-4 rounded-lg border bg-card p-4">
          <h2 id="import-ready" className="font-medium">
            3. ตรวจแล้วไม่พบจุดผิด
          </h2>
          <SummaryList lines={preview.summary} />
          {preview.changes.length > 0 && <ChangeTable preview={preview} />}
          <FormError message={saving.error} />
          {preview.hasChanges ? (
            <Button onClick={commit} disabled={busy}>
              {saving.busy ? 'กำลังบันทึก...' : 'ยืนยันนำเข้า'}
            </Button>
          ) : (
            <p className="text-sm text-muted-foreground">ไฟล์นี้ไม่มีอะไรต่างจากในระบบ ไม่ต้องนำเข้า</p>
          )}
        </section>
      )}

      {imported && (
        <section role="status" aria-labelledby="import-done" className="mt-4 rounded-lg border bg-accent/40 p-4">
          <h2 id="import-done" className="font-medium">
            นำเข้าเรียบร้อย
          </h2>
          <SummaryList lines={imported} />
          <Link to={info.back(classroomId)} className={buttonVariants({ variant: 'outline' })}>
            {info.doneLabel}
          </Link>
        </section>
      )}
    </>
  )
}

function SummaryList({ lines }: { lines: string[] }) {
  return (
    <ul className="mt-2 mb-3 grid gap-1 text-sm">
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  )
}

function ErrorTable({ preview }: { preview: Preview }) {
  return (
    <section aria-labelledby="import-errors" className="mt-4 rounded-lg border border-destructive/40 bg-card p-4">
      <h2 id="import-errors" className="font-medium text-destructive">
        พบจุดที่ต้องแก้ {preview.errorCount} จุด · ยังไม่ได้บันทึกอะไร
      </h2>
      <p className="mt-1 mb-3 text-sm text-muted-foreground">
        {preview.errorCount > preview.errors.length && `แสดง ${preview.errors.length} จุดแรก`}
      </p>
      <div className="overflow-x-auto">
        {/* ไม่ล็อกความกว้างขั้นต่ำ · ตัดคำเฉพาะคอลัมน์ข้อความยาวคอลัมน์สุดท้าย เลขแถวและหัวตารางไม่แตกเป็นทีละตัวอักษรบนมือถือ */}
        <table className="w-full text-left text-sm [&_td:first-child]:whitespace-nowrap [&_td:last-child]:[overflow-wrap:anywhere] [&_th]:whitespace-nowrap">
          <thead className="text-xs text-muted-foreground">
            <tr>
              <th scope="col" className="py-2 pr-3 font-medium">
                แถว
              </th>
              <th scope="col" className="py-2 pr-3 font-medium">
                คอลัมน์
              </th>
              <th scope="col" className="py-2 font-medium">
                ปัญหา
              </th>
            </tr>
          </thead>
          <tbody>
            {preview.errors.map((error, index) => (
              <tr key={`${error.row}-${error.column}-${index}`} className="border-t align-top">
                <td className="py-2 pr-3 tabular-nums">{error.row ?? 'หัวตาราง'}</td>
                <td className="py-2 pr-3">{error.column ?? '—'}</td>
                <td className="py-2">{error.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function ChangeTable({ preview }: { preview: Preview }) {
  return (
    <div className="mb-3">
      <div className="max-h-96 overflow-auto rounded-md border">
        {/* ไม่ล็อกความกว้างขั้นต่ำ · ตัดคำเฉพาะคอลัมน์ข้อความยาวคอลัมน์สุดท้าย เลขแถวและหัวตารางไม่แตกเป็นทีละตัวอักษรบนมือถือ */}
        <table className="w-full text-left text-sm [&_td:first-child]:whitespace-nowrap [&_td:last-child]:[overflow-wrap:anywhere] [&_th]:whitespace-nowrap">
          <thead className="sticky top-0 bg-card text-xs text-muted-foreground">
            <tr>
              <th scope="col" className="px-3 py-2 font-medium">
                แถว
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                รายการ
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                เปลี่ยนเป็น
              </th>
            </tr>
          </thead>
          <tbody>
            {preview.changes.map((change, index) => (
              <tr key={`${change.row}-${index}`} className="border-t">
                <td className="px-3 py-2 tabular-nums">{change.row}</td>
                <td className="px-3 py-2">{change.label}</td>
                <td className="px-3 py-2 tabular-nums">{change.detail}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {preview.changeCount > preview.changes.length && (
        <p className="mt-1 text-xs text-muted-foreground">
          แสดง {preview.changes.length} จาก {preview.changeCount} รายการ
        </p>
      )}
    </div>
  )
}
