import { useQuery, useQueryClient } from '@tanstack/react-query'
import { FileUp } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { Meta } from '@/components/Meta'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row, RowActions } from '@/components/Rows'
import { Button, buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

type Classroom = { id: number; name: string; studentCount: number; itemCount: number }

export default function Classrooms() {
  const termId = Number(useParams().termId)
  const queryClient = useQueryClient()
  const rooms = useQuery({
    queryKey: qk.classrooms(termId),
    queryFn: () => api<Classroom[]>(`/terms/${termId}/classrooms`),
    enabled: Number.isInteger(termId),
  })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Classroom | null>(null)
  const form = useSubmit()
  const rowAction = useSubmit()
  const { confirm, dialog } = useConfirm()

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: qk.classrooms(termId) }),
      // จำนวนห้องบนหน้ารายการภาคเรียนต้องอัปเดตตามด้วย
      queryClient.invalidateQueries({ queryKey: qk.terms }),
    ])

  async function create(f: FormData) {
    const name = String(f.get('name') ?? '')
    if (!form.check(validate.name(name, 'ชื่อห้องเรียน'))) return

    const ok = await form.run(async () => {
      await api(`/terms/${termId}/classrooms`, { method: 'POST', json: { name } })
      await refresh()
    })
    if (ok) setAdding(false)
  }

  async function rename(f: FormData) {
    if (!editing) return
    const name = String(f.get('name') ?? '')
    if (!form.check(validate.name(name, 'ชื่อห้องเรียน'))) return

    const ok = await form.run(async () => {
      await api(`/classrooms/${editing.id}`, { method: 'PATCH', json: { name } })
      await refresh()
    })
    if (ok) setEditing(null)
  }

  async function remove(room: Classroom) {
    const ok = await confirm({
      title: 'ลบห้องเรียนนี้?',
      description: room.name,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/classrooms/${room.id}`, { method: 'DELETE' })
      await refresh()
    })
  }

  return (
    <>
      <Link to={routes.teacher} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← กลับไปหน้าภาคเรียน
      </Link>

      <PageHeader title="ห้องเรียน">
        {/* ทางลัดไปจัดการรายการคะแนนทีเดียวทุกห้อง ไม่ต้องเข้าไปไล่กดทีละห้อง */}
        <Link to={routes.termItems(termId)} className={buttonVariants({ variant: 'outline' })}>
          รายการคะแนนทุกห้อง
        </Link>
        <Link to={routes.termImport(termId)} className={buttonVariants({ variant: 'outline' })}>
          <FileUp aria-hidden="true" />
          นำเข้าไฟล์ครู
        </Link>
        <Button onClick={() => setAdding(true)}>เพิ่มห้องเรียน</Button>
      </PageHeader>

      <FormError message={rowAction.error} />

      <QueryState query={rooms} empty="ยังไม่มีห้องเรียนในภาคเรียนนี้">
        {(list) => (
          <Rows>
            {list.map((room) => (
              <Row key={room.id}>
                <Link to={routes.classroom(room.id)} className="min-w-0 flex-1 hover:underline">
                  <p className="truncate font-medium">{room.name}</p>
                  <Meta>
                    <span>นักเรียน {room.studentCount} คน</span>
                    <span>รายการคะแนน {room.itemCount} รายการ</span>
                  </Meta>
                </Link>

                <RowActions>
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(room)}>
                    เปลี่ยนชื่อ
                  </Button>
                  <Button variant="destructive" size="sm" disabled={rowAction.busy} onClick={() => remove(room)}>
                    ลบ
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
        title="เพิ่มห้องเรียน"
        onSubmit={create}
        submitLabel="เพิ่ม"
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อห้องเรียน" name="name" placeholder="ม.2/1" required />
      </Modal>

      <Modal
        key={editing?.id}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="เปลี่ยนชื่อห้องเรียน"
        onSubmit={rename}
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อห้องเรียน" name="name" defaultValue={editing?.name} required />
      </Modal>

      {dialog}
    </>
  )
}
