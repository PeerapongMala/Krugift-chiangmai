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
import { Switch } from '@/components/ui/switch'
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
    return {
      name: String(f.get('name') ?? ''),
      maxScore: Number(f.get('maxScore')),
      teacherOnly: f.get('teacherOnly') === 'on',
    }
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

  // สวิตช์บนแถว: ปิด = นักเรียนไม่เห็นรายการนี้และสอบถามไม่ได้ ครูยังกรอกคะแนนได้ตามปกติ
  async function toggleVisible(item: Item) {
    await rowAction.run(async () => {
      await api(`/items/${item.id}/visibility`, { method: 'PATCH', json: { visible: item.teacherOnly } })
      await refresh()
    })
  }

  async function discard(item: Item) {
    // กรอกคะแนนไปแล้วต้องบอกให้ชัดว่าคะแนนจะหายไปด้วยกี่คน ไม่ใช่แค่ห้ามลบแล้วให้ไปล้างเองทีละช่อง
    const scored = item.scoredCount > 0
    const ok = await confirm({
      title: scored ? `ลบรายการนี้พร้อมคะแนน ${item.scoredCount} คน?` : 'ลบรายการคะแนนนี้?',
      description: scored
        ? `${item.name}
คะแนนของนักเรียน ${item.scoredCount} คนและประวัติการแก้คะแนนของรายการนี้จะถูกลบไปด้วย กู้คืนไม่ได้`
        : item.name,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/items/${item.id}?withScores=${scored}`, { method: 'DELETE' })
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
                {/* ชื่อรายการยาว (เช่น "จำนวนเต็ม สอบ 1 (คะแนนดิบ)") บนมือถือกินเต็มบรรทัด
                    สวิตช์กับปุ่มจึงตกไปบรรทัดล่าง ไม่ไปบีบชื่อจนอ่านไม่ออก */}
                <div className="min-w-0 flex-1 basis-full sm:basis-auto">
                  <p className="truncate font-medium">{item.name}</p>
                  <Meta>
                    <span>เต็ม {item.maxScore} คะแนน</span>
                    <span>{item.scoredCount > 0 ? `กรอกแล้ว ${item.scoredCount} คน` : 'ยังไม่ได้กรอกคะแนน'}</span>
                  </Meta>
                </div>

                <RowActions>
                  {/* ห่อด้วย label กดที่ข้อความก็สลับได้ ไม่ต้องเล็งสวิตช์เล็ก ๆ บนมือถือ */}
                  <label className="flex h-10 cursor-pointer items-center gap-2 rounded-md px-2 text-sm">
                    <Switch
                      checked={!item.teacherOnly}
                      onCheckedChange={() => toggleVisible(item)}
                      disabled={rowAction.busy}
                    />
                    นักเรียนเห็น
                  </label>
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
        <TeacherOnlyField />
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
        <TeacherOnlyField defaultChecked={editing?.teacherOnly} />
      </Modal>

      {dialog}
    </>
  )
}

/** ติ๊กแล้วนักเรียนจะไม่เห็นรายการนี้และสอบถามไม่ได้ · ใช้กับคะแนนดิบหรือคะแนนที่ยังไม่อยากประกาศ */
function TeacherOnlyField({ defaultChecked }: { defaultChecked?: boolean }) {
  return (
    <label className="flex min-h-10 cursor-pointer items-center gap-3 text-sm">
      <input
        type="checkbox"
        name="teacherOnly"
        defaultChecked={defaultChecked}
        className="size-5 shrink-0 accent-primary"
      />
      ซ่อนจากนักเรียน (เห็นเฉพาะครู)
    </label>
  )
}
