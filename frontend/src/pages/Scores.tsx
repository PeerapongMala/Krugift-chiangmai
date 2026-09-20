import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type KeyboardEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ClassroomTabs } from '@/components/ClassroomTabs'
import { ExcelButtons } from '@/components/ExcelButtons'
import { FormError } from '@/components/FormError'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { TabToolbar } from '@/components/TabToolbar'
import { api, errorMessage } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { validate } from '@/lib/validate'

type Item = { id: number; name: string; maxScore: number; teacherOnly: boolean }
type Pupil = { studentId: number; no: number; studentCode: string; firstName: string; lastName: string }
type Cell = { itemId: number; studentId: number; value: number }
type Grid = { items: Item[]; students: Pupil[]; scores: Cell[] }

export default function Scores() {
  const classroomId = Number(useParams().classroomId)
  const queryClient = useQueryClient()
  const [error, setError] = useState('')

  const grid = useQuery({
    queryKey: qk.scores(classroomId),
    queryFn: () => api<Grid>(`/classrooms/${classroomId}/scores`),
    enabled: Number.isInteger(classroomId),
  })

  /**
   * บันทึกทีละช่อง · ส่ง expected (ค่าที่หน้าจอเห็นอยู่) ไปด้วย
   * ถ้าครูอีกคนแก้ช่องนี้ไปก่อน server จะตอบ 409 แทนที่จะทับของเขา
   */
  async function saveCell(itemId: number, studentId: number, next: number | null, expected: number | null) {
    setError('')
    try {
      await api('/scores', { method: 'PUT', json: { itemId, studentId, value: next, expected } })

      // อัปเดตแคชเฉพาะช่องที่แก้ ไม่ refetch ทั้งตาราง โฟกัสจะได้ไม่กระโดด
      queryClient.setQueryData<Grid>(qk.scores(classroomId), (old) =>
        old === undefined
          ? old
          : {
              ...old,
              scores:
                next === null
                  ? old.scores.filter((s) => !(s.itemId === itemId && s.studentId === studentId))
                  : old.scores.some((s) => s.itemId === itemId && s.studentId === studentId)
                    ? old.scores.map((s) =>
                        s.itemId === itemId && s.studentId === studentId ? { ...s, value: next } : s,
                      )
                    : [...old.scores, { itemId, studentId, value: next }],
            },
      )
      return true
    } catch (err) {
      setError(errorMessage(err))
      // ชนกันหรือพลาดอย่างอื่น ดึงของจริงมาใหม่เพื่อไม่ให้หน้าจอค้างค่าเก่า
      await queryClient.invalidateQueries({ queryKey: qk.scores(classroomId) })
      return false
    }
  }

  return (
    <>
      <Link to={routes.teacher} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← ภาคเรียน
      </Link>

      <PageHeader title="ตารางคะแนน" />

      <ClassroomTabs classroomId={classroomId} active="scores" />

      <TabToolbar>
        <ExcelButtons classroomId={classroomId} kind="scores" />
      </TabToolbar>

      <FormError message={error} />

      <QueryState query={grid} empty="ยังไม่มีข้อมูล">
        {(data) =>
          data.items.length === 0 ? (
            <p className="py-10 text-center text-sm text-muted-foreground">
              ยังไม่มีรายการคะแนน —{' '}
              <Link to={routes.classroomItems(classroomId)} className="underline underline-offset-2">
                เพิ่มรายการก่อน
              </Link>
            </p>
          ) : data.students.length === 0 ? (
            <p className="py-10 text-center text-sm text-muted-foreground">
              ยังไม่มีนักเรียนในห้องนี้ —{' '}
              <Link to={routes.classroom(classroomId)} className="underline underline-offset-2">
                เพิ่มนักเรียนก่อน
              </Link>
            </p>
          ) : (
            <GridTable data={data} onSave={saveCell} />
          )
        }
      </QueryState>
    </>
  )
}

