import type { ComponentProps, ReactNode } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type FieldProps = ComponentProps<typeof Input> & {
  label: string
  /** คำอธิบายใต้ช่องกรอก */
  hint?: ReactNode
}

/** Label + Input ที่ผูก htmlFor/id ให้อัตโนมัติ กันลืมจนกดที่ label แล้วไม่โฟกัส */
export function Field({ label, hint, id, name, ...input }: FieldProps) {
  const fieldId = id ?? name

  return (
    <div className="grid gap-2">
      <Label htmlFor={fieldId}>{label}</Label>
      <Input id={fieldId} name={name} {...input} />
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  )
}
