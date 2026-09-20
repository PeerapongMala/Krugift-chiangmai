import type { ReactNode } from 'react'
import { Credit } from '@/components/Credit'
import { Mascot } from '@/components/Mascot'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { APP_NAME } from '@/lib/app'

export function AuthCard({ title, description, children }: { title: string; description?: ReactNode; children: ReactNode }) {
  return (
    <div className="auth-bg relative flex min-h-svh flex-col items-center justify-center gap-4 px-4 pt-8 pb-14">
      <Card className="w-full max-w-sm">
        {/* CardHeader เป็น grid จัดกลางแนวนอนต้องใช้ justify-items-center ไม่ใช่ items-center */}
        <CardHeader className="justify-items-center gap-2 text-center">
          {/*
            คาปิบาร่า + ชื่อแอป เป็นโลโก้ก้อนเดียวกัน กดแล้วกลับหน้าแรก (ใช้ <a> เพราะอยากให้โหลดใหม่ทั้งหน้าจริง ๆ)
            หน้าพวกนี้ไม่มีแถบหัวเว็บ ถ้าไม่บอกชื่อแอปคนเปิดจะไม่รู้ว่ากำลังดูของใคร
            แยกบทบาทกับหัวเรื่องด้วย "สี" ไม่ใช่ "ขนาด" ชื่อแอปจึงเด่นได้โดยไม่แย่งสายตาจากชื่อนักเรียนหรือชื่อหน้า
          */}
          <a
            href="/"
            className="mb-1 grid justify-items-center gap-1 rounded-2xl px-2 py-1 transition hover:brightness-95 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          >
            <Mascot name="orange" priority className="h-28 w-auto" />
            <span className="text-lg font-bold tracking-tight text-primary">{APP_NAME}</span>
          </a>
          <CardTitle className="text-xl">{title}</CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </CardHeader>
        <CardContent className="grid gap-6">{children}</CardContent>
      </Card>
      <Credit className="absolute bottom-3 left-4" />
    </div>
  )
}
