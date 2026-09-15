import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Modal } from '@/components/Modal'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { ScoreList, type ScoreRow } from '@/components/ScoreList'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

type MyRoom = { classroomId: number; classroom: string; term: string; no: number; items: ScoreRow[] }

type AppealTarget = { item: ScoreRow; classroom: string }

/** หน้าแรกของนักเรียนที่ล็อกอินแล้ว · server ดึงคะแนนจาก cookie ของเจ้าตัวเท่านั้น */
export default function StudentHome() {
  const rooms = useQuery({ queryKey: qk.myScores, queryFn: () => api<MyRoom[]>('/me/scores') })
  const [target, setTarget] = useState<AppealTarget | null>(null)
  const form = useSubmit()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  function openModal(next: AppealTarget) {
    form.setError('')
    setTarget(next)
  }

  async function openAppeal(data: FormData) {
    const itemId = target?.item.id
    if (!itemId) return
    const body = String(data.get('body') ?? '')
    if (!form.check(validate.message(body))) return

    let appealId = 0
    const ok = await form.run(async () => {
      const created = await api<{ id: number }>('/appeals', { method: 'POST', json: { itemId, body } })
      appealId = created.id
      await queryClient.invalidateQueries({ queryKey: qk.appeals })
    })
    if (!ok) return
    setTarget(null)
    navigate(routes.appeal(routes.studentAppeals, appealId))
  }

  return (
    <>
      <PageHeader title="คะแนนของฉัน" description="คะแนนทุกห้องที่เรียน เรียงจากภาคเรียนล่าสุด · คะแนนไม่ตรง กด ท้วง ข้างรายการนั้น" />

      <QueryState query={rooms} empty="ยังไม่มีห้องเรียนที่มีชื่อคุณอยู่ ถ้าคิดว่าผิด ให้แจ้งครู">
        {(list) => (
          <div className="grid gap-4 md:grid-cols-2">
            {list.map((room) => (
              <section
                key={room.classroomId}
                aria-labelledby={`room-${room.classroomId}`}
                className="rounded-lg border bg-card p-4"
              >
                <h2 id={`room-${room.classroomId}`} className="font-medium">
                  {room.classroom}
                </h2>
                <p className="mb-3 text-xs text-muted-foreground">
                  ภาคเรียน {room.term} · เลขที่ {room.no}
                </p>
                <ScoreList
                  items={room.items}
                  action={(item) => (
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label={`ท้วงคะแนน ${item.name}`}
                      onClick={() => openModal({ item, classroom: room.classroom })}
                    >
                      ท้วง
                    </Button>
                  )}
                />
              </section>
            ))}
          </div>
        )}
      </QueryState>

      {/* key ทำให้ช่องเหตุผลว่างใหม่ทุกครั้งที่เปลี่ยนรายการ */}
      <Modal
        key={target?.item.id}
        open={target !== null}
        onOpenChange={(open) => !open && setTarget(null)}
        title="ท้วงคะแนน"
        description={
          target && `${target.classroom} · ${target.item.name} · ได้ ${target.item.value ?? '—'} / ${target.item.maxScore}`
        }
        onSubmit={openAppeal}
        submitLabel="ส่งเรื่องท้วง"
        busy={form.busy}
        error={form.error}
      >
        <label htmlFor="appeal-body" className="text-sm font-medium">
          เหตุผลที่ท้วง
        </label>
        <textarea
          id="appeal-body"
          name="body"
          rows={4}
          maxLength={2000}
          placeholder="เช่น ข้อ 3 ตอบถูกแต่ไม่ได้คะแนน"
          className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </Modal>
    </>
  )
}