function GridTable({
  data,
  onSave,
}: {
  data: Grid
  onSave: (itemId: number, studentId: number, next: number | null, expected: number | null) => Promise<boolean>
}) {
  const valueOf = (itemId: number, studentId: number) =>
    data.scores.find((s) => s.itemId === itemId && s.studentId === studentId)?.value ?? null

  const fullTotal = data.items.reduce((sum, i) => sum + Number(i.maxScore), 0)

  return (
    // ตารางกว้างเกินจอมือถือแน่ ให้เลื่อนในกล่องตัวเอง ไม่ใช่ทั้งหน้า
    <div className="overflow-x-auto rounded-lg border bg-card">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b bg-muted">
            <th className="sticky left-0 z-10 max-w-36 bg-muted px-3 py-2 text-left font-medium sm:max-w-none">นักเรียน</th>
            {data.items.map((item) => (
              <th key={item.id} className="min-w-24 px-2 py-2 text-center font-medium">
                <span className="block truncate">{item.name}</span>
                <span className="block text-xs font-normal text-muted-foreground">เต็ม {item.maxScore}</span>
                {/* คะแนนดิบที่นำเข้ามาคู่กัน เด็กไม่เห็นรายการนี้ */}
                {item.teacherOnly && <span className="block text-xs font-normal text-primary">เฉพาะครู</span>}
              </th>
            ))}
            <th className="min-w-20 px-3 py-2 text-center font-medium">
              <span className="block">รวม</span>
              <span className="block text-xs font-normal text-muted-foreground">เต็ม {fullTotal}</span>
            </th>
          </tr>
        </thead>

        <tbody>
          {data.students.map((pupil) => {
            const total = data.items.reduce((sum, i) => sum + (valueOf(i.id, pupil.studentId) ?? 0), 0)

            return (
              <tr key={pupil.studentId} className="border-b last:border-0">
                <th scope="row" className="sticky left-0 z-10 max-w-36 bg-card px-3 py-1.5 text-left font-normal sm:max-w-none">
                  <span className="block truncate">
                    <span className="text-muted-foreground">{pupil.no}.</span> {pupil.firstName} {pupil.lastName}
                  </span>
                  <span className="block text-xs text-muted-foreground">{pupil.studentCode}</span>
                </th>

                {data.items.map((item) => {
                  const current = valueOf(item.id, pupil.studentId)
                  return (
                    <td key={item.id} className="px-1 py-1 text-center">
                      <ScoreCell
                        key={`${item.id}-${pupil.studentId}-${current}`}
                        current={current}
                        maxScore={Number(item.maxScore)}
                        onSave={(next) => onSave(item.id, pupil.studentId, next, current)}
                      />
                    </td>
                  )
                })}

                <td className="px-3 py-1.5 text-center font-medium tabular-nums">{total}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

/** ข้อความเตือนใต้ช่องแสดงค้างไว้กี่มิลลิวินาที */
const NOTICE_MS = 3000

/** รับเฉพาะตัวเลข ทศนิยมไม่เกิน 2 ตำแหน่ง · เลขไทยแปลงเป็นเลขอารบิกให้ (แป้นพิมพ์ไทยพิมพ์ ๑๒๓ ได้) · คืน null ถ้าพิมพ์อย่างอื่น */
function toScoreInput(text: string): string | null {
  const ascii = text.replace(/[๐-๙]/g, (digit) => String(digit.charCodeAt(0) - 0x0e50))
  return /^\d*(\.\d{0,2})?$/.test(ascii) ? ascii : null
}

function ScoreCell({
  current,
  maxScore,
  onSave,
}: {
  current: number | null
  maxScore: number
  onSave: (next: number | null) => Promise<boolean>
}) {
  const shown = current === null ? '' : String(current)
  const [draft, setDraft] = useState(shown)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')

  // เตือนแล้วหายเอง ครูไม่ต้องกดปิด
  useEffect(() => {
    if (!notice) return
    const timer = setTimeout(() => setNotice(''), NOTICE_MS)
    return () => clearTimeout(timer)
  }, [notice])

  async function commit() {
    const trimmed = draft.trim()
    const next = trimmed === '' || trimmed === '.' ? null : Number(trimmed)

    // ไม่เปลี่ยนก็ไม่ต้องยิง จะได้ไม่เขียน audit ซ้ำโดยไม่จำเป็น
    if (next === current) {
      setDraft(shown)
      return
    }

    const bad = validate.score(next, maxScore)
    if (bad) {
      // ค่าที่ผิดไม่ค้างในช่อง คืนคะแนนเดิมทันที แล้วบอกเหตุผลสั้น ๆ
      setDraft(shown)
      setNotice(bad)
      return
    }

    setNotice('')
    setBusy(true)
    const ok = await onSave(next)
    setBusy(false)
    if (!ok) setDraft(shown)
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') e.currentTarget.blur()
    if (e.key === 'Escape') {
      setDraft(shown)
      setNotice('')
      e.currentTarget.blur()
    }
  }

  return (
    <>
      <input
        value={draft}
        onChange={(e) => {
          const next = toScoreInput(e.target.value)
          if (next !== null) setDraft(next)
        }}
        onBlur={commit}
        onKeyDown={onKeyDown}
        disabled={busy}
        inputMode="decimal"
        aria-label={`คะแนน เต็ม ${maxScore}`}
        className={`w-16 rounded-md border border-input px-2 py-1 text-center tabular-nums outline-none focus:ring-2 focus:ring-ring ${
          busy ? 'opacity-50' : ''
        }`}
      />
      {notice && (
        <span role="status" className="mt-0.5 block text-xs text-destructive">
          {notice}
        </span>
      )}
    </>
  )
}
