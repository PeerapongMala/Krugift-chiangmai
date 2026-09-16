import { useQuery, useQueryClient } from '@tanstack/react-query'
import { FileDown, FileUp } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { ClassroomTabs } from '@/components/ClassroomTabs'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Button, buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { firstError, validate } from '@/lib/validate'

type Student = {
  studentId: number
  no: number
  studentCode: string
  firstName: string
  lastName: string
  hasGoogle: boolean
}

export default function Students() {
  const classroomId = Number(useParams().classroomId)
  const queryClient = useQueryClient()
  const students = useQuery({
    queryKey: qk.students(classroomId),
    queryFn: () => api<Student[]>(`/classrooms/${classroomId}/students`),
    enabled: Number.isInteger(classroomId),
  })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Student | null>(null)
  const form = useSubmit()
  const rowAction = useSubmit()
  const { confirm, dialog } = useConfirm()

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: qk.students(classroomId) }),
      // จำนวนนักเรียนบนหน้ารายการห้องต้องอัปเดตตาม
      queryClient.invalidateQueries({ queryKey: qk.terms }),
    ])

  function read(f: FormData) {
    return {
      studentCode: String(f.get('studentCode') ?? ''),
      firstName: String(f.get('firstName') ?? ''),
      lastName: String(f.get('lastName') ?? ''),
      no: Number(f.get('no')),
    }
  }

  async function create(f: FormData) {
    const v = read(f)
    const bad = firstError(
      validate.studentCode(v.studentCode),
      validate.name(v.firstName, 'ชื่อนักเรียน'),
      validate.name(v.lastName, 'นามสกุลนักเรียน'),
    )
    if (!form.check(bad)) return

    const ok = await form.run(async () => {
      await api(`/classrooms/${classroomId}/students`, { method: 'POST', json: v })
      await refresh()
    })
    if (ok) setAdding(false)
  }

  async function save(f: FormData) {
    if (!editing) return
    const v = read(f)
    const bad = firstError(validate.name(v.firstName, 'ชื่อนักเรียน'), validate.name(v.lastName, 'นามสกุลนักเรียน'))
    if (!form.check(bad)) return

    const ok = await form.run(async () => {
      await api(`/classrooms/${classroomId}/students/${editing.studentId}`, {
        method: 'PATCH',
        json: { firstName: v.firstName, lastName: v.lastName, no: v.no },
      })
      await refresh()
    })
    if (ok) setEditing(null)
  }

  async function takeOut(s: Student) {
    const ok = await confirm({
      title: 'ย้ายนักเรียนออกจากห้องนี้?',
      description: `${s.firstName} ${s.lastName} (${s.studentCode}) · ย้ายออกจากห้องนี้เท่านั้น ข้อมูลนักเรียนและคะแนนภาคเรียนอื่นยังอยู่`,
      confirmLabel: 'ย้ายออก',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/classrooms/${classroomId}/students/${s.studentId}`, { method: 'DELETE' })
      await refresh()
    })
  }

  async function unlink(s: Student) {
    const ok = await confirm({
      title: 'ยกเลิกการเชื่อมบัญชี Google?',
      description: `${s.firstName} ${s.lastName} จะต้องกรอกรหัสนักเรียนเพื่อเชื่อมบัญชีใหม่อีกครั้ง · ใช้ตอนนักเรียนเชื่อมผิดบัญชี หรือมีคนอื่นเผลอใช้รหัสนี้เชื่อมบัญชี`,
      confirmLabel: 'ยกเลิกการเชื่อม',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/students/${s.studentId}/unlink`, { method: 'POST' })
      await refresh()
    })
  }

  return (
    <>
      <Link to={routes.teacher} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← ภาคเรียน
      </Link>

      <PageHeader
        title="นักเรียนในห้อง"
        description="นักเรียนเข้าสู่ระบบด้วย Google แล้วกรอกรหัสนักเรียนเพื่อเชื่อมบัญชีเอง ครูไม่ต้องแจกรหัสใด ๆ"
      >
        <Link to={routes.classroomImport(classroomId, 'students')} className={buttonVariants({ variant: 'outline' })}>
          <FileUp aria-hidden="true" />
          นำเข้า
        </Link>
        {/* นำออก = ไฟล์เดียวกับไฟล์ตัวอย่างของหน้านำเข้า มีข้อมูลปัจจุบันของห้องครบ แก้แล้วนำเข้ากลับได้ */}
        <a
          href={`/api/classrooms/${classroomId}/import/students/template`}
          download
          className={buttonVariants({ variant: 'outline' })}
        >
          <FileDown aria-hidden="true" />
          นำออก
        </a>
        <Button onClick={() => setAdding(true)}>เพิ่มนักเรียน</Button>
      </PageHeader>

      <ClassroomTabs classroomId={classroomId} active="students" />

      <FormError message={rowAction.error} />

      <QueryState query={students} empty="ยังไม่มีนักเรียนในห้องนี้">
        {(list) => (
          <ul className="grid gap-2">
            {list.map((s) => (
              <li key={s.studentId} className="flex flex-wrap items-center gap-3 rounded-lg border bg-card p-3">
                <span className="w-8 shrink-0 text-center text-sm text-muted-foreground">{s.no}</span>

                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">
                    {s.firstName} {s.lastName}
                  </p>
                  <p className="truncate text-xs text-muted-foreground">
                    {s.studentCode}
                    {' · '}
                    {s.hasGoogle ? 'เชื่อมบัญชี Google แล้ว' : 'ยังไม่เคยเข้าสู่ระบบ'}
                  </p>
                </div>

                <div className="flex shrink-0 flex-wrap gap-2">
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(s)}>
                    แก้ไข
                  </Button>
                  {s.hasGoogle && (
                    <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => unlink(s)}>
                      ยกเลิกการเชื่อม
                    </Button>
                  )}
                  <Button variant="ghost" size="sm" disabled={rowAction.busy} onClick={() => takeOut(s)}>
                    ย้ายออก
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <Modal
        open={adding}
        onOpenChange={setAdding}
        title="เพิ่มนักเรียน"
        description="ถ้ารหัสนักเรียนนี้มีในระบบแล้ว ระบบจะใช้ข้อมูลเดิมเพิ่มเข้าห้องนี้ ไม่สร้างนักเรียนซ้ำ"
        onSubmit={create}
        submitLabel="เพิ่ม"
        busy={form.busy}
        error={form.error}
      >
        <Field label="รหัสนักเรียน" name="studentCode" inputMode="numeric" autoComplete="off" required />
        <Field label="ชื่อ" name="firstName" required />
        <Field label="นามสกุล" name="lastName" required />
        <Field label="เลขที่ในห้อง" name="no" type="number" min={1} max={999} defaultValue={1} required />
      </Modal>

      {/* key ทำให้ modal สร้างใหม่ทุกครั้งที่เปลี่ยนคน defaultValue จะได้อัปเดตตาม */}
      <Modal
        key={editing?.studentId}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="แก้ไขนักเรียน"
        description={editing ? `รหัสนักเรียน ${editing.studentCode} (แก้ไม่ได้)` : undefined}
        onSubmit={save}
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อ" name="firstName" defaultValue={editing?.firstName} required />
        <Field label="นามสกุล" name="lastName" defaultValue={editing?.lastName} required />
        <Field label="เลขที่ในห้อง" name="no" type="number" min={1} max={999} defaultValue={editing?.no} required />
      </Modal>

      {dialog}
    </>
  )
}
