import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { AuthCard } from '@/components/AuthCard'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { ScoreList } from '@/components/ScoreList'
import { api } from '@/lib/api'
import { routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

type PublicRoom = { id: number; name: string; term: string; nos: number[] }
type Result = {
  name: string
  classroom: string
  term: string
  items: { name: string; maxScore: number; value: number | null }[]
  total: number
  full: number
}

const selectClass =
  'h-10 w-full rounded-md border border-input bg-card px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring'

/** จัดห้องเป็นกลุ่มตามชื่อที่ครูตั้งไว้ คงลำดับที่ server ส่งมา */
function groupByTerm(rooms: PublicRoom[] | undefined): [string, PublicRoom[]][] {
  const groups = new Map<string, PublicRoom[]>()
  for (const room of rooms ?? []) groups.set(room.term, [...(groups.get(room.term) ?? []), room])
  return [...groups]
}

/**
 * ดูคะแนนด่วนโดยไม่ต้องล็อกอิน — ห้อง (dropdown) + เลขที่ (dropdown) + รหัสนักเรียน (พิมพ์)
 * ไม่มี dropdown ชื่อ เพราะหน้านี้เปิดสาธารณะ จะกลายเป็นใครก็ดูรายชื่อเด็กทั้งห้องได้
 * ผลลัพธ์อยู่ในหน้านี้เท่านั้น ไม่เก็บลงเครื่อง iPad ที่ใช้ร่วมกันคนต่อไปจะไม่เห็นของคนก่อน
 */
export default function QuickScores() {
  const rooms = useQuery({ queryKey: ['public', 'classrooms'], queryFn: () => api<PublicRoom[]>('/public/classrooms') })
  const [roomId, setRoomId] = useState('')
  const [result, setResult] = useState<Result | null>(null)
  const { busy, error, run, check } = useSubmit()

  const room = rooms.data?.find((r) => String(r.id) === roomId)

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    const studentCode = String(form.get('studentCode') ?? '')
    const no = Number(form.get('no'))

    if (!check(!roomId ? 'กรุณาเลือกห้อง' : !no ? 'กรุณาเลือกเลขที่' : validate.studentCode(studentCode))) return

    await run(async () => {
      setResult(
        await api<Result>('/public/scores', {
          method: 'POST',
          json: { classroomId: Number(roomId), no, studentCode },
        }),
      )
    })
  }

  if (result) {
    // เด็กไม่รู้จักคำว่า "ภาคเรียน" ในระบบเรา บอกแค่ห้องกับชื่อที่ครูตั้งไว้ก็พอ
    return (
      <AuthCard
        title={result.name}
        description={
          <span className="flex flex-col">
            <span>ห้อง {result.classroom}</span>
            <span>{result.term}</span>
          </span>
        }
      >
        <ScoreList items={result.items} />

        <div className="grid gap-2">
          <Button variant="outline" onClick={() => setResult(null)}>
            กรอกใหม่
          </Button>
          <p className="text-center text-xs text-muted-foreground">
            มีคำถามเรื่องคะแนน?{' '}
            <Link to={routes.login} className="underline underline-offset-2">
              เข้าสู่ระบบด้วย Google
            </Link>
          </p>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="ดูคะแนนด่วน">
      <form onSubmit={onSubmit} className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="room">ห้อง</Label>
          <select
            id="room"
            value={roomId}
            onChange={(e) => setRoomId(e.target.value)}
            className={selectClass}
            disabled={rooms.isPending}
          >
            <option value="">{rooms.isPending ? 'กำลังโหลด...' : '— เลือกห้อง —'}</option>
            {/* จัดกลุ่มตามชื่อที่ครูตั้ง (optgroup ของเบราว์เซอร์) แทนการเอาชื่อมาต่อท้ายห้องด้วยตัวคั่น */}
            {groupByTerm(rooms.data).map(([term, list]) => (
              <optgroup key={term} label={term}>
                {list.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </div>

        <div className="grid gap-2">
          <Label htmlFor="no">เลขที่</Label>
          {/* key ทำให้ล้างค่าที่เลือกไว้เมื่อเปลี่ยนห้อง เลขที่ของห้องเก่าจะได้ไม่ค้าง */}
          <select key={roomId} id="no" name="no" defaultValue="" className={selectClass} disabled={!room}>
            <option value="">{room ? '— เลือกเลขที่ —' : 'เลือกห้องก่อน'}</option>
            {room?.nos.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </div>

        <Field label="รหัสนักเรียน" name="studentCode" inputMode="numeric" autoComplete="off" required />

        <FormError message={error || (rooms.isError ? 'โหลดรายชื่อห้องไม่สำเร็จ กรุณาลองใหม่' : '')} />

        <Button type="submit" disabled={busy}>
          {busy ? 'กำลังค้นหา...' : 'ดูคะแนน'}
        </Button>
      </form>

      <Link to={routes.login} className="text-center text-sm text-muted-foreground underline underline-offset-2">
        ← กลับไปหน้าเข้าสู่ระบบ
      </Link>
    </AuthCard>
  )
}
