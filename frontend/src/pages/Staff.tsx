import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Navigate } from 'react-router'
import { useConfirm } from '@/components/ConfirmDialog'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Modal } from '@/components/Modal'
import { PageHeader } from '@/components/PageHeader'
import { QueryState } from '@/components/QueryState'
import { Rows, Row, RowActions } from '@/components/Rows'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

type StaffRole = 'Owner' | 'Teacher'
type Staff = { id: number; email: string; name: string; role: StaffRole; isMe: boolean }

const ROLE_LABEL: Record<StaffRole, string> = { Owner: 'ผู้ดูแลระบบ', Teacher: 'ครู' }

export default function StaffPage() {
  const { data: me, isPending: mePending } = useMe()
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)
  const addForm = useSubmit()
  const rowAction = useSubmit()
  const { confirm, dialog } = useConfirm()

  const isOwner = me?.isOwner === true
  const staff = useQuery({
    queryKey: qk.staff,
    queryFn: () => api<Staff[]>('/staff'),
    enabled: isOwner,
  })

  if (mePending) return null
  // หน้านี้ของเจ้าของเท่านั้น · server กันไว้อีกชั้นด้วย policy owner ไม่ได้พึ่งการซ่อนหน้าอย่างเดียว
  if (!isOwner) return <Navigate to={routes.teacher} replace />

  const refresh = () => queryClient.invalidateQueries({ queryKey: qk.staff })

  async function add(form: FormData) {
    const email = String(form.get('email') ?? '')
    if (!addForm.check(validate.email(email))) return

    const ok = await addForm.run(async () => {
      await api('/staff', { method: 'POST', json: { email, role: 'Teacher' } })
      await refresh()
    })
    if (ok) setAdding(false)
  }

  async function toggleRole(person: Staff) {
    const next: StaffRole = person.role === 'Owner' ? 'Teacher' : 'Owner'
    const promoting = next === 'Owner'

    const ok = await confirm({
      title: promoting ? 'ตั้งเป็นผู้ดูแลระบบ?' : 'ยกเลิกสิทธิ์ผู้ดูแลระบบ?',
      description: person.email,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/staff/${person.id}`, { method: 'PATCH', json: { role: next } })
      await refresh()
    })
  }

  async function remove(person: Staff) {
    const ok = await confirm({
      title: 'ลบครูคนนี้?',
      description: person.email,
      confirmLabel: 'ลบ',
      destructive: true,
    })
    if (!ok) return

    await rowAction.run(async () => {
      await api(`/staff/${person.id}`, { method: 'DELETE' })
      await refresh()
    })
  }

  return (
    <>
      <PageHeader title="จัดการครู">
        <Button onClick={() => setAdding(true)}>เพิ่มครู</Button>
      </PageHeader>

      <FormError message={rowAction.error} />

      <QueryState query={staff} empty="ยังไม่มีครูในระบบ">
        {(list) => (
          <Rows>
            {list.map((person) => (
              <Row key={person.id}>
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">
                    {person.name || person.email}
                    {person.isMe && <span className="ml-2 text-xs text-muted-foreground">(คุณ)</span>}
                  </p>
                  {person.name && <p className="truncate text-xs text-muted-foreground">{person.email}</p>}
                </div>

                <RowActions>
                  <span
                    className={
                      person.role === 'Owner'
                        ? 'rounded-full bg-accent px-2.5 py-0.5 text-xs text-accent-foreground'
                        : 'rounded-full bg-muted px-2.5 py-0.5 text-xs text-muted-foreground'
                    }
                  >
                    {ROLE_LABEL[person.role]}
                  </span>

                  {/* เปลี่ยนสิทธิ์หรือลบตัวเองไม่ได้ ตรงกับที่ server กันไว้ */}
                  {!person.isMe && (
                    <>
                      <Button variant="outline" size="sm" disabled={rowAction.busy} onClick={() => toggleRole(person)}>
                        {person.role === 'Owner' ? 'ยกเลิกสิทธิ์ผู้ดูแล' : 'ตั้งเป็นผู้ดูแลระบบ'}
                      </Button>
                      <Button variant="destructive" size="sm" disabled={rowAction.busy} onClick={() => remove(person)}>
                        ลบ
                      </Button>
                    </>
                  )}
                </RowActions>
              </Row>
            ))}
          </Rows>
        )}
      </QueryState>

      <Modal
        open={adding}
        onOpenChange={setAdding}
        title="เพิ่มครู"
        onSubmit={add}
        submitLabel="เพิ่ม"
        busy={addForm.busy}
        error={addForm.error}
      >
        <Field label="อีเมล Google ของครู" name="email" type="email" autoComplete="off" required />
      </Modal>

      {dialog}
    </>
  )
}
