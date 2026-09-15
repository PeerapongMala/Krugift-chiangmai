import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { AppealStatusBadge, type AppealStatus } from '@/components/AppealStatusBadge'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { api } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { qk, routes } from '@/lib/keys'

type AppealRow = {
  id: number
  status: AppealStatus
  createdAt: string
  unread: boolean
  item: string
  classroom: string
  term: string
  student: string
  studentCode: string
  lastMessage: string | null
}

/** รายการคำถามเรื่องคะแนน · ครูเห็นคำถามในห้องของตัวเอง นักเรียนเห็นคำถามของตัวเอง (server คัดให้) */
export default function Appeals() {
  const { data: me } = useMe()
  const isTeacher = me?.role === 'teacher'
  const base = isTeacher ? routes.teacherAppeals : routes.studentAppeals
  const appeals = useQuery({ queryKey: qk.appeals, queryFn: () => api<AppealRow[]>('/appeals') })

  return (
    <>
      <PageHeader
        title="สอบถามคะแนน"
        description={
          isTeacher
            ? 'คำถามเรื่องคะแนนจากนักเรียน · คำถามที่ยังไม่เสร็จสิ้นอยู่บนสุด'
            : 'คำถามที่คุณส่งถึงครู · จะถามเรื่องใหม่ให้กดปุ่ม "สอบถาม" ข้างคะแนนในหน้าคะแนนของฉัน'
        }
      />

      <QueryState query={appeals} empty="ยังไม่มีคำถามเรื่องคะแนน">
        {(list) => (
          <ul className="grid gap-2">
            {list.map((appeal) => (
              <li key={appeal.id}>
                <Link
                  to={routes.appeal(base, appeal.id)}
                  className="flex items-start gap-3 rounded-lg border bg-card p-3 hover:bg-accent/40"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">
                      {appeal.unread && (
                        <span
                          className="mr-2 inline-block size-2 rounded-full bg-primary align-middle"
                          role="img"
                          aria-label="มีข้อความใหม่"
                        />
                      )}
                      {appeal.item}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">
                      {isTeacher && `${appeal.student} (${appeal.studentCode}) · `}
                      {appeal.classroom} · ภาคเรียน {appeal.term}
                    </p>
                    {appeal.lastMessage && (
                      <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{appeal.lastMessage}</p>
                    )}
                  </div>
                  <AppealStatusBadge status={appeal.status} />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </QueryState>
    </>
  )
}
