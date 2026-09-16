import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { AppealStatusBadge, type AppealStatus } from '@/components/AppealStatusBadge'
import { useConfirm } from '@/components/ConfirmDialog'
import { FormError } from '@/components/FormError'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { cn } from '@/lib/utils'
import { validate } from '@/lib/validate'

type Thread = {
  id: number
  status: AppealStatus
  itemId: number
  classroomId: number
  item: string
  maxScore: number
  score: number | null
  classroom: string
  term: string
  student: string
  studentCode: string
  messages: { id: number; fromTeacher: boolean; body: string; at: string }[]
}

/** หน้า thread ของคำถามเรื่องคะแนนหนึ่งคำถาม ใช้ร่วมกันทั้งครูและนักเรียน */
export default function AppealThread() {
  const appealId = Number(useParams().appealId)
  const { data: me } = useMe()
  const isTeacher = me?.role === 'teacher'
  const base = isTeacher ? routes.teacherAppeals : routes.studentAppeals
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const reply = useSubmit()
  const closing = useSubmit()
  const { confirm, dialog } = useConfirm()

  const thread = useQuery({
    queryKey: qk.appeal(appealId),
    queryFn: () => api<Thread>(`/appeals/${appealId}`),
    enabled: Number.isInteger(appealId),
  })

  // เปิดอ่านแล้ว server ล้างสถานะข้อความใหม่ของฝั่งเรา badge บนเมนูต้องอัปเดตตาม
  useEffect(() => {
    if (thread.data) void queryClient.invalidateQueries({ queryKey: qk.appealsUnread })
  }, [thread.data, queryClient])

  // qk.appeals เป็น prefix ของทั้งรายการ thread และ badge จึงรีเฟรชครบในคำสั่งเดียว
  const refresh = () => queryClient.invalidateQueries({ queryKey: qk.appeals })

  async function send(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!reply.check(validate.message(text))) return

    const ok = await reply.run(async () => {
      await api(`/appeals/${appealId}/messages`, { method: 'POST', json: { body: text } })
      await refresh()
    })
    if (ok) setText('')
  }

  async function closeAppeal() {
    const ok = await confirm({
      title: 'จบคำถามนี้?',
      confirmLabel: 'เสร็จสิ้น',
    })
    if (!ok) return

    await closing.run(async () => {
      await api(`/appeals/${appealId}/close`, { method: 'PATCH' })
      await refresh()
    })
  }

  return (
    <>
      <Link to={base} className="mb-3 inline-block text-sm text-muted-foreground hover:text-foreground">
        ← สอบถามคะแนน
      </Link>

      <QueryState query={thread}>
        {(t) => (
          <>
            <PageHeader
              title={t.item}
              description={`${isTeacher ? `${t.student} (${t.studentCode}) · ` : ''}${t.classroom} · ภาคเรียน ${t.term}`}
            >
              <AppealStatusBadge status={t.status} viewer={isTeacher ? 'teacher' : 'student'} />
            </PageHeader>

            <p className="mb-4 rounded-lg border bg-card px-3 py-2 text-sm">
              คะแนนตอนนี้ <b className="tabular-nums">{t.score ?? '—'}</b> / {t.maxScore}
              {isTeacher && (
                <>
                  {' · '}
                  <Link to={routes.classroomScores(t.classroomId)} className="underline underline-offset-2">
                    ไปแก้ในตารางคะแนน
                  </Link>
                </>
              )}
            </p>

            <ol className="mb-4 grid gap-3">
              {t.messages.map((m) => {
                const mine = m.fromTeacher === isTeacher
                return (
                  <li
                    key={m.id}
                    className={cn(
                      'max-w-[85%] rounded-lg px-3 py-2 text-sm',
                      mine ? 'justify-self-end bg-primary text-primary-foreground' : 'justify-self-start border bg-card',
                    )}
                  >
                    <p className="mb-1 text-xs opacity-80">
                      {m.fromTeacher ? 'ครู' : 'นักเรียน'} ·{' '}
                      {new Date(m.at).toLocaleString('th-TH', { dateStyle: 'medium', timeStyle: 'short' })}
                    </p>
                    <p className="break-words whitespace-pre-wrap">{m.body}</p>
                  </li>
                )
              })}
            </ol>

            {t.status === 'Closed' ? (
              <p className="text-center text-sm text-muted-foreground">คำถามนี้เสร็จสิ้นแล้ว</p>
            ) : (
              <form onSubmit={send} className="grid gap-2">
                <label htmlFor="reply" className="text-sm font-medium">
                  ตอบกลับ
                </label>
                <textarea
                  id="reply"
                  value={text}
                  onChange={(e) => setText(e.target.value)}
                  rows={3}
                  maxLength={2000}
                  className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
                <FormError message={reply.error || closing.error} />
                <div className="flex flex-wrap justify-end gap-2">
                  <Button type="button" variant="success" onClick={closeAppeal} disabled={closing.busy}>
                    เสร็จสิ้น
                  </Button>
                  <Button type="submit" disabled={reply.busy}>
                    {reply.busy ? 'กำลังส่ง...' : 'ส่ง'}
                  </Button>
                </div>
              </form>
            )}
          </>
        )}
      </QueryState>

      {dialog}
    </>
  )
}
