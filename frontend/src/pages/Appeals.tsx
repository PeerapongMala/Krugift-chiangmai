import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { AppealStatusBadge, type AppealStatus } from '@/components/AppealStatusBadge'
import { Meta } from '@/components/Meta'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row } from '@/components/Rows'
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
      />

      <QueryState query={appeals} empty="ยังไม่มีคำถามเรื่องคะแนน">
        {(list) => (
          <Rows>
            {list.map((appeal) => (
              <Row key={appeal.id} className="p-0">
                <Link
                  to={routes.appeal(base, appeal.id)}
                  className="flex w-full items-start gap-3 px-3 py-2.5 hover:bg-muted"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">
                      {/* ป้ายนี้หายเมื่อเปิดเข้าไปอ่านเท่านั้น (server ล้างสถานะตอนเปิดคำถาม) */}
                      {appeal.unread && (
                        <span className="mr-2 inline-flex rounded-full bg-destructive px-2 py-0.5 align-middle text-[11px] leading-4 font-medium text-white">
                          ข้อความใหม่
                        </span>
                      )}
                      {appeal.item}
                    </p>
                    <Meta>
                      {isTeacher && (
                        <span>
                          {appeal.student} ({appeal.studentCode})
                        </span>
                      )}
                      <span>{appeal.classroom}</span>
                      <span>{appeal.term}</span>
                    </Meta>
                    {appeal.lastMessage && (
                      <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{appeal.lastMessage}</p>
                    )}
                  </div>
                  <AppealStatusBadge status={appeal.status} viewer={isTeacher ? 'teacher' : 'student'} />
                </Link>
              </Row>
            ))}
          </Rows>
        )}
      </QueryState>
    </>
  )
}
