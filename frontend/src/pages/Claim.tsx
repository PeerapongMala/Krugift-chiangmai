import { useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { AuthCard } from '@/components/AuthCard'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Button, buttonVariants } from '@/components/ui/button'
import { api } from '@/lib/api'
import { homeOf, useMe } from '@/lib/auth'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'
import { validate } from '@/lib/validate'

/**
 * เชื่อมบัญชี Google กับนักเรียนครั้งแรก — กรอกแค่รหัสนักเรียน
 * บัญชี Google คือตัวยืนยันตัวตน รหัสนักเรียนเป็นแค่ตัวชี้ว่าเป็นใครในระบบ
 */
export default function Claim() {
  const { data: me, isPending } = useMe()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { busy, error, run, check } = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const studentCode = String(new FormData(e.currentTarget).get('studentCode') ?? '')
    if (!check(validate.studentCode(studentCode))) return

    const ok = await run(async () => {
      await api('/auth/claim', { method: 'POST', json: { studentCode } })
      await queryClient.invalidateQueries({ queryKey: qk.me })
    })
    if (ok) navigate(routes.student, { replace: true })
  }

  if (isPending) return null
  if (!me) return <Navigate to={routes.login} replace />
  if (me.role !== 'pending') return <Navigate to={homeOf(me.role)} replace />

  return (
    <AuthCard
      title="ยืนยันตัวตนครั้งแรก"
      description={<>บัญชี <b>{me.name}</b> กรอกรหัสนักเรียนเพื่อเชื่อมบัญชี</>}
    >
      <form onSubmit={onSubmit} className="grid gap-4">
        <Field
          label="รหัสนักเรียน"
          name="studentCode"
          inputMode="numeric"
          autoComplete="off"
          required
        />
        <FormError message={error} />
        <Button type="submit" disabled={busy}>
          {busy ? 'กำลังตรวจสอบ...' : 'ยืนยันและเชื่อมบัญชี'}
        </Button>
      </form>

      {/* พาไปเลือกบัญชีใหม่เลย · server ล้าง cookie เดิมและบังคับ Google ถามบัญชีให้อยู่แล้ว */}
      <a href="/api/auth/google" className={buttonVariants({ variant: 'ghost' })}>
        ใช้บัญชี Google อื่น
      </a>
    </AuthCard>
  )
}
