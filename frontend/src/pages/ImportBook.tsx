import { useQueryClient } from '@tanstack/react-query'
import { FileUp } from 'lucide-react'
import { useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { FormError } from '@/components/FormError'
import { PageHeader } from '@/components/PageHeader'
import { Rows, Row } from '@/components/Rows'
import { Button, buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { cn } from '@/lib/utils'

/** ต้องตรงกับ Limits.ImportMaxBytes ฝั่ง backend */
const IMPORT_MAX_BYTES = 2 * 1024 * 1024
const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

type SheetReport = {
  name: string
  classroom: string
  students: number
  items: number
  itemNames: string[]
  scores: number
  isRoomSheet: boolean
  errorCount: number
}

type ImportError = { row: number | null; column: string | null; message: string }

type Preview = { sheets: SheetReport[]; errorCount: number; errors: ImportError[] }

function checkFile(file: File | null): string | null {
  if (!file) return 'กรุณาเลือกไฟล์ Excel (.xlsx)'
  if (!file.name.toLowerCase().endsWith('.xlsx'))
    return 'รองรับเฉพาะไฟล์ .xlsx ถ้าเป็น .xls ให้เปิดใน Excel แล้วเลือก Save As เป็น Excel Workbook (.xlsx)'
  if (file.size === 0) return 'ไฟล์ว่างเปล่า'
  if (file.size > IMPORT_MAX_BYTES) return `ไฟล์ใหญ่เกิน ${IMPORT_MAX_BYTES / 1024 / 1024} MB`
  return null
}

/** นำเข้าไฟล์คะแนนของครูทั้งไฟล์ (หลายชีท ชีทละห้อง) · /teacher/terms/:termId/import */
export default function ImportBook() {
  const termId = Number(useParams().termId)
  const queryClient = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [chosen, setChosen] = useState<string[]>([])
  const [imported, setImported] = useState<string[] | null>(null)
  // ปัดคะแนนที่หารแล้วเป็นจำนวนเต็มตั้งแต่ตอนนำเข้า (คะแนนดิบของครูเก็บค่าตามไฟล์เสมอ)
  const [round, setRound] = useState(false)
  const checking = useSubmit()
  const saving = useSubmit()
  const busy = checking.busy || saving.busy

  function upload<T>(step: 'preview' | 'commit', selected: File, sheets?: string[]) {
    const body = new FormData()
    body.append('file', selected)
    if (sheets) body.append('sheets', sheets.join(','))
    body.append('round', String(round))
    return api<T>(`/terms/${termId}/import/book/${step}`, { method: 'POST', body })
  }

  function choose(selected: File | null) {
    setFile(selected)
    setPreview(null)
    setChosen([])
    setImported(null)
    checking.setError('')
    saving.setError('')
  }

  async function check(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!checking.check(checkFile(file)) || !file) return
    const selected = file
    await checking.run(async () => {
      const result = await upload<Preview>('preview', selected)
      setPreview(result)
      // ติ๊กให้เองเฉพาะชีทห้องเรียนที่ตรวจแล้วไม่มีจุดผิด ครูเอาออกเองได้
      setChosen(result.sheets.filter((s) => s.isRoomSheet && s.errorCount === 0).map((s) => s.name))
    })
  }

  async function commit() {
    if (!file) return
    const selected = file
    const ok = await saving.run(async () => {
      const result = await upload<{ summary: string[] }>('commit', selected, chosen)
      setImported(result.summary)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: qk.classrooms(termId) }),
        queryClient.invalidateQueries({ queryKey: qk.terms }),
      ])
    })
    if (ok) {
      setPreview(null)
      setChosen([])
      setFile(null)
      if (fileInput.current) fileInput.current.value = ''
    }
  }

  const toggle = (name: string) =>
    setChosen((current) => (current.includes(name) ? current.filter((n) => n !== name) : [...current, name]))

  return (
    <>
      <Link to={routes.term(termId)} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← ห้องเรียน
      </Link>

      <PageHeader title="นำเข้าไฟล์คะแนนของครู" />

      <section aria-labelledby="book-upload" className="rounded-xl border bg-card p-4">
        <h2 id="book-upload" className="font-medium">
          1. เลือกไฟล์
        </h2>
        <form onSubmit={check} noValidate className="mt-3 grid gap-3">
          {/* ซ่อน input ของเบราว์เซอร์ (ข้อความข้างในเป็นภาษาอังกฤษ แก้ไม่ได้) แล้วใช้ label เป็นปุ่มแทน */}
          <input
            id="book-file"
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
            <label htmlFor="book-file" className={cn(buttonVariants({ variant: 'outline' }), 'justify-self-center')}>
              <FileUp aria-hidden="true" />
              เลือกไฟล์ Excel
            </label>
            <p className="text-sm break-all">{file ? file.name : 'ลากไฟล์มาวางตรงนี้ก็ได้ (.xlsx ไม่เกิน 2 MB)'}</p>
          </div>
          <label className="flex min-h-10 cursor-pointer items-center gap-3 text-sm">
            <input
              type="checkbox"
              checked={round}
              onChange={(e) => {
                setRound(e.target.checked)
                // ผลที่ preview โชว์ไว้คำนวณจากค่าเดิม ต้องกดตรวจใหม่ ไม่งั้นที่บันทึกจะไม่ตรงกับที่เห็น
                setPreview(null)
                setChosen([])
              }}
              className="size-5 shrink-0 accent-primary"
            />
            ปัดคะแนนที่หารแล้วเป็นจำนวนเต็ม (7.33 เป็น 7 และ 7.5 เป็น 8)
          </label>

          <FormError message={checking.error} />
          <Button type="submit" disabled={busy} className="justify-self-start">
            {checking.busy ? 'กำลังตรวจ...' : 'ตรวจไฟล์'}
          </Button>
        </form>
      </section>

      {preview && (
        <section aria-labelledby="book-sheets" className="mt-4 rounded-xl border bg-card p-4">
          <h2 id="book-sheets" className="font-medium">
            2. เลือกชีทที่จะนำเข้า
          </h2>

          <div className="mt-3">
            <Rows>
              {preview.sheets.map((sheet) => (
                <Row key={sheet.name}>
                  <label className="flex min-h-10 flex-1 cursor-pointer items-center gap-3">
                    <input
                      type="checkbox"
                      className="size-5 shrink-0 accent-primary disabled:opacity-40"
                      checked={chosen.includes(sheet.name)}
                      disabled={!sheet.isRoomSheet || busy}
                      onChange={() => toggle(sheet.name)}
                    />
                    <span className="min-w-0">
                      <span className="font-medium">{sheet.name}</span>
                      {sheet.isRoomSheet ? (
                        <span className="flex flex-wrap gap-x-3 text-xs text-muted-foreground">
                          <span>{sheet.classroom}</span>
                          <span>นักเรียน {sheet.students} คน</span>
                          <span>คะแนน {sheet.scores} ช่อง</span>
                        </span>
                      ) : (
                        <span className="block text-xs text-muted-foreground">ไม่ใช่ชีทห้องเรียน</span>
                      )}
                      {/* บอกชื่อรายการที่จะได้ ครูจะได้เห็นก่อนว่าเอาคอลัมน์ไหนมาบ้าง */}
                      {sheet.itemNames.length > 0 && (
                        <span className="mt-1 flex flex-wrap gap-1">
                          {sheet.itemNames.map((name) => (
                            <span key={name} className="rounded-full bg-muted px-2 py-0.5 text-xs">
                              {name}
                            </span>
                          ))}
                        </span>
                      )}
                    </span>
                  </label>
                  {sheet.errorCount > 0 && (
                    <span className="shrink-0 rounded-full bg-destructive/10 px-2.5 py-0.5 text-xs text-destructive">
                      ต้องแก้ {sheet.errorCount} จุด
                    </span>
                  )}
                </Row>
              ))}
            </Rows>
          </div>

          {preview.errorCount > 0 && <ErrorTable errors={preview.errors} total={preview.errorCount} />}

          <FormError message={saving.error} />
          <Button onClick={commit} disabled={busy || chosen.length === 0} className="mt-3">
            {saving.busy ? 'กำลังนำเข้า...' : `นำเข้า ${chosen.length} ชีท`}
          </Button>
        </section>
      )}

      {imported && (
        <section aria-labelledby="book-done" className="mt-4 rounded-xl border bg-card p-4">
          <h2 id="book-done" className="font-medium text-success">
            นำเข้าเรียบร้อย
          </h2>
          <ul className="mt-2 grid gap-1 text-sm">
            {imported.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
          <Link to={routes.term(termId)} className={cn(buttonVariants(), 'mt-3')}>
            ไปดูห้องเรียน
          </Link>
        </section>
      )}
    </>
  )
}

function ErrorTable({ errors, total }: { errors: ImportError[]; total: number }) {
  return (
    <div className="mt-4">
      <h3 className="font-medium text-destructive">พบจุดที่ต้องแก้ {total} จุด ยังไม่ได้บันทึกอะไร</h3>
      <div className="mt-2 overflow-x-auto">
        <table className="w-full text-left text-sm [&_td:first-child]:whitespace-nowrap [&_td:last-child]:[overflow-wrap:anywhere] [&_th]:whitespace-nowrap">
          <thead>
            <tr className="text-muted-foreground">
              <th className="py-1 pr-3">ตำแหน่ง</th>
              <th className="py-1">ปัญหา</th>
            </tr>
          </thead>
          <tbody>
            {errors.map((e, i) => (
              <tr key={i} className="border-t">
                <td className="py-1 pr-3">
                  {e.row === null ? 'ทั้งไฟล์' : `แถว ${e.row}${e.column ? ` คอลัมน์ ${e.column}` : ''}`}
                </td>
                <td className="py-1">{e.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
