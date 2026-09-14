import { useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { Field } from '@/components/Field'
import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { qk, routes } from '@/lib/keys'
import { useSubmit } from '@/lib/useSubmit'

/** ฟอร์ม รหัสนักเรียน + รหัสส่วนตัว ใช้ทั้งหน้า login (code-login) และหน้าผูกบัญชี Google (claim) */
export function CodeForm({ endpoint, submitLabel }: { endpoint: string; submitLabel: string }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { busy, error, run } = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)

    const ok = await run(async () => {
      await api(endpoint, {
        method: 'POST',
        json: { studentCode: form.get('studentCode'), code: form.get('code') },
      })
      await queryClient.invalidateQueries({ queryKey: qk.me })
    })

    if (ok) navigate(routes.student, { replace: true })
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-4">
      <Field label="รหัสนักเรียน" name="studentCode" autoComplete="username" required />
      <Field
        label="รหัสส่วนตัว (ได้จากครู)"
        name="code"
        autoComplete="off"
        autoCapitalize="characters"
        spellCheck={false}
        className="font-mono tracking-widest uppercase"
        required
      />
      <FormError message={error} />
      <Button type="submit" disabled={busy}>
        {busy ? 'กำลังตรวจสอบ...' : submitLabel}
      </Button>
    </form>
  )
}
