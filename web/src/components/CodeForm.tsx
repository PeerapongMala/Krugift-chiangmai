import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, ApiError } from '@/lib/api'

/** ฟอร์ม รหัสนักเรียน + รหัสส่วนตัว ใช้ทั้งหน้า login (code-login) และหน้าผูกบัญชี Google (claim) */
export function CodeForm({ endpoint, submitLabel }: { endpoint: string; submitLabel: string }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    setBusy(true)
    setError('')
    try {
      await api(endpoint, {
        method: 'POST',
        json: { studentCode: form.get('studentCode'), code: form.get('code') },
      })
      await queryClient.invalidateQueries({ queryKey: ['me'] })
      navigate('/student', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-4">
      <div className="grid gap-2">
        <Label htmlFor="studentCode">รหัสนักเรียน</Label>
        <Input id="studentCode" name="studentCode" autoComplete="username" required />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="code">รหัสส่วนตัว (ได้จากครู)</Label>
        <Input
          id="code"
          name="code"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          className="font-mono tracking-widest uppercase"
          required
        />
      </div>
      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      <Button type="submit" disabled={busy}>
        {busy ? 'กำลังตรวจสอบ...' : submitLabel}
      </Button>
    </form>
  )
}
