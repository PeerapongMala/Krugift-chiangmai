import { useQuery, useQueryClient } from '@tanstack/react-query'
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
import { TabToolbar } from '@/components/TabToolbar'
import { TermTabs } from '@/components/TermTabs'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { firstError, validate } from '@/lib/validate'
import { useTermName } from '@/lib/useTermName'

/** รายการคะแนนชื่อเดียวกันของทุกห้องในภาคเรียน รวมเป็นแถวเดียว */
type TermItem = {
  name: string
  classrooms: number
  /** null = คละกัน บางห้องซ่อน บางห้องไม่ซ่อน */
  visible: boolean | null
  /** null = คะแนนเต็มไม่เท่ากันทุกห้อง */
  maxScore: number | null
  scoredCount: number
  fractionalCount: number
}

type RoundPreview = { count: number; items: string[] }

/**
 * จัดการรายการคะแนนทั้งภาคเรียนในหน้าเดียว
 * ครูนำเข้าไฟล์ทีเดียว 8 ห้อง ทุกห้องมีรายการชื่อเดียวกัน การไล่กดทีละห้องจึงเป็นงานซ้ำ 8 รอบ
 */
export default function TermItems() {
  const termId = Number(useParams().termId)
  const queryClient = useQueryClient()
  const items = useQuery({
    queryKey: qk.termItems(termId),
    queryFn: () => api<TermItem[]>(`/terms/${termId}/items`),
    enabled: Number.isInteger(termId),
  })
  const action = useSubmit()
  const form = useSubmit()
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<TermItem | null>(null)
  const { confirm, dialog } = useConfirm()
  /** ผลของการปัดคะแนน ถ้าไม่บอกครูจะไม่เห็นว่าเกิดอะไรขึ้น เพราะตัวเลขอยู่อีกหน้า */
  const [note, setNote] = useState('')
  const termName = useTermName(termId)

  // รายการกับคะแนนของทุกห้องเปลี่ยนพร้อมกัน ล้าง cache ของห้องทั้งหมดด้วย
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: qk.termItems(termId) }),
      queryClient.invalidateQueries({ queryKey: ['classrooms'] }),
    ])

  async function toggleVisible(item: TermItem) {
    // คละกันอยู่ = กดครั้งแรกให้ตั้งเหมือนกันทั้งภาคเรียนโดยเปิดให้นักเรียนเห็น
    const visible = item.visible === null ? true : !item.visible
    await action.run(async () => {
      await api(`/terms/${termId}/items/visibility`, { method: 'PATCH', json: { name: item.name, visible } })
      await refresh()
    })
  }

  /** รายการที่เพิ่มจากหน้านี้ถูกสร้างให้ทุกห้องในภาคเรียน ครูจะได้ไม่ต้องไล่เพิ่มทีละห้อง */
  async function create(f: FormData) {
    const name = String(f.get('name') ?? '')
    const maxScore = Number(f.get('maxScore'))
    if (!form.check(firstError(validate.name(name, 'ชื่อรายการ'), validate.maxScore(maxScore)))) return

    const ok = await form.run(async () => {
      const done = await api<{ classrooms: number }>(`/terms/${termId}/items`, {
        method: 'POST',
        json: { name, maxScore, teacherOnly: f.get('teacherOnly') === 'on' },
      })
      await refresh()
      setNote(`เพิ่ม "${name.trim()}" ให้ ${done.classrooms} ห้องแล้ว`)
    })
    if (ok) setAdding(false)
  }

  async function save(f: FormData) {
    if (!editing) return
    const newName = String(f.get('name') ?? '')
    const maxScore = Number(f.get('maxScore'))
    if (!form.check(firstError(validate.name(newName, 'ชื่อรายการ'), validate.maxScore(maxScore)))) return

    const ok = await form.run(async () => {
      const done = await api<{ classrooms: number }>(`/terms/${termId}/items`, {
        method: 'PATCH',
        json: { name: editing.name, newName, maxScore },
      })
      await refresh()
      setNote(`แก้ "${newName.trim()}" ใน ${done.classrooms} ห้องแล้ว`)
    })
    if (ok) setEditing(null)
  }

  async function discard(item: TermItem) {
    const ok = await confirm({
      title: `ลบ "${item.name}" ทุกห้อง?`,
      description: `ลบออกจาก ${item.classrooms} ห้องในภาคเรียนนี้${
        item.scoredCount > 0 ? `
คะแนนที่กรอกไว้ ${item.scoredCount} ช่อง ประวัติการแก้คะแนน และคำถามของรายการนี้จะถูกลบไปด้วย กู้คืนไม่ได้` : ''
      }`,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    setNote('')
    await action.run(async () => {
      await api(`/terms/${termId}/items?name=${encodeURIComponent(item.name)}`, { method: 'DELETE' })
      await refresh()
      setNote(`ลบ "${item.name}" ออกจาก ${item.classrooms} ห้องแล้ว`)
    })
  }

  async function roundScores() {
    setNote('')
    const preview = await api<RoundPreview>(`/terms/${termId}/round-scores`)
    if (preview.count === 0) {
      setNote('คะแนนของทุกรายการเป็นจำนวนเต็มอยู่แล้ว ไม่มีช่องไหนต้องปัด')
      return
    }

    const ok = await confirm({
      title: `ปัดคะแนน ${preview.count} ช่องเป็นจำนวนเต็ม?`,
      description: `มีผลกับ ${preview.items.join(' ')} ทุกห้องในภาคเรียนนี้
7.33 จะกลายเป็น 7 และ 7.5 จะกลายเป็น 8
คะแนนดิบที่ซ่อนจากนักเรียนไม่ถูกแตะ ค่าเดิมยังดูย้อนหลังได้ในประวัติการแก้คะแนน`,
      confirmLabel: 'ปัดคะแนน',
      destructive: true,
    })
    if (!ok) return

    await action.run(async () => {
      const done = await api<{ changed: number }>(`/terms/${termId}/round-scores`, { method: 'POST' })
      await refresh()
      setNote(`ปัดคะแนนแล้ว ${done.changed} ช่อง`)
    })
  }

  return (
    <>
      <Link to={routes.teacher} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← กลับไปหน้าภาคเรียน
      </Link>

      <PageHeader title={termName || 'ภาคเรียน'} description="สวิตช์ในหน้านี้มีผลกับทุกห้องพร้อมกัน" />

      <TermTabs termId={termId} active="items" />

      <TabToolbar>
        <Button variant="outline" disabled={action.busy} onClick={roundScores}>
          ปัดคะแนนเป็นจำนวนเต็ม
        </Button>
        <Button disabled={action.busy} onClick={() => setAdding(true)}>
          เพิ่มรายการ
        </Button>
      </TabToolbar>

      {note && <p className="mb-3 rounded-lg bg-accent px-3 py-2 text-sm">{note}</p>}

      <FormError message={action.error} />

      <QueryState query={items} empty="ยังไม่มีรายการคะแนนในภาคเรียนนี้">
        {(list) => (
          <Rows>
            {list.map((item) => (
              <Row key={item.name}>
                <div className="min-w-0 flex-1 basis-full sm:basis-auto">
                  <p className="truncate font-medium">{item.name}</p>
                  <Meta>
                    <span>{item.classrooms} ห้อง</span>
                    <span>{item.maxScore === null ? 'คะแนนเต็มไม่เท่ากัน' : `เต็ม ${item.maxScore} คะแนน`}</span>
                    <span>{item.scoredCount > 0 ? `กรอกแล้ว ${item.scoredCount} คน` : 'ยังไม่ได้กรอกคะแนน'}</span>
                    {item.fractionalCount > 0 && <span className="text-primary">มีเศษ {item.fractionalCount} ช่อง</span>}
                  </Meta>
                </div>

                <RowActions>
                  {/* ห่อด้วย label กดที่ข้อความก็สลับได้ ไม่ต้องเล็งสวิตช์เล็ก ๆ บนมือถือ */}
                  <label className="flex h-10 cursor-pointer items-center gap-2 rounded-md px-2 text-sm">
                    <Switch
                      checked={item.visible === true}
                      onCheckedChange={() => toggleVisible(item)}
                      disabled={action.busy}
                    />
                    {item.visible === null ? 'บางห้องซ่อนอยู่' : 'นักเรียนเห็น'}
                  </label>
                  <Button variant="outline" size="sm" disabled={action.busy} onClick={() => setEditing(item)}>
                    แก้ไข
                  </Button>
                  <Button variant="destructive" size="sm" disabled={action.busy} onClick={() => discard(item)}>
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
        title="เพิ่มรายการคะแนนทุกห้อง"
        onSubmit={create}
        submitLabel="เพิ่ม"
        busy={form.busy}
        error={form.error}
      >
        <Field label="ชื่อรายการ" name="name" placeholder="สอบปลายภาค" required />
        <Field label="คะแนนเต็ม" name="maxScore" type="number" min={0.01} step={0.01} defaultValue={10} required />
        <label className="flex min-h-10 cursor-pointer items-center gap-3 text-sm">
          <input type="checkbox" name="teacherOnly" className="size-5 shrink-0 accent-primary" />
          ซ่อนจากนักเรียน (เห็นเฉพาะครู)
        </label>
      </Modal>

      {/* key ทำให้ modal สร้างใหม่ทุกครั้งที่เปลี่ยนรายการ defaultValue จะได้อัปเดตตาม */}
      <Modal
        key={editing?.name}
        open={editing !== null}
        onOpenChange={(open) => !open && setEditing(null)}
        title="แก้ไขรายการคะแนนทุกห้อง"
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
          defaultValue={editing?.maxScore ?? undefined}
          required
        />
      </Modal>

      {dialog}
    </>
  )
}
