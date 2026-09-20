import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { ClassroomTabs } from '@/components/ClassroomTabs'
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

type Item = { id: number; name: string; maxScore: number; sortOrder: number; scoredCount: number; teacherOnly: boolean }

export default function Items() {
  const classroomId = Number(useParams().classroomId)
  const queryClient = useQueryClient()
  const items = useQuery({
    queryKey: qk.items(classroomId),
    queryFn: () => api<Item[]>(`/classrooms/${classroomId}/items`),
    enabled: Number.isInteger(classroomId),
  })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Item | null>(null)
  const form = useSubmit()
  const rowAction = useSubmit()
  const { confirm, dialog } = useConfirm()

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: qk.items(classroomId) }),
      queryClient.invalidateQueries({ queryKey: qk.scores(classroomId) }),
      queryClient.invalidateQueries({ queryKey: qk.terms }),
    ])

  function read(f: FormData) {
    return { name: String(f.get('name') ?? ''), maxScore: Number(f.get('maxScore')) }
  }

  async function create(f: FormData) {
    const v = read(f)
    if (!form.check(firstError(validate.name(v.name, 'ชื่อรายการ'), validate.maxScore(v.maxScore)))) return

    const ok = await form.run(async () => {
      await api(`/classrooms/${classroomId}/items`, { method: 'POST', json: v })
      await refresh()
    })
    if (ok) setAdding(false)
  }

  async function save(f: FormData) {
    if (!editing) return
    const v = read(f)
    if (!form.check(firstError(validate.name(v.name, 'ชื่อรายการ'), validate.maxScore(v.maxScore)))) return

    const ok = await form.run(async () => {
      await api(`/items/${editing.id}`, { method: 'PATCH', json: v })
      await refresh()
    })
    if (ok) setEditing(null)
  }

  async function discard(item: Item) {
    const ok = await confirm({
      title: 'ลบรายการคะแนนนี้?',
      description: item.name,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/items/${item.id}`, { method: 'DELETE' })
      await refresh()
    })
  }

  return (
    <>
      <Link to={routes.teacher} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← ภาคเรียน
      </Link>

      <PageHeader title="รายการคะแนน" />

      <ClassroomTabs classroomId={classroomId} active="items" />

      <TabToolbar>
        <Button onClick={() => setAdding(true)}>เพิ่มรายการ</Button>
      </TabToolbar>

      <FormError message={rowAction.error} />

      <QueryState query={items} empty="ยังไม่มีรายการคะแนนในห้องนี้">
        {(list) => (
          <Rows>
            {list.map((item) => (
              <Row key={item.id}>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">{item.name}</p>
                  <Meta>
                    {/* รายการคะแนนดิบที่นำเข้ามาคู่กัน เด็กไม่เห็นและสอบถามไม่ได้ */}
                    {item.teacherOnly && <span className="text-primary">เฉพาะครู</span>}
                    <span>เต็ม {item.maxScore} คะแนน</span>
                    <span>{item.scoredCount > 0 ? `กรอกแล้ว ${item.scoredCount} คน` : 'ยังไม่ได้กรอกคะแนน'}</span>
                  </Meta>
                </div>

                <RowActions>
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(item)}>
                    แก้ไข
                  </Button>
                  <Button variant="destructive" size="sm" disabled={rowAction.busy} onClick={() => discard(item)}>
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
        title="เพิ่มรายการคะแนน"
        onSubmit={create}
        submitLabel="เพิ่ม"
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อรายการ" name="name" placeholder="สอบกลางภาค" required />
        <Field label="คะแนนเต็ม" name="maxScore" type="number" min={0.01} step={0.01} defaultValue={10} required />
      </Modal>

      {/* key ทำให้ modal สร้างใหม่ทุกครั้งที่เปลี่ยนรายการ defaultValue จะได้อัปเดตตาม */}
      <Modal
        key={editing?.id}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="แก้ไขรายการคะแนน"
        onSubmit={save}
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อรายการ" name="name" defaultValue={editing?.name} required />
        <Field
          label="คะแนนเต็ม"
          name="maxScore"
          type="number"
          min={0.01}
          step={0.01}
          defaultValue={editing?.maxScore}
          required
        />
      </Modal>

      {dialog}
    </>
  )
}
