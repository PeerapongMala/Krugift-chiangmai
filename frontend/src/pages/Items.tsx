import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { firstError, validate } from '@/lib/validate'

type Item = { id: number; name: string; maxScore: number; sortOrder: number; scoredCount: number }

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
      description:
        item.scoredCount > 0
          ? `${item.name} · มีคะแนนที่กรอกไว้ ${item.scoredCount} คน ระบบจะไม่ให้ลบจนกว่าจะล้างคะแนนหมด`
          : `${item.name} · ยังไม่มีใครได้คะแนน ลบได้เลย`,
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
      <div className="mb-3 flex flex-wrap gap-4 text-sm text-muted-foreground">
        <Link to={routes.classroom(classroomId)} className="hover:text-foreground">
          ← นักเรียนในห้อง
        </Link>
        <Link to={routes.classroomScores(classroomId)} className="hover:text-foreground">
          ตารางคะแนน →
        </Link>
      </div>

      <PageHeader title="รายการคะแนน" description="สิ่งที่เอาไว้ให้คะแนน เช่น สอบกลางภาค 20 คะแนน">
        <Button onClick={() => setAdding(true)}>เพิ่มรายการ</Button>
      </PageHeader>

      <FormError message={rowAction.error} />

      <QueryState query={items} empty="ยังไม่มีรายการคะแนนในห้องนี้">
        {(list) => (
          <ul className="grid gap-2">
            {list.map((item) => (
              <li key={item.id} className="flex flex-wrap items-center gap-3 rounded-lg border bg-card p-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">{item.name}</p>
                  <p className="text-xs text-muted-foreground">
                    เต็ม {item.maxScore} คะแนน
                    {' · '}
                    {item.scoredCount > 0 ? `กรอกแล้ว ${item.scoredCount} คน` : 'ยังไม่มีใครได้คะแนน'}
                  </p>
                </div>

                <div className="flex shrink-0 gap-2">
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(item)}>
                    แก้ไข
                  </Button>
                  <Button variant="ghost" size="sm" disabled={rowAction.busy} onClick={() => discard(item)}>
                    ลบ
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
        description={
          editing && editing.scoredCount > 0
            ? `มีคะแนนกรอกไว้แล้ว ${editing.scoredCount} คน · ลดคะแนนเต็มต่ำกว่าคะแนนสูงสุดที่กรอกไว้ไม่ได้`
            : undefined
        }
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
