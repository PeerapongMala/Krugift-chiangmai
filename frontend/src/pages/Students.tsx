import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { ClassroomTabs } from '@/components/ClassroomTabs'
import { ExportButton } from '@/components/ExportButton'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { Meta } from '@/components/Meta'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row, RowActions } from '@/components/Rows'
import { TabToolbar } from '@/components/TabToolbar'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { firstError, validate } from '@/lib/validate'

type Student = {
  studentId: number
  no: number
  studentCode: string
  title: string
  firstName: string
  lastName: string
  nickname: string
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
      title: String(f.get('title') ?? ''),
      firstName: String(f.get('firstName') ?? ''),
      lastName: String(f.get('lastName') ?? ''),
      nickname: String(f.get('nickname') ?? ''),
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
        json: { title: v.title, firstName: v.firstName, lastName: v.lastName, nickname: v.nickname, no: v.no },
      })
      await refresh()
    })
    if (ok) setEditing(null)
  }

  async function takeOut(s: Student) {
    const ok = await confirm({
      title: 'ย้ายนักเรียนออกจากห้องนี้?',
      description: `${s.firstName} ${s.lastName} (${s.studentCode})`,
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
      description: `${s.firstName} ${s.lastName}`,
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

      <PageHeader title="นักเรียนในห้อง" />

      <ClassroomTabs classroomId={classroomId} active="students" />

      <TabToolbar>
        <ExportButton classroomId={classroomId} />
        <Button onClick={() => setAdding(true)}>เพิ่มนักเรียน</Button>
      </TabToolbar>

      <FormError message={rowAction.error} />

      <QueryState query={students} empty="ยังไม่มีนักเรียนในห้องนี้">
        {(list) => (
          <Rows>
            {list.map((s) => (
              <Row key={s.studentId}>
                <span className="w-8 shrink-0 text-center text-sm text-muted-foreground">{s.no}</span>

                <div className="min-w-0 flex-1">
                  {/* ไม่ใส่คำนำหน้าในรายการ ชื่อไทยยาวอยู่แล้ว จอ 400px จะโดนตัดจนอ่านนามสกุลไม่ออก */}
                  <p className="truncate font-medium">
                    {s.firstName} {s.lastName}
                    {s.nickname && <span className="font-normal text-muted-foreground"> ({s.nickname})</span>}
                  </p>
                  <Meta>
                    <span>{s.studentCode}</span>
                    <span>{s.hasGoogle ? 'เชื่อมบัญชี Google แล้ว' : 'ยังไม่เคยเข้าสู่ระบบ'}</span>
                  </Meta>
                </div>

                <RowActions>
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(s)}>
                    แก้ไข
                  </Button>
                  {s.hasGoogle && (
                    <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => unlink(s)}>
                      ยกเลิกการเชื่อม
                    </Button>
                  )}
                  <Button variant="destructive" size="sm" disabled={rowAction.busy} onClick={() => takeOut(s)}>
                    ย้ายออก
                  </Button>
                </RowActions>
              </Row>
            ))}
          </Rows>
        )}
      </QueryState>

      <Modal
        open={adding}
        onOpenChange={setAdding}
        title="เพิ่มนักเรียน"
        onSubmit={create}
        submitLabel="เพิ่ม"
        busy={form.busy}
        error={form.error}
      >
        <Field label="รหัสนักเรียน" name="studentCode" inputMode="numeric" autoComplete="off" required />
        <Field label="คำนำหน้า" name="title" placeholder="เด็กหญิง" autoComplete="off" />
        <Field label="ชื่อ" name="firstName" required />
        <Field label="นามสกุล" name="lastName" required />
        <Field label="ชื่อเล่น" name="nickname" autoComplete="off" />
        <Field label="เลขที่ในห้อง" name="no" type="number" min={1} max={999} defaultValue={1} required />
      </Modal>

      {/* key ทำให้ modal สร้างใหม่ทุกครั้งที่เปลี่ยนคน defaultValue จะได้อัปเดตตาม */}
      <Modal
        key={editing?.studentId}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="แก้ไขนักเรียน"
        description={editing ? `รหัสนักเรียน ${editing.studentCode}` : undefined}
        onSubmit={save}
        busy={form.busy}
        error={form.error}
      >
        <Field label="คำนำหน้า" name="title" defaultValue={editing?.title} autoComplete="off" />
        <Field label="ชื่อ" name="firstName" defaultValue={editing?.firstName} required />
        <Field label="นามสกุล" name="lastName" defaultValue={editing?.lastName} required />
        <Field label="ชื่อเล่น" name="nickname" defaultValue={editing?.nickname} autoComplete="off" />
        <Field label="เลขที่ในห้อง" name="no" type="number" min={1} max={999} defaultValue={editing?.no} required />
      </Modal>

      {dialog}
    </>
  )
}
