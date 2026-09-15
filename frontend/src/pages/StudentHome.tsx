import { useQuery } from '@tanstack/react-query'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { ScoreList, type ScoreRow } from '@/components/ScoreList'
import { api } from '@/lib/api'
import { qk } from '@/lib/keys'

type MyRoom = { classroomId: number; classroom: string; term: string; no: number; items: ScoreRow[] }

/** หน้าแรกของนักเรียนที่ล็อกอินแล้ว · server ดึงคะแนนจาก cookie ของเจ้าตัวเท่านั้น */
export default function StudentHome() {
  const rooms = useQuery({ queryKey: qk.myScores, queryFn: () => api<MyRoom[]>('/me/scores') })

  return (
    <>
      <PageHeader title="คะแนนของฉัน" description="คะแนนทุกห้องที่เรียน เรียงจากภาคเรียนล่าสุด" />

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
                <ScoreList items={room.items} />
              </section>
            ))}
          </div>
        )}
      </QueryState>
    </>
  )
}
