import type { ReactNode } from 'react'

/** หัวข้อหน้า + ปุ่มการกระทำ children คือปุ่มด้านขวา เช่น เพิ่มห้องเรียน */
export function PageHeader({
  title,
  description,
  children,
}: {
  title: string
  description?: ReactNode
  children?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h1 className="font-heading text-xl font-semibold">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {children && <div className="flex shrink-0 flex-wrap gap-2">{children}</div>}
    </div>
  )
}
