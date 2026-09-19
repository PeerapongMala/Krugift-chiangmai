import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row, RowActions } from '@/components/Rows'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

type Term = { id: number; name: string; createdAt: string; publicScores: boolean; classroomCount: number }

export default function Terms() {
  const queryClient = useQueryClient()
  const terms = useQuery({ queryKey: qk.terms, queryFn: () => api<Term[]>('/terms') })
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<Term | null>(null)
  const [purging, setPurging] = useState<Term | null>(null)
  const form = useSubmit()
  const rowAction = useSubmit()
  const { confirm, dialog } = useConfirm()

  const refresh = () => queryClient.invalidateQueries({ queryKey: qk.terms })

  async function create(f: FormData) {
    const name = String(f.get('name') ?? '')
    if (!form.check(validate.name(name, 'ชื่อภาคเรียน'))) return

    const ok = await form.run(async () => {
      await api('/terms', { method: 'POST', json: { name } })
      await refresh()
    })
    if (ok) setAdding(false)
  }

  async function rename(f: FormData) {
    if (!editing) return
    const name = String(f.get('name') ?? '')
    if (!form.check(validate.name(name, 'ชื่อภาคเรียน'))) return

    const ok = await form.run(async () => {
      await api(`/terms/${editing.id}`, { method: 'PATCH', json: { name } })
      await refresh()
    })
    if (ok) setEditing(null)
  }

  // เปิด/ปิดหน้าดูคะแนนด่วน (ไม่ต้องล็อกอิน) ของภาคเรียนนี้
  async function togglePublic(term: Term) {
    await rowAction.run(async () => {
      await api(`/terms/${term.id}/public-scores`, { method: 'PATCH', json: { enabled: !term.publicScores } })
      await refresh()
    })
  }

  async function remove(term: Term) {
    const ok = await confirm({
      title: 'ลบภาคเรียนนี้?',
      description: term.name,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/terms/${term.id}`, { method: 'DELETE' })
      await refresh()
    })
  }

  async function purge(f: FormData) {
    if (!purging) return
    const name = String(f.get('name') ?? '')

    const ok = await form.run(async () => {
      await api(`/terms/${purging.id}/delete-all`, { method: 'POST', json: { name } })
      await refresh()
    })
    if (ok) setPurging(null)
  }

  return (
    <>
      <PageHeader title="ภาคเรียน">
        <Button onClick={() => setAdding(true)}>สร้างภาคเรียน</Button>
      </PageHeader>

      <FormError message={rowAction.error} />

      <QueryState query={terms} empty="ยังไม่มีภาคเรียน">
        {(list) => (
          <Rows>
            {list.map((term) => (
              <Row key={term.id}>
                <Link to={routes.term(term.id)} className="min-w-28 flex-1 hover:underline">
                  <p className="truncate font-medium">{term.name}</p>
                  <p className="text-xs text-muted-foreground">
                    {term.classroomCount > 0 ? `${term.classroomCount} ห้องเรียน` : 'ยังไม่มีห้องเรียน'}
                  </p>
                </Link>

                <RowActions>
                  {/* ห่อด้วย label กดที่ข้อความก็สลับได้ ไม่ต้องเล็งสวิตช์เล็ก ๆ บนมือถือ */}
                  <label className="flex h-10 cursor-pointer items-center gap-2 rounded-md px-2 text-sm">
                    <Switch
                      checked={term.publicScores}
                      onCheckedChange={() => togglePublic(term)}
                      disabled={rowAction.busy}
                    />
                    ดูคะแนนด่วน
                  </label>
                  <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => setEditing(term)}>
                    เปลี่ยนชื่อ
                  </Button>
                  <Button
                    variant="destructive"
                    size="sm"
                    disabled={rowAction.busy}
                    onClick={() => (term.classroomCount > 0 ? setPurging(term) : remove(term))}
                  >
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
        title="สร้างภาคเรียน"
        onSubmit={create}
        submitLabel="สร้าง"
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อภาคเรียน" name="name" placeholder="1/2569" required />
      </Modal>

      {/* key ทำให้ modal สร้างใหม่ทุกครั้งที่เปลี่ยนภาคเรียน defaultValue จะได้อัปเดตตาม */}
      <Modal
        key={editing?.id}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="เปลี่ยนชื่อภาคเรียน"
        onSubmit={rename}
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อภาคเรียน" name="name" defaultValue={editing?.name} required />
      </Modal>

      {/* ลบภาคเรียนที่มีข้อมูลแล้ว คะแนนทั้งภาคเรียนหายถาวร จึงให้พิมพ์ชื่อยืนยันแทนปุ่มเดียวจบ */}
      <Modal
        key={`purge-${purging?.id}`}
        open={purging !== null}
        onOpenChange={(open) => !open && setPurging(null)}
        title="ลบภาคเรียนพร้อมข้อมูลทั้งหมด?"
        description={
          purging && `${purging.name} · ห้องเรียน ${purging.classroomCount} ห้อง พร้อมรายชื่อ รายการคะแนน คะแนน และคำถามทั้งหมด`
        }
        onSubmit={purge}
        submitLabel="ลบทั้งหมด"
        destructive
        busy={form.busy}
        error={form.error}
      >
        <Field label={`พิมพ์ “${purging?.name ?? ''}” เพื่อยืนยัน`} name="name" autoComplete="off" required />
      </Modal>

      {dialog}
    </>
  )
}
