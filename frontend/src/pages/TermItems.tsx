import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { FormError } from '@/components/FormError'
import { Meta } from '@/components/Meta'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row, RowActions } from '@/components/Rows'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'

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
  const { confirm, dialog } = useConfirm()
  /** ผลของการปัดคะแนน ถ้าไม่บอกครูจะไม่เห็นว่าเกิดอะไรขึ้น เพราะตัวเลขอยู่อีกหน้า */
  const [note, setNote] = useState('')

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
      <Link to={routes.term(termId)} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← กลับไปหน้าห้องเรียน
      </Link>

      <PageHeader title="รายการคะแนนทั้งภาคเรียน">
        <Button variant="outline" disabled={action.busy} onClick={roundScores}>
          ปัดคะแนนเป็นจำนวนเต็ม
        </Button>
      </PageHeader>

      <p className="mb-3 text-sm text-muted-foreground">สวิตช์ในหน้านี้มีผลกับทุกห้องพร้อมกัน</p>

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
                </RowActions>
              </Row>
            ))}
          </Rows>
        )}
      </QueryState>

      {dialog}
    </>
  )
}
